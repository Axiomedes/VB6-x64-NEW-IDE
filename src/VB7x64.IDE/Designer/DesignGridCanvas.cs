using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VB7x64.IDE.Designer;

/// <summary>
/// Lienzo de diseño con soporte para matriz de puntos (Grid Dots) y ajuste magnético (Snap to Grid).
/// </summary>
public class DesignGridCanvas : Canvas
{
    private double _gridSize = 8.0; // 8 píxeles = 120 twips
    private bool _showGrid = true;
    private bool _snapToGrid = true;
    private Color _gridDotColor = Color.FromRgb(160, 160, 160);

    public double GridSize
    {
        get => _gridSize;
        set
        {
            if (Math.Abs(_gridSize - value) > 0.01 && value >= 2.0)
            {
                _gridSize = value;
                UpdateGridBackground();
            }
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (_showGrid != value)
            {
                _showGrid = value;
                UpdateGridBackground();
            }
        }
    }

    public bool SnapToGrid
    {
        get => _snapToGrid;
        set => _snapToGrid = value;
    }

    public Color GridDotColor
    {
        get => _gridDotColor;
        set
        {
            if (_gridDotColor != value)
            {
                _gridDotColor = value;
                UpdateGridBackground();
            }
        }
    }

    public DesignGridCanvas()
    {
        ClipToBounds = true;
        UpdateGridBackground();
    }

    public double SnapValue(double value)
    {
        if (!_snapToGrid || _gridSize <= 0) return value;
        return GridSnapHelper.Snap(value, _gridSize);
    }

    public Point SnapPoint(Point pt)
    {
        if (!_snapToGrid || _gridSize <= 0) return pt;
        var (x, y) = GridSnapHelper.SnapPoint(pt.X, pt.Y, _gridSize);
        return new Point(x, y);
    }

    public void UpdateGridBackground()
    {
        if (!_showGrid || _gridSize < 2.0)
        {
            Background = Brushes.Transparent;
            return;
        }

        // Crear una celda en mosaico que dibuja un punto de 1x1 píxel en el origen
        var group = new DrawingGroup();
        var dotGeometry = new RectangleGeometry(new Rect(0, 0, 1, 1));
        var dotBrush = new SolidColorBrush(_gridDotColor);
        dotBrush.Freeze();

        group.Children.Add(new GeometryDrawing(dotBrush, null, dotGeometry));

        var tileBrush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, _gridSize, _gridSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        tileBrush.Freeze();

        Background = tileBrush;
    }
}
