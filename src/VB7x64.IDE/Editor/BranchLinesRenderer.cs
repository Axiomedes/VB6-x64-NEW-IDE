using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using VB7x64.ProjectModel.Advanced;

namespace VB7x64.IDE.Editor;

/// <summary>
/// Renderiza las guías de estructura de bloques de VB6 (Branch Lines) dentro de TextView.
/// Dibuja una línea vertical estilizada posicionada un espacio antes de las palabras clave (Sub, End Sub, If, End If, For, etc.)
/// con conectores en ángulo recto (┌ y └) en la apertura y cierre del bloque, y ramificaciones (├──)
/// en sentencias intermedias (Else, ElseIf, Case).
/// Todas las líneas (tanto de procedimientos principales como de bloques internos) se dibujan dinámicamente
/// desplazándose coordinadas en sincronía con el texto tanto vertical como horizontalmente.
/// </summary>
public class BranchLinesRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private List<VB6Block> _blocks = [];
    private bool _needsParse = true;

    public KnownLayer Layer => KnownLayer.Background;

    public Pen LinePen { get; set; }
    public bool IsEnabled { get; set; } = true;

    public List<VB6Block> Blocks
    {
        get
        {
            EnsureBlocks();
            return _blocks;
        }
    }

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

        double charWidth = textView.WideSpaceWidth > 0 ? textView.WideSpaceWidth : 8.0;

        // Clip de protección para permitir dibujar en el margen izquierdo (X hasta -14) sin invadir los números de línea
        double clipWidth = Math.Max(0, (textView.ActualWidth > 0 ? textView.ActualWidth : 2000) + 16);
        double clipHeight = Math.Max(0, textView.ActualHeight > 0 ? textView.ActualHeight : 2000);
        var clipRect = new Rect(-14, 0, clipWidth, clipHeight);
        drawingContext.PushClip(new RectangleGeometry(clipRect));

        try
        {
            RenderBlockHierarchy(_blocks, textView, drawingContext, firstVisibleLine, lastVisibleLine, charWidth);
        }
        finally
        {
            drawingContext.Pop();
        }
    }

    private void RenderBlockHierarchy(
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
                RenderBlockHierarchy(block.Children, textView, drawingContext, firstVisibleLine, lastVisibleLine, charWidth);
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

        // Si el bloque está colapsado (plegado en la misma línea visual), no dibujar guía vertical
        if (startVLine != null && startVLine.LastDocumentLine.LineNumber >= block.EndLine)
        {
            return;
        }

        // 1. Determinar coordenada X de la palabra clave en pantalla (restando HorizontalOffset para soportar desplazamiento horizontal)
        double keywordX;
        if (startVLine != null)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, startVLine.VisualLength));
            keywordX = startVLine.GetVisualPosition(col, VisualYPosition.LineMiddle).X - textView.HorizontalOffset;
        }
        else if (endVLine != null)
        {
            int col = Math.Min(block.StartColumn, Math.Max(0, endVLine.VisualLength));
            keywordX = endVLine.GetVisualPosition(col, VisualYPosition.LineMiddle).X - textView.HorizontalOffset;
        }
        else
        {
            keywordX = (block.StartColumn * charWidth) - textView.HorizontalOffset;
        }

        // Posición de la guía vertical: exactamente un ancho de espacio antes de la palabra clave
        double spaceWidth = charWidth > 0 ? charWidth : 8.0;
        double guideX = keywordX - spaceWidth;

        // Snap a píxel exacto (+0.5) para nitidez absoluta
        double snapGuideX = Math.Floor(guideX) + 0.5;
        // La línea horizontal termina 2px antes de la primera letra para no tocarla
        double snapKeywordX = Math.Floor(keywordX - 2.0) + 0.5;

        if (snapKeywordX <= snapGuideX)
        {
            snapKeywordX = snapGuideX + 4.0;
        }

        // 2. Límites verticales y esquinas en ángulo recto calculados con VisualTop y VerticalOffset
        double topY;
        if (startVLine != null)
        {
            topY = Math.Floor(startVLine.VisualTop - textView.VerticalOffset + (startVLine.Height / 2.0)) + 0.5;
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
            bottomY = Math.Floor(endVLine.VisualTop - textView.VerticalOffset + (endVLine.Height / 2.0)) + 0.5;
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
                        double interY = Math.Floor(interVLine.VisualTop - textView.VerticalOffset + (interVLine.Height / 2.0)) + 0.5;
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
