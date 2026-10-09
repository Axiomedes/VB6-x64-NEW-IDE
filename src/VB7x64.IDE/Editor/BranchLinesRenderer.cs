using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using VB7x64.ProjectModel.Advanced;

namespace VB7x64.IDE.Editor;

/// <summary>
/// Renderiza las guías de estructura de bloques de VB6 (Branch Lines).
/// Dibuja una línea vertical estilizada posicionada un espacio antes de las palabras clave (If, End If, For, etc.)
/// con conectores en ángulo recto (┌ y └) en la apertura y cierre del bloque, y ramificaciones (├──)
/// en sentencias intermedias (Else, ElseIf, Case).
/// </summary>
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

        var penBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)); // Gris sutil, limpio
        penBrush.Freeze();
        var pen = new Pen(penBrush, 1);
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
            if (block.StartLine < block.EndLine)
            {
                if (block.EndLine >= firstVisibleLine && block.StartLine <= lastVisibleLine)
                {
                    DrawSingleBlockGuide(block, textView, drawingContext, firstVisibleLine, lastVisibleLine, charWidth);
                }
            }

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
        int firstVisibleLine,
        int lastVisibleLine,
        double charWidth)
    {
        var startVLine = textView.GetVisualLine(block.StartLine);
        var endVLine = textView.GetVisualLine(block.EndLine);

        // 1. Determinar coordenada X de la palabra clave en pantalla
        double keywordX;
        if (startVLine != null)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, startVLine.VisualLength));
            keywordX = startVLine.GetVisualPosition(col, VisualYPosition.LineMiddle).X;
        }
        else if (endVLine != null)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, endVLine.VisualLength));
            keywordX = endVLine.GetVisualPosition(col, VisualYPosition.LineMiddle).X;
        }
        else if (textView.VisualLines.Count > 0)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, textView.VisualLines[0].VisualLength));
            keywordX = textView.VisualLines[0].GetVisualPosition(col, VisualYPosition.LineMiddle).X;
        }
        else
        {
            keywordX = (block.StartColumn * charWidth) - textView.HorizontalOffset + 2.0;
        }

        // Posición de la guía vertical: un espacio antes de la palabra clave
        double spaceWidth = charWidth > 0 ? charWidth : 8.0;
        double guideX = keywordX - (spaceWidth * 0.75);
        if (guideX < 2.0) guideX = 2.0;

        // Snap a píxel exacto (+0.5) para que las líneas de 1px sean completamente nítidas
        double snapGuideX = Math.Floor(guideX) + 0.5;
        // La línea horizontal termina 2px antes de la primera letra para no tocarla
        double snapKeywordX = Math.Floor(keywordX - 2.0) + 0.5;

        if (snapKeywordX <= snapGuideX)
        {
            snapKeywordX = snapGuideX + 4.0;
        }

        // 2. Límites verticales y esquinas en ángulo recto
        double topY;
        if (startVLine != null)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, startVLine.VisualLength));
            topY = Math.Floor(startVLine.GetVisualPosition(col, VisualYPosition.LineMiddle).Y) + 0.5;

            // Pequeña línea horizontal que forma ángulo recto superior (┌)
            drawingContext.DrawLine(LinePen, new Point(snapGuideX, topY), new Point(snapKeywordX, topY));
        }
        else
        {
            topY = -1.0; // Bloque inicia más arriba de la zona visible
        }

        double bottomY;
        if (endVLine != null)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, endVLine.VisualLength));
            bottomY = Math.Floor(endVLine.GetVisualPosition(col, VisualYPosition.LineMiddle).Y) + 0.5;

            // Pequeña línea horizontal que forma ángulo recto inferior (└)
            drawingContext.DrawLine(LinePen, new Point(snapGuideX, bottomY), new Point(snapKeywordX, bottomY));
        }
        else
        {
            bottomY = textView.ActualHeight + 1.0; // Bloque termina más abajo de la zona visible
        }

        // 3. Ramas intermedias (ElseIf, Else, Case) formando un empalme en T (├──)
        if (block.IntermediateLines.Count > 0)
        {
            foreach (int interLine in block.IntermediateLines)
            {
                if (interLine >= firstVisibleLine && interLine <= lastVisibleLine)
                {
                    var interVLine = textView.GetVisualLine(interLine);
                    if (interVLine != null)
                    {
                        int interCol = Math.Min(block.StartColumn, Math.Max(0, interVLine.VisualLength));
                        double interY = Math.Floor(interVLine.GetVisualPosition(interCol, VisualYPosition.LineMiddle).Y) + 0.5;
                        if (interY >= topY && interY <= bottomY)
                        {
                            drawingContext.DrawLine(LinePen, new Point(snapGuideX, interY), new Point(snapKeywordX, interY));
                        }
                    }
                }
            }
        }

        // 4. Línea vertical principal que conecta la apertura con el cierre
        if (bottomY > topY)
        {
            drawingContext.DrawLine(LinePen, new Point(snapGuideX, topY), new Point(snapGuideX, bottomY));
        }
    }
}
