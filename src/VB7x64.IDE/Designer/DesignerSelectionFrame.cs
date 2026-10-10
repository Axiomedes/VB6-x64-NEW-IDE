using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Designer;

/// <summary>
/// Marco visual de selección con 8 tiradores de dimensionamiento (Sizing Handles) para manipulación en el diseñador.
/// </summary>
public class DesignerSelectionFrame : Canvas
{
    private const double HandleSize = 7.0;
    private const double HalfHandle = HandleSize / 2.0;

    private readonly Rectangle _outline;
    private readonly Thumb _thumbNW;
    private readonly Thumb _thumbN;
    private readonly Thumb _thumbNE;
    private readonly Thumb _thumbE;
    private readonly Thumb _thumbSE;
    private readonly Thumb _thumbS;
    private readonly Thumb _thumbSW;
    private readonly Thumb _thumbW;

    public FormDesignerControl? BoundControl { get; private set; }
    public FrameworkElement? BoundVisual { get; private set; }

    public event Action<FormDesignerControl>? Resizing;
    public event Action<FormDesignerControl>? ResizeCompleted;

    public Func<double, double>? SnapFunction { get; set; }

    public DesignerSelectionFrame()
    {
        IsHitTestVisible = true;

        // Contorno punteado clásico
        _outline = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0, 85, 234)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Children.Add(_outline);

        // 8 Tiradores en las esquinas y puntos medios
        _thumbNW = CreateThumb(Cursors.SizeNWSE, ThumbNW_DragDelta);
        _thumbN = CreateThumb(Cursors.SizeNS, ThumbN_DragDelta);
        _thumbNE = CreateThumb(Cursors.SizeNESW, ThumbNE_DragDelta);
        _thumbE = CreateThumb(Cursors.SizeWE, ThumbE_DragDelta);
        _thumbSE = CreateThumb(Cursors.SizeNWSE, ThumbSE_DragDelta);
        _thumbS = CreateThumb(Cursors.SizeNS, ThumbS_DragDelta);
        _thumbSW = CreateThumb(Cursors.SizeNESW, ThumbSW_DragDelta);
        _thumbW = CreateThumb(Cursors.SizeWE, ThumbW_DragDelta);

        Children.Add(_thumbNW);
        Children.Add(_thumbN);
        Children.Add(_thumbNE);
        Children.Add(_thumbE);
        Children.Add(_thumbSE);
        Children.Add(_thumbS);
        Children.Add(_thumbSW);
        Children.Add(_thumbW);

        Visibility = Visibility.Collapsed;
    }

    private Thumb CreateThumb(Cursor cursor, DragDeltaEventHandler onDelta)
    {
        var thumb = new Thumb
        {
            Width = HandleSize,
            Height = HandleSize,
            Cursor = cursor
        };

        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, Brushes.White);
        factory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0, 0, 128)));
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        thumb.Template = new ControlTemplate(typeof(Thumb))
        {
            VisualTree = factory
        };

        thumb.DragDelta += onDelta;
        thumb.DragCompleted += (s, e) =>
        {
            if (BoundControl != null)
            {
                ResizeCompleted?.Invoke(BoundControl);
            }
        };

        return thumb;
    }

    /// <summary>
    /// Vincula el marco de selección a un control y posiciona los 8 tiradores.
    /// </summary>
    public void BindTo(FormDesignerControl? control, FrameworkElement? visual)
    {
        BoundControl = control;
        BoundVisual = visual;

        if (control == null || visual == null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        visual.UpdateLayout();
        UpdateLayoutFromControl();
    }

    /// <summary>
    /// Actualiza la posición y tamaño del marco y de los 8 tiradores a partir de las coordenadas del control.
    /// </summary>
    public void UpdateLayoutFromControl()
    {
        if (BoundControl == null) return;

        double x = BoundControl.LeftPixels;
        double y = BoundControl.TopPixels;
        double w = Math.Max(15, BoundControl.WidthPixels);
        double h = Math.Max(15, BoundControl.HeightPixels);

        // Si el control visual está alojado dentro de un contenedor (ej: Frame con GroupBox, PictureBox, etc.)
        // proyectar el origen exacto del contenedor visual sobre el GridCanvas para no omitir el encabezado, bordes y padding del Frame.
        if (BoundVisual != null && Parent is Visual gridCanvas)
        {
            try
            {
                if (BoundVisual.Parent is Visual containerVisual && containerVisual != gridCanvas && containerVisual.IsDescendantOf(gridCanvas))
                {
                    Point origin = ((UIElement)containerVisual).TranslatePoint(new Point(0, 0), (UIElement)gridCanvas);
                    x = origin.X + BoundControl.LeftPixels;
                    y = origin.Y + BoundControl.TopPixels;
                }
                else if (BoundVisual.IsDescendantOf(gridCanvas))
                {
                    x = BoundControl.LeftPixels;
                    y = BoundControl.TopPixels;
                }
            }
            catch
            {
                if (BoundControl.ParentControl != null)
                {
                    x += BoundControl.ParentControl.LeftPixels;
                    y += BoundControl.ParentControl.TopPixels;
                }
            }
        }
        else if (BoundControl.ParentControl != null)
        {
            x += BoundControl.ParentControl.LeftPixels;
            y += BoundControl.ParentControl.TopPixels;
        }

        SetLeft(this, x);
        SetTop(this, y);
        Width = w;
        Height = h;

        _outline.Width = w;
        _outline.Height = h;

        // Posicionar los 8 tiradores
        // 1. Top-Left
        SetLeft(_thumbNW, -HalfHandle);
        SetTop(_thumbNW, -HalfHandle);

        // 2. Top-Center
        SetLeft(_thumbN, (w / 2.0) - HalfHandle);
        SetTop(_thumbN, -HalfHandle);

        // 3. Top-Right
        SetLeft(_thumbNE, w - HalfHandle);
        SetTop(_thumbNE, -HalfHandle);

        // 4. Middle-Right
        SetLeft(_thumbE, w - HalfHandle);
        SetTop(_thumbE, (h / 2.0) - HalfHandle);

        // 5. Bottom-Right
        SetLeft(_thumbSE, w - HalfHandle);
        SetTop(_thumbSE, h - HalfHandle);

        // 6. Bottom-Center
        SetLeft(_thumbS, (w / 2.0) - HalfHandle);
        SetTop(_thumbS, h - HalfHandle);

        // 7. Bottom-Left
        SetLeft(_thumbSW, -HalfHandle);
        SetTop(_thumbSW, h - HalfHandle);

        // 8. Middle-Left
        SetLeft(_thumbW, -HalfHandle);
        SetTop(_thumbW, (h / 2.0) - HalfHandle);

        // Controles de tamaño fijo en diseño (ej. Timer) no muestran tiradores de redimensión
        var thumbsVis = (BoundControl.IsNonResizable) ? Visibility.Collapsed : Visibility.Visible;
        _thumbNW.Visibility = thumbsVis;
        _thumbN.Visibility = thumbsVis;
        _thumbNE.Visibility = thumbsVis;
        _thumbE.Visibility = thumbsVis;
        _thumbSE.Visibility = thumbsVis;
        _thumbS.Visibility = thumbsVis;
        _thumbSW.Visibility = thumbsVis;
        _thumbW.Visibility = thumbsVis;
    }

    private double Snap(double value) => SnapFunction?.Invoke(value) ?? value;

    #region Manejadores de DragDelta para cada tirador
    private void ThumbE_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (BoundControl == null || BoundVisual == null) return;

        double newWidth = Math.Max(15, BoundControl.WidthPixels + e.HorizontalChange);
        newWidth = Snap(newWidth);

        BoundControl.WidthPixels = newWidth;
        BoundVisual.Width = newWidth;

        UpdateLayoutFromControl();
        Resizing?.Invoke(BoundControl);
    }

    private void ThumbS_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (BoundControl == null || BoundVisual == null) return;

        double newHeight = Math.Max(15, BoundControl.HeightPixels + e.VerticalChange);
        newHeight = Snap(newHeight);

        BoundControl.HeightPixels = newHeight;
        BoundVisual.Height = newHeight;

        UpdateLayoutFromControl();
        Resizing?.Invoke(BoundControl);
    }

    private void ThumbSE_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (BoundControl == null || BoundVisual == null) return;

        double newWidth = Math.Max(15, Snap(BoundControl.WidthPixels + e.HorizontalChange));
        double newHeight = Math.Max(15, Snap(BoundControl.HeightPixels + e.VerticalChange));

        BoundControl.WidthPixels = newWidth;
        BoundControl.HeightPixels = newHeight;
        BoundVisual.Width = newWidth;
        BoundVisual.Height = newHeight;

        UpdateLayoutFromControl();
        Resizing?.Invoke(BoundControl);
    }

    private void ThumbW_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (BoundControl == null || BoundVisual == null) return;

        double newLeft = Snap(BoundControl.LeftPixels + e.HorizontalChange);
        double delta = BoundControl.LeftPixels - newLeft;
        double newWidth = Math.Max(15, BoundControl.WidthPixels + delta);

        BoundControl.LeftPixels = newLeft;
        BoundControl.WidthPixels = newWidth;

        SetLeft(BoundVisual, newLeft);
        BoundVisual.Width = newWidth;

        UpdateLayoutFromControl();
        Resizing?.Invoke(BoundControl);
    }

    private void ThumbN_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (BoundControl == null || BoundVisual == null) return;

        double newTop = Snap(BoundControl.TopPixels + e.VerticalChange);
        double delta = BoundControl.TopPixels - newTop;
        double newHeight = Math.Max(15, BoundControl.HeightPixels + delta);

        BoundControl.TopPixels = newTop;
        BoundControl.HeightPixels = newHeight;

        SetTop(BoundVisual, newTop);
        BoundVisual.Height = newHeight;

        UpdateLayoutFromControl();
        Resizing?.Invoke(BoundControl);
    }

    private void ThumbNW_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ThumbN_DragDelta(sender, e);
        ThumbW_DragDelta(sender, e);
    }

    private void ThumbNE_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ThumbN_DragDelta(sender, e);
        ThumbE_DragDelta(sender, e);
    }

    private void ThumbSW_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ThumbS_DragDelta(sender, e);
        ThumbW_DragDelta(sender, e);
    }
    #endregion
}
