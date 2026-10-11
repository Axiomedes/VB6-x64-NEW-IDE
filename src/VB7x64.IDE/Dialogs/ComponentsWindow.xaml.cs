using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VB7x64.ProjectModel.ActiveX;
using VB7x64.ProjectModel.IntelliSense;
using VB7x64.ProjectModel.Vbp;

namespace VB7x64.IDE.Dialogs;

public class OcxComponentItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public string DisplayTitle { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string LibraryName { get; init; } = string.Empty;
    public string Guid { get; init; } = string.Empty;
    public int MajorVersion { get; init; }
    public int MinorVersion { get; init; }
    public string ControlsSummary { get; init; } = string.Empty;
    public OcxLibraryInfo LibraryInfo { get; init; } = null!;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class ComponentsWindow : Window
{
    private readonly VB6Project? _project;
    public ObservableCollection<OcxComponentItemViewModel> Components { get; } = [];

    public List<OcxLibraryInfo> SelectedLibraries { get; } = [];
    public List<OcxLibraryInfo> DeselectedLibraries { get; } = [];

    public ComponentsWindow(VB6Project? project = null)
    {
        _project = project;
        InitializeComponent();
        LstComponents.ItemsSource = Components;
        LoadKnownComponents();
    }

    private void LoadKnownComponents()
    {
        Components.Clear();

        // 1. Bibliotecas ya cargadas en IntelliSense
        var registered = VbControlMemberCatalog.GetRegisteredOcxLibraries();
        foreach (var lib in registered)
        {
            AddComponentViewModel(lib, isSelected: true);
        }

        // 2. Controles declarados en el archivo de proyecto (.vbp)
        if (_project != null)
        {
            foreach (var item in _project.ActiveXControls)
            {
                if (Components.Any(c => string.Equals(c.FilePath, item.AbsolutePath, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(c.LibraryName, item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string? resolved = OcxTypeLibReader.ResolveOcxPath(item.RelativePath, _project.ProjectDirectory);
                if (string.IsNullOrEmpty(resolved) && !string.IsNullOrEmpty(item.AbsolutePath) && File.Exists(item.AbsolutePath))
                {
                    resolved = item.AbsolutePath;
                }

                if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                {
                    if (OcxTypeLibReader.TryReadTypeLib(resolved, out var lib, out _))
                    {
                        AddComponentViewModel(lib!, isSelected: true);
                    }
                }
            }
        }

        if (Components.Count > 0)
        {
            LstComponents.SelectedIndex = 0;
        }
    }

    private void AddComponentViewModel(OcxLibraryInfo lib, bool isSelected)
    {
        if (Components.Any(c => string.Equals(c.FilePath, lib.FilePath, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(c.LibraryName, lib.LibraryName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        string title = !string.IsNullOrWhiteSpace(lib.Description)
            ? $"{lib.Description} ({lib.LibraryName})"
            : lib.LibraryName;

        string ctrls = lib.Controls.Count > 0
            ? string.Join(", ", lib.Controls.Select(c => c.Name))
            : "(Ningún control visual)";

        Components.Add(new OcxComponentItemViewModel
        {
            DisplayTitle = title,
            FilePath = lib.FilePath,
            LibraryName = lib.LibraryName,
            Guid = lib.Guid,
            MajorVersion = lib.MajorVersion,
            MinorVersion = lib.MinorVersion,
            ControlsSummary = ctrls,
            LibraryInfo = lib,
            IsSelected = isSelected
        });
    }

    private void LstComponents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstComponents.SelectedItem is OcxComponentItemViewModel item)
        {
            TxtFilePath.Text = item.FilePath;
            TxtLibrary.Text = $"{item.LibraryName} (v{item.MajorVersion}.{item.MinorVersion}) - {item.Guid}";
            TxtControls.Text = item.ControlsSummary;
        }
        else
        {
            TxtFilePath.Text = "(Ninguno)";
            TxtLibrary.Text = "-";
            TxtControls.Text = "-";
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Seleccionar Control ActiveX / Biblioteca de Tipos",
            Filter = "Controles ActiveX (*.ocx;*.tlb;*.dll)|*.ocx;*.tlb;*.dll|Todos los archivos (*.*)|*.*",
            Multiselect = true
        };

        if (ofd.ShowDialog(this) == true)
        {
            foreach (var file in ofd.FileNames)
            {
                if (OcxTypeLibReader.TryReadTypeLib(file, out var lib, out string? error))
                {
                    var existing = Components.FirstOrDefault(c =>
                        string.Equals(c.FilePath, lib!.FilePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.LibraryName, lib.LibraryName, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.IsSelected = true;
                        LstComponents.SelectedItem = existing;
                    }
                    else
                    {
                        AddComponentViewModel(lib!, isSelected: true);
                        LstComponents.SelectedItem = Components.Last();
                    }
                }
                else
                {
                    MessageBox.Show(
                        this,
                        $"No se pudo leer la Type Library del archivo seleccionado:\n\n{file}\n\nDetalles del error: {error}",
                        "Error de Lectura ActiveX",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in Components) c.IsSelected = true;
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in Components) c.IsSelected = false;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        SelectedLibraries.Clear();
        DeselectedLibraries.Clear();

        foreach (var c in Components)
        {
            if (c.IsSelected)
            {
                SelectedLibraries.Add(c.LibraryInfo);
            }
            else
            {
                DeselectedLibraries.Add(c.LibraryInfo);
            }
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
