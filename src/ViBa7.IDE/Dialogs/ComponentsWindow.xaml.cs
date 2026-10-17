using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ViBa7.ProjectModel.ActiveX;
using ViBa7.ProjectModel.IntelliSense;
using ViBa7.ProjectModel.Vbp;

namespace ViBa7.IDE.Dialogs;

public class ComponentItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _displayTitle = string.Empty;
    private string _subtitle = string.Empty;
    private string _filePath = string.Empty;
    private string _guid = string.Empty;
    private int _majorVersion = 1;
    private int _minorVersion = 0;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                SelectionChangedCallback?.Invoke();
            }
        }
    }

    public string DisplayTitle
    {
        get => _displayTitle;
        set
        {
            if (_displayTitle != value)
            {
                _displayTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
            }
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            if (_subtitle != value)
            {
                _subtitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
            }
        }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePath)));
            }
        }
    }

    public string LibraryName { get; set; } = string.Empty;

    public string Guid
    {
        get => _guid;
        set
        {
            if (_guid != value)
            {
                _guid = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Guid)));
            }
        }
    }

    public int MajorVersion
    {
        get => _majorVersion;
        set
        {
            if (_majorVersion != value)
            {
                _majorVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MajorVersion)));
            }
        }
    }

    public int MinorVersion
    {
        get => _minorVersion;
        set
        {
            if (_minorVersion != value)
            {
                _minorVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinorVersion)));
            }
        }
    }

    public bool IsControl { get; init; }
    public string TypeBadgeIcon => IsControl ? "🧩" : "📚";
    public string ControlsSummary { get; set; } = string.Empty;
    public OcxLibraryInfo? LibraryInfo { get; set; }
    public bool WasInitiallySelected { get; set; }
    public Action? SelectionChangedCallback { get; set; }

    public void RefreshSubtitle()
    {
        string fileNameDisplay = !string.IsNullOrEmpty(FilePath) ? Path.GetFileName(FilePath) : "(Ruta no definida)";
        string guidDisplay = !string.IsNullOrEmpty(Guid) ? $" • {Guid}" : string.Empty;
        Subtitle = $"{fileNameDisplay} (v{MajorVersion}.{MinorVersion}){guidDisplay}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class ComponentsWindow : Window
{
    private readonly VB6Project? _project;
    private readonly List<ComponentItemViewModel> _allComponents = [];
    public ObservableCollection<ComponentItemViewModel> FilteredComponents { get; } = [];

    public List<ComponentItemViewModel> SelectedItems { get; } = [];
    public List<ComponentItemViewModel> DeselectedItems { get; } = [];

    public List<OcxLibraryInfo> SelectedLibraries => SelectedItems
        .Where(i => i.LibraryInfo != null)
        .Select(i => i.LibraryInfo!)
        .ToList();

    public List<OcxLibraryInfo> DeselectedLibraries => DeselectedItems
        .Where(i => i.LibraryInfo != null)
        .Select(i => i.LibraryInfo!)
        .ToList();

    public ComponentsWindow(VB6Project? project = null)
    {
        _project = project;
        InitializeComponent();
        LstComponents.ItemsSource = FilteredComponents;
        Loaded += async (s, e) => await LoadAllComponentsAsync();
    }

    private async System.Threading.Tasks.Task LoadAllComponentsAsync()
    {
        _allComponents.Clear();

        // 1. Bibliotecas OCX ya activas en IntelliSense
        var registeredCatalog = VbControlMemberCatalog.GetRegisteredOcxLibraries();
        foreach (var lib in registeredCatalog)
        {
            AddComponentItem(
                guid: lib.Guid,
                major: lib.MajorVersion,
                minor: lib.MinorVersion,
                title: !string.IsNullOrWhiteSpace(lib.Description) ? lib.Description : lib.LibraryName,
                filePath: lib.FilePath,
                libName: lib.LibraryName,
                isControl: lib.Controls.Any(c => c.IsControl) || lib.FilePath.EndsWith(".ocx", StringComparison.OrdinalIgnoreCase),
                isSelected: true,
                libInfo: lib);
        }

        // 2. Componentes declarados en el archivo de proyecto (.vbp)
        if (_project != null)
        {
            foreach (var item in _project.ActiveXControls)
            {
                string? resolved = OcxTypeLibReader.ResolveOcxPath(item.RelativePath, _project.ProjectDirectory, item.Guid);
                if (string.IsNullOrEmpty(resolved) && !string.IsNullOrEmpty(item.AbsolutePath) && File.Exists(item.AbsolutePath))
                {
                    resolved = item.AbsolutePath;
                }

                string effectivePath = !string.IsNullOrEmpty(resolved) ? resolved : item.RelativePath;

                OcxLibraryInfo? lib = null;
                if (!string.IsNullOrEmpty(effectivePath) && File.Exists(effectivePath))
                {
                    OcxTypeLibReader.TryReadTypeLib(effectivePath, out lib, out _);
                }

                string title = lib != null && !string.IsNullOrWhiteSpace(lib.Description) ? lib.Description :
                               (lib != null && !string.IsNullOrWhiteSpace(lib.LibraryName) ? lib.LibraryName : item.Name);
                string guid = lib?.Guid ?? item.Guid ?? string.Empty;
                int maj = lib?.MajorVersion ?? (int.TryParse(item.MajorVersion, out int mj) ? mj : 1);
                int min = lib?.MinorVersion ?? (int.TryParse(item.MinorVersion, out int mn) ? mn : 0);
                string libName = lib?.LibraryName ?? item.Name;

                AddComponentItem(
                    guid: guid,
                    major: maj,
                    minor: min,
                    title: title,
                    filePath: effectivePath,
                    libName: libName,
                    isControl: true,
                    isSelected: true,
                    libInfo: lib);
            }

            foreach (var r in _project.References)
            {
                OcxLibraryInfo? lib = null;
                if (!string.IsNullOrEmpty(r.FilePath) && File.Exists(r.FilePath))
                {
                    OcxTypeLibReader.TryReadTypeLib(r.FilePath, out lib, out _);
                }

                string title = lib != null && !string.IsNullOrWhiteSpace(lib.Description) ? lib.Description :
                               (!string.IsNullOrWhiteSpace(r.Description) ? r.Description : r.DisplayTitle);
                string libName = lib?.LibraryName ?? (!string.IsNullOrEmpty(r.Description) ? r.Description : Path.GetFileNameWithoutExtension(r.FilePath));

                AddComponentItem(
                    guid: r.Guid,
                    major: int.TryParse(r.MajorVersion, out int maj) ? maj : (lib?.MajorVersion ?? 1),
                    minor: int.TryParse(r.MinorVersion, out int min) ? min : (lib?.MinorVersion ?? 0),
                    title: title,
                    filePath: r.FilePath,
                    libName: libName,
                    isControl: false,
                    isSelected: true,
                    libInfo: lib);
            }
        }

        // Mostrar de inmediato los componentes del proyecto
        ApplyFilter();
        if (FilteredComponents.Count > 0)
        {
            LstComponents.SelectedIndex = 0;
        }

        // 3. Descubrir todos los componentes ActiveX (OCX y DLLs) registrados en Windows en segundo plano
        if (PnlLoading != null)
        {
            PnlLoading.Visibility = Visibility.Visible;
        }

        try
        {
            var systemComponents = await System.Threading.Tasks.Task.Run(() => SystemActiveXRegistryScanner.GetRegisteredComponents());
            foreach (var sys in systemComponents)
            {
                AddComponentItem(
                    guid: sys.Guid,
                    major: sys.MajorVersion,
                    minor: sys.MinorVersion,
                    title: sys.DisplayTitle,
                    filePath: sys.FilePath,
                    libName: sys.Name,
                    isControl: sys.IsControl,
                    isSelected: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al escanear registro ActiveX: {ex.Message}");
        }
        finally
        {
            if (PnlLoading != null)
            {
                PnlLoading.Visibility = Visibility.Collapsed;
            }
            ApplyFilter();
            if (FilteredComponents.Count > 0 && LstComponents.SelectedIndex < 0)
            {
                LstComponents.SelectedIndex = 0;
            }
        }
    }

    private void AddComponentItem(
        string guid,
        int major,
        int minor,
        string title,
        string filePath,
        string libName,
        bool isControl,
        bool isSelected,
        OcxLibraryInfo? libInfo = null)
    {
        string normFile = Path.GetFileName(filePath);

        // Evitar duplicados por ruta o por GUID
        var existing = _allComponents.FirstOrDefault(c =>
            (!string.IsNullOrEmpty(filePath) && string.Equals(c.FilePath, filePath, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(guid) && string.Equals(c.Guid, guid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.Guid)) ||
            (string.Equals(Path.GetFileName(c.FilePath), normFile, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(normFile)));

        if (existing != null)
        {
            if (isSelected)
            {
                existing.IsSelected = true;
                existing.WasInitiallySelected = true;
            }
            if (libInfo != null && existing.LibraryInfo == null)
            {
                existing.LibraryInfo = libInfo;
            }

            // Actualizar datos de existing con información más completa del escáner
            if (string.IsNullOrEmpty(existing.Guid) && !string.IsNullOrEmpty(guid))
            {
                existing.Guid = guid;
            }
            if (Path.IsPathRooted(filePath) && (!Path.IsPathRooted(existing.FilePath) || !File.Exists(existing.FilePath)))
            {
                existing.FilePath = filePath;
            }
            if (!string.IsNullOrWhiteSpace(title) && (existing.DisplayTitle == existing.LibraryName || existing.DisplayTitle == Path.GetFileNameWithoutExtension(existing.FilePath) || string.IsNullOrEmpty(existing.DisplayTitle)))
            {
                existing.DisplayTitle = title;
            }
            if (!string.IsNullOrWhiteSpace(libName) && (string.IsNullOrEmpty(existing.LibraryName) || existing.LibraryName == Path.GetFileNameWithoutExtension(existing.FilePath)))
            {
                existing.LibraryName = libName;
            }
            if (major > 1 || minor > 0)
            {
                existing.MajorVersion = major;
                existing.MinorVersion = minor;
            }

            existing.RefreshSubtitle();
            return;
        }

        string fileNameDisplay = !string.IsNullOrEmpty(filePath) ? Path.GetFileName(filePath) : "(Ruta no definida)";
        string guidDisplay = !string.IsNullOrEmpty(guid) ? $" • {guid}" : string.Empty;
        string subtitle = $"{fileNameDisplay} (v{major}.{minor}){guidDisplay}";

        var item = new ComponentItemViewModel
        {
            DisplayTitle = title,
            Subtitle = subtitle,
            FilePath = filePath,
            LibraryName = !string.IsNullOrEmpty(libName) ? libName : Path.GetFileNameWithoutExtension(filePath),
            Guid = guid,
            MajorVersion = major,
            MinorVersion = minor,
            IsControl = isControl,
            LibraryInfo = libInfo,
            IsSelected = isSelected,
            WasInitiallySelected = isSelected,
            SelectionChangedCallback = UpdateSelectionCounters
        };

        _allComponents.Add(item);
    }

    private void ApplyFilter()
    {
        string filterText = TxtSearch?.Text?.Trim() ?? string.Empty;
        int activeTab = TabsCategory?.SelectedIndex ?? 0;

        FilteredComponents.Clear();

        foreach (var c in _allComponents)
        {
            // Filtro por pestaña
            if (activeTab == 0 && !c.IsControl) continue; // Solo Controles OCX
            if (activeTab == 1 && c.IsControl) continue;  // Solo DLLs ActiveX / TLB

            // Filtro por texto de búsqueda
            if (!string.IsNullOrEmpty(filterText))
            {
                bool matches = c.DisplayTitle.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                                c.FilePath.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                                c.LibraryName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                                c.Guid.Contains(filterText, StringComparison.OrdinalIgnoreCase);

                if (!matches) continue;
            }

            FilteredComponents.Add(c);
        }

        UpdateSelectionCounters();
    }

    private void UpdateSelectionCounters()
    {
        if (TxtStatusTotal != null)
        {
            TxtStatusTotal.Text = $"Mostrados: {FilteredComponents.Count} de {_allComponents.Count}";
        }

        if (TxtStatusSelected != null)
        {
            int totalSelected = _allComponents.Count(c => c.IsSelected);
            TxtStatusSelected.Text = $"Seleccionados: {totalSelected}";
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = string.Empty;
        TxtSearch.Focus();
    }

    private void TabsCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void LstComponents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstComponents.SelectedItem is ComponentItemViewModel item)
        {
            // Cargar metadata de la TypeLib en demanda si aún no se tiene
            if (item.LibraryInfo == null && File.Exists(item.FilePath))
            {
                if (OcxTypeLibReader.TryReadTypeLib(item.FilePath, out var lib, out _))
                {
                    item.LibraryInfo = lib;
                    item.LibraryName = lib!.LibraryName;
                    if (lib.Controls.Count > 0)
                    {
                        item.ControlsSummary = string.Join(", ", lib.Controls.Select(c => c.Name));
                    }
                }
            }

            TxtFilePath.Text = !string.IsNullOrEmpty(item.FilePath) ? item.FilePath : "(Sin ruta física)";
            string guidText = !string.IsNullOrEmpty(item.Guid) ? $" - {item.Guid}" : string.Empty;
            TxtLibrary.Text = $"{item.LibraryName} (v{item.MajorVersion}.{item.MinorVersion}){guidText} [{(item.IsControl ? "Control ActiveX" : "DLL ActiveX / TypeLib")}]";

            if (item.LibraryInfo != null && item.LibraryInfo.Controls.Count > 0)
            {
                var visualCtrls = item.LibraryInfo.Controls.Where(c => c.IsControl).Select(c => c.Name).ToList();
                var classes = item.LibraryInfo.Controls.Where(c => !c.IsControl).Select(c => c.Name).ToList();

                var parts = new List<string>();
                if (visualCtrls.Count > 0) parts.Add($"Controles visuales ({visualCtrls.Count}): {string.Join(", ", visualCtrls)}");
                if (classes.Count > 0) parts.Add($"Clases expuestas ({classes.Count}): {string.Join(", ", classes)}");

                TxtControls.Text = string.Join(" | ", parts);
            }
            else if (!string.IsNullOrWhiteSpace(item.ControlsSummary))
            {
                TxtControls.Text = item.ControlsSummary;
            }
            else
            {
                TxtControls.Text = item.IsControl ? "Control visual ActiveX" : "Biblioteca de clases COM / Automatización";
            }
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
            Title = "Seleccionar Componente ActiveX o DLL para Autoregistro",
            Filter = "Componentes ActiveX (*.ocx;*.dll;*.tlb)|*.ocx;*.dll;*.tlb|Controles ActiveX (*.ocx)|*.ocx|DLLs ActiveX (*.dll;*.tlb)|*.dll;*.tlb|Todos los archivos (*.*)|*.*",
            Multiselect = true
        };

        if (ofd.ShowDialog(this) == true)
        {
            int registeredCount = 0;
            ComponentItemViewModel? lastAdded = null;

            foreach (var file in ofd.FileNames)
            {
                // 1. Autoregistrar el componente en Windows
                bool regOk = SystemActiveXRegistryScanner.RegisterActiveXFileInSystem(file, out string? regStatus);

                // 2. Leer la Type Library
                if (OcxTypeLibReader.TryReadTypeLib(file, out var lib, out string? error))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    bool isCtrl = ext == ".ocx" || lib!.Controls.Any(c => c.IsControl);

                    // Buscar si ya está en la lista
                    var existing = _allComponents.FirstOrDefault(c =>
                        string.Equals(c.FilePath, file, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(lib!.Guid) && string.Equals(c.Guid, lib.Guid, StringComparison.OrdinalIgnoreCase)));

                    if (existing != null)
                    {
                        existing.IsSelected = true;
                        existing.LibraryInfo = lib;
                        lastAdded = existing;
                    }
                    else
                    {
                        string title = !string.IsNullOrWhiteSpace(lib!.Description) ? lib.Description : lib.LibraryName;
                        var newItem = new ComponentItemViewModel
                        {
                            DisplayTitle = title,
                            Subtitle = $"{Path.GetFileName(file)} (v{lib.MajorVersion}.{lib.MinorVersion}) • {lib.Guid}",
                            FilePath = file,
                            LibraryName = lib.LibraryName,
                            Guid = lib.Guid,
                            MajorVersion = lib.MajorVersion,
                            MinorVersion = lib.MinorVersion,
                            IsControl = isCtrl,
                            LibraryInfo = lib,
                            IsSelected = true,
                            WasInitiallySelected = false,
                            SelectionChangedCallback = UpdateSelectionCounters
                        };

                        _allComponents.Insert(0, newItem);
                        lastAdded = newItem;
                    }

                    registeredCount++;
                }
                else
                {
                    MessageBox.Show(
                        this,
                        $"El archivo fue autoregistrado, pero no se pudo leer su Type Library:\n\n{file}\n\nDetalle: {error}",
                        "Aviso de Componente",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            if (registeredCount > 0)
            {
                ApplyFilter();
                if (lastAdded != null)
                {
                    LstComponents.SelectedItem = lastAdded;
                    LstComponents.ScrollIntoView(lastAdded);
                }

                MessageBox.Show(
                    this,
                    $"Se {(registeredCount == 1 ? "ha autoregistrado y marcado 1 componente" : $"han autoregistrado y marcado {registeredCount} componentes")} para su uso en el proyecto activo.",
                    "Componentes Registrados",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in FilteredComponents)
        {
            c.IsSelected = true;
        }
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in FilteredComponents)
        {
            c.IsSelected = false;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        SelectedItems.Clear();
        DeselectedItems.Clear();

        foreach (var c in _allComponents)
        {
            if (c.IsSelected)
            {
                SelectedItems.Add(c);
            }
            else if (c.WasInitiallySelected)
            {
                DeselectedItems.Add(c);
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
