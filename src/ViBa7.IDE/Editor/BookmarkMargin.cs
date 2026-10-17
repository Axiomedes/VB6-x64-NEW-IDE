using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ViBa7.ProjectModel.Navigation;

namespace ViBa7.IDE.Editor;

/// <summary>
/// Margen interactivo de marcadores (Bookmark Margin / Glyph Gutter) ubicado
/// en el extremo izquierdo del editor de código.
/// Sincronizado en tiempo real con el desplazamiento vertical (scroll) y redibujado
/// dinámico de líneas visuales de AvalonEdit.
/// </summary>
public class BookmarkMargin : AbstractMargin
{
    private readonly TextEditor _editor;
    private readonly string _filePath;

    private static readonly Brush GutterBackground = new SolidColorBrush(Color.FromRgb(246, 246, 246));
    private static readonly Pen GutterSeparator = new Pen(new SolidColorBrush(Color.FromRgb(226, 226, 226)), 1);
    private static readonly Brush BookmarkBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
    private static readonly Pen BookmarkPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 90, 160)), 1);

    static BookmarkMargin()
    {
        GutterBackground.Freeze();
        GutterSeparator.Freeze();
        BookmarkBrush.Freeze();
        BookmarkPen.Freeze();
    }

    public BookmarkMargin(TextEditor editor, string filePath)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _filePath = filePath ?? string.Empty;

        Cursor = Cursors.Hand;
        ToolTip = "Clic para agregar o quitar marcador";

        BookmarkManager.Instance.BookmarksChanged += OnBookmarksChanged;
    }

    private void OnBookmarksChanged()
    {
        Dispatcher.InvokeAsync(InvalidateVisual);
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.VisualLinesChanged -= TextView_Updated;
            oldTextView.ScrollOffsetChanged -= TextView_Updated;
        }
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += TextView_Updated;
            newTextView.ScrollOffsetChanged += TextView_Updated;
        }
        InvalidateVisual();
    }

    private void TextView_Updated(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(20, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // 1. Fondo sutil del margen de marcadores (Gutter)
        drawingContext.DrawRectangle(
            GutterBackground,
            null,
            new Rect(0, 0, RenderSize.Width, RenderSize.Height));

        // 2. Línea sutil separadora derecha
        drawingContext.DrawLine(
            GutterSeparator,
            new Point(RenderSize.Width - 0.5, 0),
            new Point(RenderSize.Width - 0.5, RenderSize.Height));

        var textView = TextView;
        if (textView == null || !textView.VisualLinesValid)
            return;

        var bookmarks = BookmarkManager.Instance.GetBookmarks(_filePath);
        if (bookmarks.Count == 0)
            return;

        // 3. Dibujar marcadores alineados con cada línea visible y su desplazamiento vertical actual
        foreach (var visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (BookmarkManager.Instance.HasBookmark(_filePath, lineNumber))
            {
                double y = visualLine.VisualTop - textView.VerticalOffset;
                double size = 12;
                double x = Math.Round((RenderSize.Width - size) / 2.0);
                double yOffset = Math.Round(y + (visualLine.Height - size) / 2.0);

                // Dibujar forma de cinta de marcador (Bookmark ribbon badge)
                var pathGeom = new PathGeometry();
                var figure = new PathFigure
                {
                    StartPoint = new Point(x, yOffset),
                    IsClosed = true,
                    IsFilled = true
                };

                figure.Segments.Add(new LineSegment(new Point(x + size, yOffset), false));
                figure.Segments.Add(new LineSegment(new Point(x + size, yOffset + size), false));
                figure.Segments.Add(new LineSegment(new Point(x + size / 2.0, yOffset + size - 3), false));
                figure.Segments.Add(new LineSegment(new Point(x, yOffset + size), false));
                pathGeom.Figures.Add(figure);

                drawingContext.DrawGeometry(BookmarkBrush, BookmarkPen, pathGeom);
            }
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton == MouseButton.Left && TextView != null)
        {
            var pos = e.GetPosition(this);
            var visualLine = TextView.GetVisualLineFromVisualTop(pos.Y + TextView.VerticalOffset);
            if (visualLine != null)
            {
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;
                BookmarkManager.Instance.ToggleBookmark(_filePath, lineNumber);
                e.Handled = true;
                InvalidateVisual();
            }
        }
    }
}
