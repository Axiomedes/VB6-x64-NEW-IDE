using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using VB7x64.ProjectModel.Advanced;

namespace VB7x64.IDE.Editor;

/// <summary>
/// Renderiza detalles sutiles de fin de bloque (Inlay Hints / End-of-Line Details) a la derecha
/// de sentencias de cierre como 'End Sub', 'End If', 'End Select', 'Next', 'Loop', etc.
/// para facilitar la orientación cuando el bloque es extenso.
/// </summary>
public class EndOfLineDetailsRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private List<VB6Block> _blocks = [];
    private bool _needsParse = true;
    private Brush _hintBrush;

    public KnownLayer Layer => KnownLayer.Background;

    public bool IsEnabled { get; set; } = true;
    public int MinLines { get; set; } = 5;

    public EndOfLineDetailsRenderer(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));

        var brush = new SolidColorBrush(Color.FromRgb(138, 138, 138));
        brush.Freeze();
        _hintBrush = brush;

        _editor.TextChanged += (_, _) =>
        {
            _needsParse = true;
            if (IsEnabled && _editor.TextArea?.TextView != null)
            {
                _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            }
        };
    }

    public void UpdateColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _hintBrush = brush;

        _editor.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);
    }

    public void InvalidateBlocks()
    {
        _needsParse = true;
        _editor.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);
    }

    private void EnsureBlocks()
    {
        if (_needsParse)
        {
            try
            {
                _blocks = VB6BlockParser.ParseBlocks(_editor.Text);
            }
            catch
            {
                _blocks = [];
            }
            _needsParse = false;
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || _editor.Document == null || _editor.TextArea == null)
            return;

        if (textView.VisualLines.Count == 0)
            return;

        EnsureBlocks();

        if (_blocks.Count == 0)
            return;

        int firstVisibleLine = textView.VisualLines[0].FirstDocumentLine.LineNumber;
        int lastVisibleLine = textView.VisualLines[^1].LastDocumentLine.LineNumber;

        var typeface = new Typeface(_editor.FontFamily, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        double emSize = Math.Max(9.5, _editor.FontSize - 1.5);
        double pixelsPerDip = VisualTreeHelper.GetDpi(_editor).PixelsPerDip;

        RenderBlockHints(_blocks, textView, drawingContext, firstVisibleLine, lastVisibleLine, typeface, emSize, pixelsPerDip);
    }

    private void RenderBlockHints(
        List<VB6Block> blocks,
        TextView textView,
        DrawingContext drawingContext,
        int firstVisibleLine,
        int lastVisibleLine,
        Typeface typeface,
        double emSize,
        double pixelsPerDip)
    {
        foreach (var block in blocks)
        {
            int blockLines = block.EndLine - block.StartLine + 1;
            if (blockLines >= MinLines && block.EndLine >= firstVisibleLine && block.EndLine <= lastVisibleLine)
            {
                var endVLine = textView.GetVisualLine(block.EndLine);
                if (endVLine != null)
                {
                    string hintText = BuildHintText(block);
                    if (!string.IsNullOrEmpty(hintText))
                    {
                        var formattedText = new FormattedText(
                            hintText,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            emSize,
                            _hintBrush,
                            pixelsPerDip);

                        double lineEndX = endVLine.GetVisualPosition(endVLine.VisualLength, VisualYPosition.LineMiddle).X - textView.HorizontalOffset;
                        double textX = lineEndX + 16.0;
                        double textY = endVLine.VisualTop - textView.VerticalOffset + ((endVLine.Height - formattedText.Height) / 2.0);

                        drawingContext.DrawText(formattedText, new Point(textX, textY));
                    }
                }
            }

            if (block.Children.Count > 0)
            {
                RenderBlockHints(block.Children, textView, drawingContext, firstVisibleLine, lastVisibleLine, typeface, emSize, pixelsPerDip);
            }
        }
    }

    private static string BuildHintText(VB6Block block)
    {
        string header = block.Header.Trim();
        if (string.IsNullOrEmpty(header))
            return string.Empty;

        // Limpiar terminaciones como ' Then' para If
        if (header.EndsWith(" Then", StringComparison.OrdinalIgnoreCase))
        {
            header = header[..^5].TrimEnd();
        }

        // Acortar si es excesivamente largo
        if (header.Length > 40)
        {
            header = header[..37] + "...";
        }

        return $" ' [{header}]";
    }
}
