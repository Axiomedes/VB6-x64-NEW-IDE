using System;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace VB7x64.IDE.Editor;

/// <summary>
/// Margen visual situado entre los números de línea y el área de texto (TextView).
/// Proporciona espacio visual limpio y dibuja las guías de bloque para procedimientos raíz
/// (Sub, Function, Property) en columna 0 con esquinas en ángulo recto (┌ y └).
/// </summary>
public class BranchLinesMargin : AbstractMargin
{
    private readonly TextEditor _editor;
    private readonly BranchLinesRenderer _renderer;
    public const double DesiredMarginWidth = 16.0;

    public BranchLinesMargin(TextEditor editor, BranchLinesRenderer renderer)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _renderer.SetMargin(this);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(DesiredMarginWidth, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Fondo del margen alineado con el editor
        var bgBrush = _editor.Background ?? Brushes.White;
        drawingContext.DrawRectangle(bgBrush, null, new Rect(0, 0, DesiredMarginWidth, RenderSize.Height));

        if (!_renderer.IsEnabled || TextView == null || TextView.VisualLines.Count == 0)
            return;

        var blocks = _renderer.Blocks;
        if (blocks.Count == 0)
            return;

        int firstVisibleLine = TextView.VisualLines[0].FirstDocumentLine.LineNumber;
        int lastVisibleLine = TextView.VisualLines[^1].LastDocumentLine.LineNumber;

        double guideX = 7.5; // Centro del margen (snapped a +0.5)
        double connectX = DesiredMarginWidth; // Conexión hacia el borde de TextView

        foreach (var block in blocks)
        {
            // Solo dibujamos procedimientos raíz en columna 0
            if (block.StartColumn > 0)
                continue;

            if (block.EndLine < firstVisibleLine || block.StartLine > lastVisibleLine)
                continue;

            var startVLine = TextView.GetVisualLine(block.StartLine);
            var endVLine = TextView.GetVisualLine(block.EndLine);

            double topY;
            if (startVLine != null)
            {
                topY = Math.Floor(startVLine.VisualTop - TextView.VerticalOffset + (startVLine.Height / 2.0)) + 0.5;
                // Esquina superior en ángulo recto (┌)
                drawingContext.DrawLine(_renderer.LinePen, new Point(guideX, topY), new Point(connectX, topY));
            }
            else
            {
                topY = -1.0;
            }

            double bottomY;
            if (endVLine != null)
            {
                bottomY = Math.Floor(endVLine.VisualTop - TextView.VerticalOffset + (endVLine.Height / 2.0)) + 0.5;
                // Esquina inferior en ángulo recto (└)
                drawingContext.DrawLine(_renderer.LinePen, new Point(guideX, bottomY), new Point(connectX, bottomY));
            }
            else
            {
                bottomY = TextView.ActualHeight + 1.0;
            }

            // Línea vertical continua que conecta apertura y cierre
            if (bottomY > topY)
            {
                drawingContext.DrawLine(_renderer.LinePen, new Point(guideX, topY), new Point(guideX, bottomY));
            }
        }
    }
}
