using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using VB7x64.ProjectModel.Advanced;

namespace VB7x64.IDE.Editor;

public class BranchLinesRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private List<VB6Block> _blocks = [];
    private bool _needsParse = true;

    public KnownLayer Layer => KnownLayer.Background;

    public Pen LinePen { get; set; }
    public bool IsEnabled { get; set; } = true;

    public BranchLinesRenderer(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));

        var penBrush = new SolidColorBrush(Color.FromRgb(208, 208, 208));
        penBrush.Freeze();
        var pen = new Pen(penBrush, 1);
        pen.DashStyle = DashStyles.Dot; // Línea punteada sutil
        pen.Freeze();
        LinePen = pen;

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
        var penBrush = new SolidColorBrush(color);
        penBrush.Freeze();
        var pen = new Pen(penBrush, 1);
        pen.DashStyle = DashStyles.Dot;
        pen.Freeze();
        LinePen = pen;

        _editor.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);
    }

    public void InvalidateBlocks()
    {
        _needsParse = true;
        _editor.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || _editor.Document == null || _editor.TextArea == null)
            return;

        if (textView.VisualLines.Count == 0)
            return;

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

        if (_blocks.Count == 0)
            return;

        int firstVisibleLine = textView.VisualLines[0].FirstDocumentLine.LineNumber;
        int lastVisibleLine = textView.VisualLines[^1].LastDocumentLine.LineNumber;

        double charWidth = textView.WideSpaceWidth > 0 ? textView.WideSpaceWidth : 8.0;

        // Renderizar bloques jerárquicos
        RenderBlockList(_blocks, textView, drawingContext, firstVisibleLine, lastVisibleLine, charWidth);
    }

    private void RenderBlockList(
        List<VB6Block> blocks,
        TextView textView,
        DrawingContext drawingContext,
        int firstVisibleLine,
        int lastVisibleLine,
        double charWidth)
    {
        foreach (var block in blocks)
        {
            // Ignorar bloques de una sola línea
            if (block.StartLine < block.EndLine)
            {
                // Solo si el bloque solapa el rango visible en pantalla
                if (block.EndLine >= firstVisibleLine && block.StartLine <= lastVisibleLine)
                {
                    DrawSingleBlockGuide(block, textView, drawingContext, charWidth);
                }
            }

            // Procesar bloques hijos
            if (block.Children.Count > 0)
            {
                RenderBlockList(block.Children, textView, drawingContext, firstVisibleLine, lastVisibleLine, charWidth);
            }
        }
    }

    private void DrawSingleBlockGuide(
        VB6Block block,
        TextView textView,
        DrawingContext drawingContext,
        double charWidth)
    {
        double x = (block.StartColumn * charWidth) + 3.0;

        double topY;
        var startVLine = textView.GetVisualLine(block.StartLine);
        if (startVLine != null)
        {
            topY = startVLine.VisualTop - textView.VerticalOffset + startVLine.Height;
            // Pequeño conector horizontal en el inicio
            drawingContext.DrawLine(LinePen, new Point(x, topY - (startVLine.Height / 2)), new Point(x + 5.0, topY - (startVLine.Height / 2)));
        }
        else
        {
            topY = 0; // Inicio fuera de pantalla arriba
        }

        double bottomY;
        var endVLine = textView.GetVisualLine(block.EndLine);
        if (endVLine != null)
        {
            bottomY = endVLine.VisualTop - textView.VerticalOffset + (endVLine.Height / 2);
            // Pequeño conector horizontal en el cierre
            drawingContext.DrawLine(LinePen, new Point(x, bottomY), new Point(x + 5.0, bottomY));
        }
        else
        {
            bottomY = textView.ActualHeight; // Cierre fuera de pantalla abajo
        }

        if (bottomY > topY)
        {
            // Línea vertical que une apertura y cierre
            drawingContext.DrawLine(LinePen, new Point(x, topY), new Point(x, bottomY));
        }
    }
}
