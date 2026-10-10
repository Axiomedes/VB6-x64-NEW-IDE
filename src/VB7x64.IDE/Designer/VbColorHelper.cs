using System;
using System.Windows.Media;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Designer;

/// <summary>
/// Utilidad para convertir colores del formato de Visual Basic 6.0 a colores y pinceles de WPF.
/// </summary>
public static class VbColorHelper
{
    private static readonly SolidColorBrush DefaultControlBrush = new(Color.FromRgb(240, 240, 240));
    private static readonly SolidColorBrush DefaultTextBrush = new(Color.FromRgb(0, 0, 0));
    private static readonly SolidColorBrush DefaultWindowBrush = new(Color.FromRgb(255, 255, 255));

    static VbColorHelper()
    {
        DefaultControlBrush.Freeze();
        DefaultTextBrush.Freeze();
        DefaultWindowBrush.Freeze();
    }

    /// <summary>
    /// Convierte una cadena de color de VB6 (&H8000000F& o &H00BBGGRR& o número decimal) a un Color de WPF.
    /// </summary>
    public static Color ToWpfColor(string? vbColorStr, Color defaultFallback)
    {
        var fallback = new VbRgbColor(defaultFallback.R, defaultFallback.G, defaultFallback.B);
        var parsed = VbColorModel.ParseColor(vbColorStr, fallback);
        return Color.FromRgb(parsed.R, parsed.G, parsed.B);
    }

    /// <summary>
    /// Convierte una cadena de color de VB6 a un SolidColorBrush de WPF.
    /// </summary>
    public static SolidColorBrush ToBrush(string? vbColorStr, SolidColorBrush defaultBrush)
    {
        if (string.IsNullOrWhiteSpace(vbColorStr))
        {
            return defaultBrush;
        }

        var col = ToWpfColor(vbColorStr, defaultBrush.Color);
        if (col == defaultBrush.Color)
        {
            return defaultBrush;
        }

        var brush = new SolidColorBrush(col);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Convierte un Color de WPF a la representación hexadecimal canónica de VB6 (&H00BBGGRR&).
    /// </summary>
    public static string ToVbColorHex(Color color)
    {
        return VbColorModel.ToVbColorHex(color.R, color.G, color.B);
    }
}

