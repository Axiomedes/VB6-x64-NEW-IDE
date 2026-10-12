using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VB7x64.IDE.Designer;

/// <summary>
/// Herramienta interactiva de cuentagotas / gotero (Eyedropper) para capturar colores
/// de cualquier píxel de la pantalla, incluyendo aplicaciones externas, imágenes y escritorio.
/// </summary>
public static class ScreenColorDropper
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Lee el color exacto del píxel ubicado bajo el cursor del ratón en coordenadas de pantalla global.
    /// </summary>
    public static Color GetColorAtCursor()
    {
        if (GetCursorPos(out POINT pt))
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                try
                {
                    uint pixel = GetPixel(hdc, pt.X, pt.Y);
                    byte r = (byte)(pixel & 0x000000FF);
                    byte g = (byte)((pixel & 0x0000FF00) >> 8);
                    byte b = (byte)((pixel & 0x00FF0000) >> 16);
                    return Color.FromRgb(r, g, b);
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
        }
        return Colors.Black;
    }

    /// <summary>
    /// Inicia el modo de captura por cuentagotas mostrando una lupa flotante sobre toda la pantalla.
    /// </summary>
    public static void StartColorPick(Action<Color> onColorPicked)
    {
        var window = new ScreenColorDropperWindow(onColorPicked);
        window.Show();
    }
}

/// <summary>
/// Ventana transparente de captura en pantalla completa para el cuentagotas.
/// </summary>
internal class ScreenColorDropperWindow : Window
{
    private readonly Action<Color> _onColorPicked;
    private readonly Canvas _hudCanvas;
    private readonly Border _previewBorder;
    private readonly Rectangle _colorSwatch;
    private readonly TextBlock _txtHex;
    private readonly TextBlock _txtRgb;
    private readonly DispatcherTimer _timer;

    public ScreenColorDropperWindow(Action<Color> onColorPicked)
    {
        _onColorPicked = onColorPicked ?? throw new ArgumentNullException(nameof(onColorPicked));

        // Cubrir todo el escritorio virtual (incluyendo múltiples monitores)
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // Casi invisible pero captura eventos del ratón
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _hudCanvas = new Canvas { IsHitTestVisible = false };

        _colorSwatch = new Rectangle
        {
            Width = 26,
            Height = 26,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            Margin = new Thickness(0, 0, 6, 0)
        };

        _txtHex = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        _txtRgb = new TextBlock
        {
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
        };

        var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(_txtHex);
        textStack.Children.Add(_txtRgb);

        var hintText = new TextBlock
        {
            Text = "Clic: capturar • Esc: cancelar",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            Margin = new Thickness(0, 3, 0, 0)
        };

        var mainStack = new StackPanel { Orientation = Orientation.Vertical };
        var topRow = new StackPanel { Orientation = Orientation.Horizontal };
        topRow.Children.Add(_colorSwatch);
        topRow.Children.Add(textStack);
        mainStack.Children.Add(topRow);
        mainStack.Children.Add(hintText);

        _previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 4, 6, 4),
            Child = mainStack
        };

        _hudCanvas.Children.Add(_previewBorder);
        Content = _hudCanvas;

        Loaded += (_, _) =>
        {
            CaptureMouse();
            UpdateColorUnderCursor();
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        _timer.Tick += (_, _) => UpdateColorUnderCursor();
        _timer.Start();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                ClosePicker();
            }
        };

        MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var color = ScreenColorDropper.GetColorAtCursor();
                ClosePicker();
                _onColorPicked?.Invoke(color);
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                ClosePicker();
            }
        };
    }

    private void UpdateColorUnderCursor()
    {
        var pos = Mouse.GetPosition(this);
        var color = ScreenColorDropper.GetColorAtCursor();

        _colorSwatch.Fill = new SolidColorBrush(color);
        string vbHex = VbColorHelper.ToVbColorHex(color);
        _txtHex.Text = vbHex;
        _txtRgb.Text = $"RGB({color.R}, {color.G}, {color.B})";

        // Posicionar el cuadro informativo ligeramente desplazado del cursor para no tapar el píxel
        double hudX = pos.X + 18;
        double hudY = pos.Y + 18;

        // Evitar que el cuadro salga de la pantalla
        if (hudX + 160 > ActualWidth) hudX = pos.X - 165;
        if (hudY + 60 > ActualHeight) hudY = pos.Y - 65;

        Canvas.SetLeft(_previewBorder, Math.Max(0, hudX));
        Canvas.SetTop(_previewBorder, Math.Max(0, hudY));
    }

    private void ClosePicker()
    {
        _timer.Stop();
        ReleaseMouseCapture();
        Close();
    }
}
