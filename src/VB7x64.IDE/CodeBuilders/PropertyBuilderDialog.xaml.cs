using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using VB7x64.ProjectModel.CodeBuilders;

namespace VB7x64.IDE.CodeBuilders;

public partial class PropertyBuilderDialog : Window
{
    private static readonly string[] CommonDataTypes =
    [
        "String", "Long", "Integer", "Boolean", "Double", "Single",
        "Currency", "Date", "Byte", "Variant", "Object", "Collection"
    ];

    private bool _isUpdatingInternally;
    public string GeneratedCode { get; private set; } = "";

    public PropertyBuilderDialog(string initialPropertyName = "")
    {
        InitializeComponent();

        CmbDataType.ItemsSource = CommonDataTypes;
        CmbDataType.SelectedItem = "String";

        CmbScope.ItemsSource = new List<string> { "Public", "Private", "Friend" };
        CmbScope.SelectedItem = "Public";

        if (!string.IsNullOrWhiteSpace(initialPropertyName))
        {
            TxtPropertyName.Text = initialPropertyName;
            TxtBackingField.Text = $"m_{initialPropertyName}";
        }
        else
        {
            TxtPropertyName.Text = "Nombre";
            TxtBackingField.Text = "m_Nombre";
        }

        UpdatePreview();
        TxtPropertyName.Focus();
        TxtPropertyName.SelectAll();
    }

    private void CmbDataType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingInternally) return;

        string typeName = CmbDataType.SelectedItem?.ToString() ?? CmbDataType.Text;
        bool isObject = PropertyBuilderHelper.IsObjectType(typeName);

        _isUpdatingInternally = true;
        if (isObject)
        {
            ChkGenerateLet.IsChecked = false;
            ChkGenerateSet.IsChecked = true;
        }
        else
        {
            ChkGenerateLet.IsChecked = true;
            ChkGenerateSet.IsChecked = false;
        }
        _isUpdatingInternally = false;

        UpdatePreview();
    }

    private void Inputs_Changed(object sender, EventArgs e)
    {
        if (_isUpdatingInternally) return;

        // Auto-actualizar variable de respaldo si coincide con el patrón m_
        if (sender == TxtPropertyName && !string.IsNullOrWhiteSpace(TxtPropertyName.Text))
        {
            string p = TxtPropertyName.Text.Trim();
            if (string.IsNullOrWhiteSpace(TxtBackingField.Text) || TxtBackingField.Text.StartsWith("m_"))
            {
                _isUpdatingInternally = true;
                TxtBackingField.Text = $"m_{p}";
                _isUpdatingInternally = false;
            }
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var options = new PropertyBuilderOptions
        {
            PropertyName = TxtPropertyName.Text.Trim(),
            DataType = CmbDataType.Text.Trim(),
            BackingFieldName = TxtBackingField.Text.Trim(),
            Scope = CmbScope.SelectedItem?.ToString() ?? "Public",
            GenerateGet = ChkGenerateGet.IsChecked == true,
            GenerateLet = ChkGenerateLet.IsChecked == true,
            GenerateSet = ChkGenerateSet.IsChecked == true,
            GenerateBackingField = ChkGenerateBackingField.IsChecked == true,
            IncludeComments = ChkIncludeComments.IsChecked == true
        };

        GeneratedCode = PropertyBuilderHelper.GeneratePropertyCode(options);
        if (TxtPreview != null)
        {
            TxtPreview.Text = GeneratedCode;
        }
    }

    private void BtnInsert_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
        if (string.IsNullOrWhiteSpace(GeneratedCode))
        {
            MessageBox.Show("Debes seleccionar al menos un procedimiento o variable a generar.",
                "Asistente de Propiedades", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
