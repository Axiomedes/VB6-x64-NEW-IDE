using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Designer;

/// <summary>
/// Superficie de diseño interactiva para formularios de Visual Basic 6.0.
/// </summary>
public partial class FormDesignSurface : UserControl
{
    private const double TitleBarHeight = 26.0;
    private const double WindowBorderThickness = 3.0;

    public FormDesignModel? CurrentModel { get; private set; }

    public event Action<FormDesignerControl>? ControlSelected;
    public event Action<FormDesignerControl>? ControlDoubleClicked;
    public event Action? ModelModified;

    public FormDesignerControl? SelectedControl { get; private set; }

    public FormDesignSurface()
    {
        InitializeComponent();
        Loaded += FormDesignSurface_Loaded;
    }

    private void FormDesignSurface_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateResizeThumbs();
    }

    /// <summary>
    /// Carga y visualiza el modelo de diseño del formulario.
    /// </summary>
    public void LoadModel(FormDesignModel model)
    {
        CurrentModel = model;

        TxtFormCaption.Text = string.IsNullOrEmpty(model.Caption) ? model.Name : model.Caption;
        TxtFormTitle.Text = $"Diseñador: {model.Name}";

        // Configurar dimensiones del área cliente
        GridCanvas.Width = model.ClientWidthPixels;
        GridCanvas.Height = model.ClientHeightPixels;

        // Configurar color de fondo del formulario
        FormClientArea.Background = VbColorHelper.ToBrush(
            model.BackColorHex,
            new SolidColorBrush(Color.FromRgb(240, 240, 240))
        );

        UpdateDimensionText();
        RefreshCanvas();
        UpdateResizeThumbs();
    }

    /// <summary>
    /// Regenera todos los elementos visuales de los controles en el lienzo.
    /// </summary>
    public void RefreshCanvas()
    {
        GridCanvas.Children.Clear();

        if (CurrentModel == null) return;

        foreach (var control in CurrentModel.Children)
        {
            var visual = ControlVisualRenderer.CreateVisual(control, OnChildVisualCreated);
            AttachControlEvents(visual, control);
            GridCanvas.Children.Add(visual);
        }
    }

    private void OnChildVisualCreated(FormDesignerControl childControl, FrameworkElement childVisual)
    {
        AttachControlEvents(childVisual, childControl);
    }

    private void AttachControlEvents(FrameworkElement visual, FormDesignerControl control)
    {
        visual.MouseLeftButtonDown += (s, e) =>
        {
            SelectControl(control);

            if (e.ClickCount == 2)
            {
                ControlDoubleClicked?.Invoke(control);
                e.Handled = true;
            }
        };

        visual.Cursor = Cursors.SizeAll;
    }

    public void SelectControl(FormDesignerControl? control)
    {
        SelectedControl = control;
        if (control != null)
        {
            TxtDesignerStatus.Text = $"Seleccionado: {control.Name} ({control.DisplayType}) | Pos: X={control.LeftPixels:F0}, Y={control.TopPixels:F0} | Dim: {control.WidthPixels:F0} × {control.HeightPixels:F0} px";
            ControlSelected?.Invoke(control);
        }
        else
        {
            TxtDesignerStatus.Text = "Formulario seleccionado.";
        }
    }

    private void UpdateDimensionText()
    {
        if (CurrentModel == null) return;
        TxtFormDimensions.Text = $"{CurrentModel.ClientWidth} × {CurrentModel.ClientHeight} twips ({CurrentModel.ClientWidthPixels:F0} × {CurrentModel.ClientHeightPixels:F0} px)";
    }

    private void UpdateResizeThumbs()
    {
        double formW = GridCanvas.Width + (WindowBorderThickness * 2);
        double formH = GridCanvas.Height + TitleBarHeight + (WindowBorderThickness * 2);

        // Tirador de la esquina inferior derecha
        Canvas.SetLeft(ThumbResizeCorner, formW);
        Canvas.SetTop(ThumbResizeCorner, formH);

        // Tirador del borde derecho (centrado verticalmente)
        Canvas.SetLeft(ThumbResizeRight, formW);
        Canvas.SetTop(ThumbResizeRight, formH / 2);

        // Tirador del borde inferior (centrado horizontalmente)
        Canvas.SetLeft(ThumbResizeBottom, formW / 2);
        Canvas.SetTop(ThumbResizeBottom, formH);
    }

    #region Redimensión del Formulario con Tiradores
    private void ThumbResizeCorner_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (CurrentModel == null) return;

        double newWidth = Math.Max(120, GridCanvas.Width + e.HorizontalChange);
        double newHeight = Math.Max(60, GridCanvas.Height + e.VerticalChange);

        if (GridCanvas.SnapToGrid)
        {
            newWidth = GridCanvas.SnapValue(newWidth);
            newHeight = GridCanvas.SnapValue(newHeight);
        }

        GridCanvas.Width = newWidth;
        GridCanvas.Height = newHeight;

        CurrentModel.ClientWidthPixels = newWidth;
        CurrentModel.ClientHeightPixels = newHeight;

        UpdateDimensionText();
        UpdateResizeThumbs();
        ModelModified?.Invoke();
    }

    private void ThumbResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (CurrentModel == null) return;

        double newWidth = Math.Max(120, GridCanvas.Width + e.HorizontalChange);
        if (GridCanvas.SnapToGrid)
        {
            newWidth = GridCanvas.SnapValue(newWidth);
        }

        GridCanvas.Width = newWidth;
        CurrentModel.ClientWidthPixels = newWidth;

        UpdateDimensionText();
        UpdateResizeThumbs();
        ModelModified?.Invoke();
    }

    private void ThumbResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (CurrentModel == null) return;

        double newHeight = Math.Max(60, GridCanvas.Height + e.VerticalChange);
        if (GridCanvas.SnapToGrid)
        {
            newHeight = GridCanvas.SnapValue(newHeight);
        }

        GridCanvas.Height = newHeight;
        CurrentModel.ClientHeightPixels = newHeight;

        UpdateDimensionText();
        UpdateResizeThumbs();
        ModelModified?.Invoke();
    }
    #endregion

    #region Barra de Herramientas del Diseñador
    private void BtnToggleGrid_Click(object sender, RoutedEventArgs e)
    {
        GridCanvas.ShowGrid = BtnToggleGrid.IsChecked == true;
    }

    private void BtnToggleSnap_Click(object sender, RoutedEventArgs e)
    {
        GridCanvas.SnapToGrid = BtnToggleSnap.IsChecked == true;
    }

    private void CmbGridSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCanvas == null) return;

        GridCanvas.GridSize = CmbGridSize.SelectedIndex switch
        {
            0 => 4.0,
            1 => 8.0,
            2 => 12.0,
            3 => 16.0,
            _ => 8.0
        };
    }
    #endregion
}
