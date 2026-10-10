using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Dialogs;

public partial class TabOrderDialog : Window
{
    private readonly string _originalHeader;
    private readonly List<FormControlItem> _originalControls;
    private List<FormControlItem> _currentControls = new();

    public string ResultingHeader { get; private set; } = string.Empty;
    public bool HasControls => _originalControls.Count > 0;

    public TabOrderDialog(string formHeaderAndDesign, string formName = "Formulario")
    {
        InitializeComponent();

        _originalHeader = formHeaderAndDesign ?? string.Empty;
        TxtHeader.Text = $"Formulario: {formName}";

        _originalControls = TabOrderEngine.ParseControls(_originalHeader);
        _currentControls = CloneList(_originalControls);

        TxtControlCount.Text = $"{_originalControls.Count} controles encontrados";
        RefreshGrid();
    }

    private void RefreshGrid(int selectIndex = -1)
    {
        // Reindexar secuencialmente
        for (int i = 0; i < _currentControls.Count; i++)
        {
            _currentControls[i].TabIndex = i;
        }

        DgControls.ItemsSource = null;
        DgControls.ItemsSource = _currentControls;

        if (selectIndex >= 0 && selectIndex < _currentControls.Count)
        {
            DgControls.SelectedIndex = selectIndex;
        }
    }

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
    {
        int idx = DgControls.SelectedIndex;
        if (idx > 0)
        {
            var item = _currentControls[idx];
            _currentControls.RemoveAt(idx);
            _currentControls.Insert(idx - 1, item);
            RefreshGrid(idx - 1);
        }
    }

    private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
    {
        int idx = DgControls.SelectedIndex;
        if (idx >= 0 && idx < _currentControls.Count - 1)
        {
            var item = _currentControls[idx];
            _currentControls.RemoveAt(idx);
            _currentControls.Insert(idx + 1, item);
            RefreshGrid(idx + 1);
        }
    }

    private void BtnAutoGeographic_Click(object sender, RoutedEventArgs e)
    {
        _currentControls = TabOrderEngine.AutoOrderByGeography(_currentControls);
        RefreshGrid();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _currentControls = CloneList(_originalControls);
        RefreshGrid();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_originalControls.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron controles en este formulario.", "Orden de Tabulación", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = false;
            return;
        }

        ResultingHeader = TabOrderEngine.ApplyTabOrder(_originalHeader, _currentControls);
        DialogResult = true;
    }

    private static List<FormControlItem> CloneList(List<FormControlItem> src)
    {
        return src.Select(c => new FormControlItem
        {
            Name = c.Name,
            TypeName = c.TypeName,
            TabIndex = c.TabIndex,
            TabStop = c.TabStop,
            Left = c.Left,
            Top = c.Top,
            Width = c.Width,
            Height = c.Height,
            Caption = c.Caption,
            ContainerName = c.ContainerName,
            HasExplicitTabIndex = c.HasExplicitTabIndex
        }).ToList();
    }
}
