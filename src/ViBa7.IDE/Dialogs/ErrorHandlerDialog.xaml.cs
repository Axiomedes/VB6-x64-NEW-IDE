using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ViBa7.ProjectModel.ErrorHandling;
using ViBa7.ProjectModel.Formatting;

namespace ViBa7.IDE.Dialogs;

public partial class ErrorHandlerDialog : Window
{
    private readonly string _moduleCode;
    private readonly string _moduleName;
    private readonly string _header;
    private readonly List<ProcedureSortItem> _procedures;
    private readonly ProcedureSortItem? _currentProcedure;
    private readonly List<ErrorScheme> _schemes;
    private bool _isInitializing = true;

    public string ResultingCode { get; private set; } = string.Empty;
    public bool IsRemoval { get; private set; }
    public int AffectedCount { get; private set; }

    public ErrorHandlerDialog(string moduleCode, string moduleName, int caretLineNumber = 1)
    {
        InitializeComponent();

        _moduleCode = moduleCode ?? string.Empty;
        _moduleName = string.IsNullOrWhiteSpace(moduleName) ? "Module1" : moduleName;

        var (header, procs) = ProcedureSorter.ParseProcedures(_moduleCode);
        _header = header;
        _procedures = procs;

        // Identificar procedimiento actual por número de línea
        _currentProcedure = _procedures.FirstOrDefault(p => caretLineNumber >= p.StartLine && caretLineNumber <= p.EndLine)
                            ?? _procedures.FirstOrDefault();

        if (_currentProcedure != null)
        {
            RbCurrentProc.Content = $"Solo el procedimiento '{_currentProcedure.Name}'";
        }
        else
        {
            RbCurrentProc.IsEnabled = false;
            RbAllProcs.IsChecked = true;
        }

        _schemes = ErrorScheme.GetPredefinedSchemes();
        foreach (var s in _schemes)
        {
            CmbSchemes.Items.Add(new ComboBoxItem { Content = s.Name, Tag = s });
        }
        CmbSchemes.SelectedIndex = 0;

        _isInitializing = false;
        LoadSelectedScheme();
        UpdatePreview();
    }

    private void CmbSchemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        LoadSelectedScheme();
        UpdatePreview();
    }

    private void LoadSelectedScheme()
    {
        if (CmbSchemes.SelectedItem is ComboBoxItem cbi && cbi.Tag is ErrorScheme scheme)
        {
            TxtSchemeDesc.Text = scheme.Description;
            TxtHeaderStatement.Text = scheme.HeaderStatement;
            TxtHandlerBlock.Text = scheme.HandlerBlock;
        }
    }

    private void Template_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        UpdatePreview();
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        UpdatePreview();
    }

    private ErrorScheme GetCurrentSchemeFromInputs()
    {
        return new ErrorScheme
        {
            Name = "Personalizado",
            HeaderStatement = TxtHeaderStatement.Text.Trim(),
            HandlerBlock = TxtHandlerBlock.Text.Trim()
        };
    }

    private void UpdatePreview()
    {
        if (TxtPreview == null) return;

        var scheme = GetCurrentSchemeFromInputs();
        bool overwrite = ChkOverwriteExisting?.IsChecked == true;

        if (_procedures.Count == 0)
        {
            TxtPreview.Text = "' No se encontraron procedimientos en el módulo actual.";
            return;
        }

        if (RbCurrentProc?.IsChecked == true && _currentProcedure != null)
        {
            TxtPreview.Text = ErrorHandlingEngine.ApplySchemeToProcedure(
                _currentProcedure.Code, _currentProcedure.Name, _currentProcedure.Kind, _moduleName, scheme, overwrite);
        }
        else
        {
            // Muestra vista previa del primer procedimiento o del módulo
            var sample = _currentProcedure ?? _procedures[0];
            string sampleTransformed = ErrorHandlingEngine.ApplySchemeToProcedure(
                sample.Code, sample.Name, sample.Kind, _moduleName, scheme, overwrite);

            TxtPreview.Text = $"' [Vista previa para {_procedures.Count} procedimientos - Muestra de '{sample.Name}']:\r\n\r\n" + sampleTransformed;
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_procedures.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron procedimientos en el módulo actual.", "Manejo de Errores", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var scheme = GetCurrentSchemeFromInputs();
        bool overwrite = ChkOverwriteExisting?.IsChecked == true;
        IsRemoval = false;

        if (RbCurrentProc?.IsChecked == true && _currentProcedure != null)
        {
            string updatedProc = ErrorHandlingEngine.ApplySchemeToProcedure(
                _currentProcedure.Code, _currentProcedure.Name, _currentProcedure.Kind, _moduleName, scheme, overwrite);

            var updatedList = new List<ProcedureSortItem>();
            foreach (var p in _procedures)
            {
                if (p == _currentProcedure)
                {
                    p.Code = updatedProc;
                }
                updatedList.Add(p);
            }
            ResultingCode = ProcedureSorter.GenerateSortedCode(_header, updatedList, preserveComments: true);
            AffectedCount = 1;
        }
        else
        {
            ResultingCode = ErrorHandlingEngine.ApplyToModule(_moduleCode, _moduleName, scheme, overwrite, out int count);
            AffectedCount = count;
        }

        DialogResult = true;
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_procedures.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron procedimientos en el módulo actual.", "Manejo de Errores", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsRemoval = true;

        if (RbCurrentProc?.IsChecked == true && _currentProcedure != null)
        {
            string cleanedProc = ErrorHandlingEngine.RemoveErrorHandlerFromProcedure(_currentProcedure.Code, _currentProcedure.Kind);
            var updatedList = new List<ProcedureSortItem>();
            foreach (var p in _procedures)
            {
                if (p == _currentProcedure)
                {
                    p.Code = cleanedProc;
                }
                updatedList.Add(p);
            }
            ResultingCode = ProcedureSorter.GenerateSortedCode(_header, updatedList, preserveComments: true);
            AffectedCount = 1;
        }
        else
        {
            ResultingCode = ErrorHandlingEngine.RemoveFromModule(_moduleCode, out int count);
            AffectedCount = count;
        }

        DialogResult = true;
    }
}
