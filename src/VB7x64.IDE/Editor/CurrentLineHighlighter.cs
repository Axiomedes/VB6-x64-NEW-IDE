using System;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace VB7x64.IDE.Editor;

public class CurrentLineHighlighter : IBackgroundRenderer
{
    private readonly TextEditor _editor;

    public KnownLayer Layer => KnownLayer.Background;

    public Brush LineBrush { get; set; }
    public Pen? BorderPen { get; set; }
    public bool IsEnabled { get; set; } = true;

    public CurrentLineHighlighter(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        LineBrush = new SolidColorBrush(Color.FromArgb(50, 0, 122, 204)); // Sutil azul claro
        LineBrush.Freeze();

        _editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (IsEnabled && _editor.TextArea.TextView != null)
            {
                _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            }
        };
    }

    public void UpdateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        LineBrush = brush;

        // Borde ultra-sutil
        byte borderAlpha = (byte)Math.Min(255, color.A + 40);
        var penBrush = new SolidColorBrush(Color.FromArgb(borderAlpha, color.R, color.G, color.B));
        penBrush.Freeze();
        var pen = new Pen(penBrush, 1);
        pen.Freeze();
        BorderPen = pen;

        _editor.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || _editor.Document == null || _editor.TextArea == null)
            return;

        int caretLine = _editor.TextArea.Caret.Line;
        if (caretLine < 1 || caretLine > _editor.Document.LineCount)
            return;

        var visualLine = textView.GetVisualLine(caretLine);
        if (visualLine == null)
            return;

        double y = visualLine.VisualTop - textView.VerticalOffset;
        double height = visualLine.Height;
        double width = Math.Max(textView.ActualWidth, textView.RenderSize.Width);

        if (width <= 0) width = 2000; // Ancho de reserva si aún no se ha medido el control

        var rect = new Rect(0, y, width, height);
        drawingContext.DrawRectangle(LineBrush, BorderPen, rect);
    }
}
