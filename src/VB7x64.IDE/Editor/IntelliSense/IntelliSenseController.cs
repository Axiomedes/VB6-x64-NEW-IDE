using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using VB7x64.ProjectModel.IntelliSense;

namespace VB7x64.IDE.Editor.IntelliSense;

/// <summary>
/// Controlador principal de IntelliSense para la pestaña del editor.
/// Gestiona la visualización reactiva de Auto List Members (Ctrl+J / .),
/// Parameter Info (Ctrl+I / '(' / ',') y Complete Word (Ctrl+Espacio).
/// </summary>
public class IntelliSenseController
{
    private readonly EditorTabItem _tabItem;
    private readonly TextEditor _editor;
    private CompletionWindow? _completionWindow;
    private InsightWindow? _insightWindow;
    private VbParameterInsightControl? _insightControl;

    public IntelliSenseController(EditorTabItem tabItem)
    {
        _tabItem = tabItem;
        _editor = tabItem.Editor;

        _editor.TextArea.TextEntered += TextArea_TextEntered;
        _editor.TextArea.TextEntering += TextArea_TextEntering;
        _editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        _editor.PreviewKeyDown += Editor_PreviewKeyDown;
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 0) return;
        char c = e.Text[0];

        if (c == '.')
        {
            // Disparar Auto List Members al escribir punto '.'
            ShowCompletionWindow(explicitTrigger: false);
        }
        else if (c == '(' || c == ',')
        {
            // Disparar o actualizar Parameter Info
            ShowParameterInsight(explicitTrigger: false);
        }
        else if (c == ')')
        {
            // Cerrar Parameter Info al cerrar el paréntesis
            CloseParameterInsight();
        }
        else if (char.IsLetter(c) && _completionWindow == null && _tabItem.Settings?.EnableIntelliSense != false && _tabItem.Settings?.AutoListMembers != false)
        {
            // Si el usuario escribe palabras y no hay ventana abierta, podemos verificar si está escribiendo un miembro
            // tras un punto reciente en la misma línea
            int offset = _editor.CaretOffset;
            string docText = _editor.Text;
            var ctx = IntelliSenseContextDetector.DetectContext(docText, offset, _tabItem.DesignModel, explicitTrigger: false);
            if (ctx.Kind == IntelliSenseContextKind.MemberAccess && ctx.Candidates.Count > 0)
            {
                ShowCompletionWindow(explicitTrigger: false);
            }
        }
    }

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            char c = e.Text[0];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                // Al pulsar un carácter delimitador (como espacio, punto, paréntesis o salto de línea),
                // completar la selección actual si hay coincidencia
                if (c == '\n' || c == '\r' || c == '\t' || c == '.' || c == '(' || c == ' ')
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_insightWindow != null)
        {
            // Actualizar el índice del parámetro activo al mover el cursor dentro de la llamada
            UpdateParameterInsight();
        }
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. Ctrl + J: Listar Miembros (Auto List Members)
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.J)
        {
            ShowCompletionWindow(explicitTrigger: true);
            e.Handled = true;
            return;
        }

        // 2. Ctrl + I: Información Rápida de Parámetros (Quick Info / Parameter Info)
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
        {
            ShowParameterInsight(explicitTrigger: true);
            e.Handled = true;
            return;
        }

        // 3. Ctrl + Espacio: Completar Palabra (Complete Word)
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Space)
        {
            ShowCompletionWindow(explicitTrigger: true);
            e.Handled = true;
            return;
        }

        // 4. Escape: Cancelar ventana activa de IntelliSense
        if (e.Key == Key.Escape)
        {
            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
                e.Handled = true;
                return;
            }

            if (_insightWindow != null)
            {
                CloseParameterInsight();
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    /// Despliega la ventana de autocompletado (Auto List Members).
    /// </summary>
    public void ShowCompletionWindow(bool explicitTrigger)
    {
        int offset = _editor.CaretOffset;
        string docText = _editor.Text;

        var context = IntelliSenseContextDetector.DetectContext(
            docText,
            offset,
            _tabItem.DesignModel,
            explicitTrigger: explicitTrigger);

        if (context.Kind == IntelliSenseContextKind.None || context.Candidates.Count == 0)
        {
            return;
        }

        _completionWindow?.Close();

        _completionWindow = new CompletionWindow(_editor.TextArea)
        {
            Width = 260,
            MaxHeight = 220
        };

        if (context.ReplaceLength > 0 && context.ReplaceStartPosition >= 0)
        {
            _completionWindow.StartOffset = context.ReplaceStartPosition;
            _completionWindow.EndOffset = context.ReplaceStartPosition + context.ReplaceLength;
        }

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var sym in context.Candidates)
        {
            data.Add(new VbCompletionData(sym, context));
        }

        _completionWindow.Closed += (s, e) => _completionWindow = null;
        _completionWindow.Show();
    }

    /// <summary>
    /// Despliega o actualiza la ventana flotante de información de parámetros (Parameter Info).
    /// </summary>
    public void ShowParameterInsight(bool explicitTrigger)
    {
        int offset = _editor.CaretOffset;
        string docText = _editor.Text;

        var context = IntelliSenseContextDetector.DetectContext(
            docText,
            offset,
            _tabItem.DesignModel,
            explicitTrigger: explicitTrigger,
            isParameterInfoTrigger: true);

        if (context.Kind == IntelliSenseContextKind.ParameterInfo && context.InvokedSymbol != null)
        {
            if (_insightWindow == null)
            {
                _insightControl = new VbParameterInsightControl();
                _insightWindow = new InsightWindow(_editor.TextArea)
                {
                    Content = _insightControl
                };
                _insightWindow.Closed += (s, e) =>
                {
                    _insightWindow = null;
                    _insightControl = null;
                };
                _insightWindow.Show();
            }

            _insightControl?.Update(context);
        }
        else if (!explicitTrigger)
        {
            CloseParameterInsight();
        }
    }

    /// <summary>
    /// Actualiza el resaltado del parámetro en la ventana flotante activa.
    /// </summary>
    public void UpdateParameterInsight()
    {
        int offset = _editor.CaretOffset;
        string docText = _editor.Text;

        var context = IntelliSenseContextDetector.DetectContext(
            docText,
            offset,
            _tabItem.DesignModel,
            explicitTrigger: false,
            isParameterInfoTrigger: true);

        if (context.Kind == IntelliSenseContextKind.ParameterInfo && context.InvokedSymbol != null)
        {
            _insightControl?.Update(context);
        }
        else
        {
            CloseParameterInsight();
        }
    }

    /// <summary>
    /// Cierra la ventana de información de parámetros si está abierta.
    /// </summary>
    public void CloseParameterInsight()
    {
        if (_insightWindow != null)
        {
            _insightWindow.Close();
            _insightWindow = null;
            _insightControl = null;
        }
    }
}
