using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using VB7x64.ProjectModel.Navigation;

namespace VB7x64.IDE.Editor;

public class BookmarkMargin : AbstractMargin
{
    private readonly TextEditor _editor;
    private readonly string _filePath;

    private static readonly Brush BookmarkBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
    private static readonly Pen BookmarkPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 90, 160)), 1);

    static BookmarkMargin()
    {
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

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(18, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Fondo sutil del margen
        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            null,
            new Rect(0, 0, RenderSize.Width, RenderSize.Height));

        var textView = TextView;
        if (textView == null || !textView.VisualLinesValid)
            return;

        var bookmarks = BookmarkManager.Instance.GetBookmarks(_filePath);
        if (bookmarks.Count == 0)
            return;

        foreach (var visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (BookmarkManager.Instance.HasBookmark(_filePath, lineNumber))
            {
                double y = visualLine.VisualTop - textView.VerticalOffset;
                double height = Math.Min(visualLine.Height, 16);
                double size = 12;
                double x = (RenderSize.Width - size) / 2;
                double yOffset = y + (visualLine.Height - size) / 2;

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
                figure.Segments.Add(new LineSegment(new Point(x + size / 2, yOffset + size - 3), false));
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
