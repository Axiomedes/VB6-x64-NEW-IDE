using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ViBa7.ProjectModel.Formatting;

namespace ViBa7.IDE.Dialogs;

public partial class SortProceduresDialog : Window
{
    private readonly string _originalHeader;
    private readonly List<ProcedureSortItem> _originalProcedures;
    private List<ProcedureSortItem> _currentSortedList = new();

    public string ResultingCode { get; private set; } = string.Empty;
    public bool HasProcedures => _originalProcedures.Count > 0;

    public SortProceduresDialog(string code)
    {
        InitializeComponent();

        var (header, procs) = ProcedureSorter.ParseProcedures(code);
        _originalHeader = header;
        _originalProcedures = procs;

        TxtProcCount.Text = $"Total: {_originalProcedures.Count} procedimientos encontrados";

        UpdateSortedPreview();
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_originalProcedures == null) return;
        UpdateSortedPreview();
    }

    private void UpdateSortedPreview()
    {
        var options = new ProcedureSortOptions
        {
            Order = ProcedureSortOrder.AlphabeticalAsc,
            GroupProperties = ChkGroupProperties?.IsChecked == true,
            PreserveComments = ChkPreserveComments?.IsChecked == true
        };

        if (RbAlphaDesc?.IsChecked == true) options.Order = ProcedureSortOrder.AlphabeticalDesc;
        else if (RbByType?.IsChecked == true) options.Order = ProcedureSortOrder.ByType;
        else if (RbByScope?.IsChecked == true) options.Order = ProcedureSortOrder.ByScope;

        _currentSortedList = ProcedureSorter.SortProcedures(_originalProcedures, options);

        if (DgProcedures != null)
        {
            DgProcedures.ItemsSource = null;
            DgProcedures.ItemsSource = _currentSortedList;
        }

        ResultingCode = ProcedureSorter.GenerateSortedCode(_originalHeader, _currentSortedList, options.PreserveComments);

        if (TxtPreviewCode != null)
        {
            TxtPreviewCode.Text = ResultingCode;
        }
    }

    private void DgProcedures_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DgProcedures.SelectedItem is ProcedureSortItem item)
        {
            // Opcional: enfocar el procedimiento en el preview
            string target = item.FullText;
            int idx = TxtPreviewCode.Text.IndexOf(item.Name, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                TxtPreviewCode.Select(idx, item.Name.Length);
            }
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_originalProcedures.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron procedimientos en el módulo actual para ordenar.", "Ordenar Procedimientos", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = false;
            return;
        }

        DialogResult = true;
    }
}
