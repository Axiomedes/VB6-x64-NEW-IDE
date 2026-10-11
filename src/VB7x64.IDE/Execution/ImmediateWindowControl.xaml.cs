using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VB7x64.ProjectModel.Execution;

namespace VB7x64.IDE.Execution;

/// <summary>
/// Control de usuario para la Ventana Inmediata (Immediate Window) de VB6.
/// Permite evaluar expresiones interactivamente (? expr), ejecutar sentencias directas
/// y visualizar la salida generada por llamadas a Debug.Print en tiempo real.
/// </summary>
public partial class ImmediateWindowControl : UserControl
{
    private LiveExecutionHost? _host;
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;

    public LiveExecutionHost? Host => _host;

    public ImmediateWindowControl()
    {
        InitializeComponent();
    }

    public void AttachHost(LiveExecutionHost host)
    {
        if (_host != null)
        {
            _host.Context.OnDebugPrint -= HandleDebugPrint;
        }

        _host = host;

        if (_host != null)
        {
            _host.Context.OnDebugPrint += HandleDebugPrint;
        }
    }

    public void DetachHost()
    {
        if (_host != null)
        {
            _host.Context.OnDebugPrint -= HandleDebugPrint;
            _host = null;
        }
    }

    private void HandleDebugPrint(string message)
    {
        Dispatcher.Invoke(() =>
        {
            AppendOutput(message);
        });
    }

    public void AppendOutput(string text)
    {
        TxtHistory.AppendText(text + Environment.NewLine);
        TxtHistory.ScrollToEnd();
    }

    private void ExecuteCurrentInput()
    {
        string raw = TxtInput.Text.Trim();
        if (string.IsNullOrEmpty(raw)) return;

        _commandHistory.Add(raw);
        _historyIndex = _commandHistory.Count;

        string commandToRun = raw;
        if (!commandToRun.StartsWith('?') && !commandToRun.StartsWith("Print ", StringComparison.OrdinalIgnoreCase) && !commandToRun.Contains('='))
        {
            // Si el usuario escribió una expresión sin '?' (ej. `Now` o `2 + 2`), asumimos evaluación con `?`
            commandToRun = "?" + commandToRun;
        }

        AppendOutput("? " + raw.TrimStart('?').Trim());

        try
        {
            if (_host == null)
            {
                // Si no hay ejecución activa, crear un host efímero para evaluar la expresión
                var tempHost = new LiveExecutionHost();
                var result = tempHost.EvaluateImmediate(commandToRun);
                if (result.VarType != VbVarType.Empty)
                {
                    AppendOutput(FormatResult(result));
                }
            }
            else
            {
                var result = _host.EvaluateImmediate(commandToRun);
                if (result.VarType != VbVarType.Empty)
                {
                    AppendOutput(FormatResult(result));
                }
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"[Error] {ex.Message}");
        }

        TxtInput.Clear();
        TxtInput.Focus();
    }

    private static string FormatResult(VbValue val)
    {
        return val.VarType switch
        {
            VbVarType.String => $"\"{val.AsString()}\"",
            VbVarType.Boolean => val.AsBoolean() ? "True" : "False",
            VbVarType.Date => $"#{val.AsDate():dd/MM/yyyy HH:mm:ss}#",
            VbVarType.Null => "Null",
            VbVarType.Empty => "Empty",
            _ => val.AsString()
        };
    }

    private void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ExecuteCurrentInput();
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (_commandHistory.Count > 0 && _historyIndex > 0)
            {
                _historyIndex--;
                TxtInput.Text = _commandHistory[_historyIndex];
                TxtInput.CaretIndex = TxtInput.Text.Length;
            }
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            if (_historyIndex < _commandHistory.Count - 1)
            {
                _historyIndex++;
                TxtInput.Text = _commandHistory[_historyIndex];
                TxtInput.CaretIndex = TxtInput.Text.Length;
            }
            else
            {
                _historyIndex = _commandHistory.Count;
                TxtInput.Clear();
            }
        }
    }

    private void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        ExecuteCurrentInput();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtHistory.Clear();
    }
}
