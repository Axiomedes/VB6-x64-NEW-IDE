using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VB7x64.ProjectModel.ErrorHandling;
using VB7x64.ProjectModel.Formatting;

namespace VB7x64.IDE.Dialogs;

public partial class TracingDialog : Window
{
    private readonly string _moduleCode;
    private readonly string _moduleName;
    private readonly string _header;
    private readonly List<ProcedureSortItem> _procedures;
    private readonly ProcedureSortItem? _currentProcedure;
    private bool _isInitializing = true;

    public string ResultingCode { get; private set; } = string.Empty;
    public bool IsRemoval { get; private set; }
    public int AffectedCount { get; private set; }

    public TracingDialog(string moduleCode, string moduleName, int caretLineNumber = 1)
    {
        InitializeComponent();

        _moduleCode = moduleCode ?? string.Empty;
        _moduleName = string.IsNullOrWhiteSpace(moduleName) ? "Module1" : moduleName;

        var (header, procs) = ProcedureSorter.ParseProcedures(_moduleCode);
        _header = header;
        _procedures = procs;

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

        var defaultOpts = new TracingOptions();
        TxtEntryTemplate.Text = defaultOpts.EntryTemplate;
        TxtExitTemplate.Text = defaultOpts.ExitTemplate;
        TxtTraceTag.Text = defaultOpts.TraceTag;
        ChkInjectExit.IsChecked = defaultOpts.InjectExitTrace;

        _isInitializing = false;
        UpdatePreview();
    }

    private TracingOptions GetCurrentOptions()
    {
        return new TracingOptions
        {
            EntryTemplate = TxtEntryTemplate.Text.Trim(),
            ExitTemplate = TxtExitTemplate.Text.Trim(),
            TraceTag = string.IsNullOrWhiteSpace(TxtTraceTag.Text) ? TracingOptions.DefaultTag : TxtTraceTag.Text.Trim(),
            InjectExitTrace = ChkInjectExit.IsChecked == true
        };
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

    private void UpdatePreview()
    {
        if (TxtPreview == null) return;

        if (_procedures.Count == 0)
        {
            TxtPreview.Text = "' No se encontraron procedimientos en el módulo actual.";
            return;
        }

        var opts = GetCurrentOptions();

        if (RbCurrentProc?.IsChecked == true && _currentProcedure != null)
        {
            TxtPreview.Text = TracingEngine.InjectTracesIntoProcedure(
                _currentProcedure.Code, _currentProcedure.Name, _currentProcedure.Kind, _moduleName, opts);
        }
        else
        {
            var sample = _currentProcedure ?? _procedures[0];
            string sampleInjected = TracingEngine.InjectTracesIntoProcedure(
                sample.Code, sample.Name, sample.Kind, _moduleName, opts);

            TxtPreview.Text = $"' [Vista previa de inyección para {_procedures.Count} procedimientos - Muestra de '{sample.Name}']:\r\n\r\n" + sampleInjected;
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_procedures.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron procedimientos en el módulo actual.", "Inyección de Trazas", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var opts = GetCurrentOptions();
        IsRemoval = false;

        if (RbCurrentProc?.IsChecked == true && _currentProcedure != null)
        {
            string updated = TracingEngine.InjectTracesIntoProcedure(
                _currentProcedure.Code, _currentProcedure.Name, _currentProcedure.Kind, _moduleName, opts);

            var updatedList = new List<ProcedureSortItem>();
            foreach (var p in _procedures)
            {
                if (p == _currentProcedure)
                {
                    p.Code = updated;
                }
                updatedList.Add(p);
            }
            ResultingCode = ProcedureSorter.GenerateSortedCode(_header, updatedList, preserveComments: true);
            AffectedCount = 1;
        }
        else
        {
            ResultingCode = TracingEngine.InjectTracesIntoModule(_moduleCode, _moduleName, opts, out int count);
            AffectedCount = count;
        }

        DialogResult = true;
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_procedures.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron procedimientos en el módulo actual.", "Retiro de Trazas", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var opts = GetCurrentOptions();
        IsRemoval = true;

        if (RbCurrentProc?.IsChecked == true && _currentProcedure != null)
        {
            string cleaned = TracingEngine.RemoveTracesFromProcedure(_currentProcedure.Code, opts.TraceTag);
            var updatedList = new List<ProcedureSortItem>();
            foreach (var p in _procedures)
            {
                if (p == _currentProcedure)
                {
                    p.Code = cleaned;
                }
                updatedList.Add(p);
            }
            ResultingCode = ProcedureSorter.GenerateSortedCode(_header, updatedList, preserveComments: true);
            AffectedCount = 1;
        }
        else
        {
            ResultingCode = TracingEngine.RemoveTracesFromModule(_moduleCode, opts.TraceTag, out int count);
            AffectedCount = count;
        }

        DialogResult = true;
    }
}
