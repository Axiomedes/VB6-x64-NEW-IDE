using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ViBa7.ProjectModel.CodeBuilders;

namespace ViBa7.IDE.CodeBuilders;

public class ParameterItemModel : INotifyPropertyChanged
{
    private string _name = "param";
    private string _passingMechanism = "ByVal";
    private string _dataType = "Long";
    private bool _isOptional;
    private string _defaultValue = "";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string PassingMechanism
    {
        get => _passingMechanism;
        set { _passingMechanism = value; OnPropertyChanged(nameof(PassingMechanism)); }
    }

    public string DataType
    {
        get => _dataType;
        set { _dataType = value; OnPropertyChanged(nameof(DataType)); }
    }

    public bool IsOptional
    {
        get => _isOptional;
        set { _isOptional = value; OnPropertyChanged(nameof(IsOptional)); }
    }

    public string DefaultValue
    {
        get => _defaultValue;
        set { _defaultValue = value; OnPropertyChanged(nameof(DefaultValue)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

public partial class ProcedureBuilderDialog : Window
{
    private static readonly string[] CommonDataTypes =
    [
        "String", "Long", "Integer", "Boolean", "Double", "Single",
        "Currency", "Date", "Byte", "Variant", "Object", "Collection"
    ];

    private readonly ObservableCollection<ParameterItemModel> _parameters = [];
    public string GeneratedCode { get; private set; } = "";

    public ProcedureBuilderDialog(string initialName = "")
    {
        InitializeComponent();

        CmbKind.ItemsSource = new List<string> { "Sub", "Function" };
        CmbKind.SelectedItem = "Sub";

        CmbScope.ItemsSource = new List<string> { "Public", "Private", "Friend" };
        CmbScope.SelectedItem = "Public";

        CmbReturnType.ItemsSource = CommonDataTypes;
        CmbReturnType.SelectedItem = "Boolean";

        ColPass.ItemsSource = new List<string> { "ByVal", "ByRef" };
        ColType.ItemsSource = CommonDataTypes;

        DgParameters.ItemsSource = _parameters;
        _parameters.CollectionChanged += (_, _) => UpdatePreview();

        TxtProcedureName.Text = !string.IsNullOrWhiteSpace(initialName) ? initialName : "ProcesarOperacion";

        UpdateReturnTypeVisibility();
        UpdatePreview();

        TxtProcedureName.Focus();
        TxtProcedureName.SelectAll();
    }

    private void CmbKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateReturnTypeVisibility();
        UpdatePreview();
    }

    private void UpdateReturnTypeVisibility()
    {
        bool isFunction = string.Equals(CmbKind.SelectedItem?.ToString(), "Function", StringComparison.OrdinalIgnoreCase);
        LblReturnType.Visibility = isFunction ? Visibility.Visible : Visibility.Collapsed;
        CmbReturnType.Visibility = isFunction ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Inputs_Changed(object sender, EventArgs e)
    {
        UpdatePreview();
    }

    private void BtnAddParam_Click(object sender, RoutedEventArgs e)
    {
        int count = _parameters.Count + 1;
        var p = new ParameterItemModel
        {
            Name = $"param{count}",
            PassingMechanism = "ByVal",
            DataType = count == 1 ? "Long" : "String"
        };
        p.PropertyChanged += (_, _) => UpdatePreview();
        _parameters.Add(p);
        UpdatePreview();
    }

    private void BtnRemoveParam_Click(object sender, RoutedEventArgs e)
    {
        if (DgParameters.SelectedItem is ParameterItemModel sel)
        {
            _parameters.Remove(sel);
            UpdatePreview();
        }
        else if (_parameters.Count > 0)
        {
            _parameters.RemoveAt(_parameters.Count - 1);
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        bool isFunction = string.Equals(CmbKind.SelectedItem?.ToString(), "Function", StringComparison.OrdinalIgnoreCase);

        var paramList = _parameters.Select(p => new ProcedureParameter
        {
            Name = p.Name,
            PassingMechanism = p.PassingMechanism,
            DataType = p.DataType,
            IsOptional = p.IsOptional,
            DefaultValue = p.DefaultValue
        }).ToList();

        var options = new ProcedureBuilderOptions
        {
            ProcedureName = TxtProcedureName.Text.Trim(),
            Kind = isFunction ? BuilderProcedureKind.Function : BuilderProcedureKind.Sub,
            Scope = CmbScope.SelectedItem?.ToString() ?? "Public",
            ReturnType = CmbReturnType.Text.Trim(),
            Parameters = paramList,
            IncludeErrorHandler = ChkErrorHandler.IsChecked == true,
            IncludeComments = ChkComments.IsChecked == true
        };

        GeneratedCode = ProcedureBuilderHelper.GenerateProcedureCode(options);
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
            MessageBox.Show("No se pudo generar el código del procedimiento.", "Asistente de Procedimientos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
