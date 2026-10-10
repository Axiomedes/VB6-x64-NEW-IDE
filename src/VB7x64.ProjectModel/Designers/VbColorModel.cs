using System;
using System.Globalization;

namespace VB7x64.ProjectModel.Designers;

public record VbRgbColor(byte R, byte G, byte B, bool IsSystemColor = false, byte SystemColorIndex = 0);

/// <summary>
/// Modelo de conversión y parseo de colores de Visual Basic 6.0 (&H00BBGGRR& y colores de sistema &H8000000F&).
/// </summary>
public static class VbColorModel
{
    public static readonly VbRgbColor DefaultControlColor = new(240, 240, 240);
    public static readonly VbRgbColor DefaultTextColor = new(0, 0, 0);
    public static readonly VbRgbColor DefaultWindowColor = new(255, 255, 255);

    /// <summary>
    /// Parsea una cadena de color de VB6 y retorna sus componentes R, G, B y metadatos de sistema.
    /// </summary>
    public static VbRgbColor ParseColor(string? vbColorStr, VbRgbColor defaultFallback)
    {
        if (string.IsNullOrWhiteSpace(vbColorStr))
        {
            return defaultFallback;
        }

        string clean = vbColorStr.Trim().TrimEnd('&');

        try
        {
            uint colorVal;
            if (clean.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
            {
                colorVal = Convert.ToUInt32(clean.Substring(2), 16);
            }
            else if (clean.StartsWith("#") && clean.Length == 7)
            {
                byte r = byte.Parse(clean.Substring(1, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(clean.Substring(3, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(clean.Substring(5, 2), NumberStyles.HexNumber);
                return new VbRgbColor(r, g, b);
            }
            else if (uint.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint decVal))
            {
                colorVal = decVal;
            }
            else
            {
                return defaultFallback;
            }

            // Chequeo de si es un color de sistema de Windows (máscara 0x80000000)
            if ((colorVal & 0x80000000) != 0)
            {
                byte sysIndex = (byte)(colorVal & 0xFF);
                var rgb = GetWindowsSystemRgb(sysIndex, defaultFallback);
                return new VbRgbColor(rgb.R, rgb.G, rgb.B, true, sysIndex);
            }

            // Color RGB normal de VB6: formato 0x00BBGGRR (Blue en bits 16-23, Green en 8-15, Red en 0-7)
            byte red = (byte)(colorVal & 0xFF);
            byte green = (byte)((colorVal >> 8) & 0xFF);
            byte blue = (byte)((colorVal >> 16) & 0xFF);

            return new VbRgbColor(red, green, blue);
        }
        catch
        {
            return defaultFallback;
        }
    }

    /// <summary>
    /// Convierte componentes R, G, B al formato hexadecimal canónico de VB6 (&H00BBGGRR&).
    /// </summary>
    public static string ToVbColorHex(byte r, byte g, byte b)
    {
        uint bgr = ((uint)b << 16) | ((uint)g << 8) | r;
        return $"&H00{bgr:X6}&";
    }

    private static VbRgbColor GetWindowsSystemRgb(byte index, VbRgbColor fallback)
    {
        return index switch
        {
            0x00 => new VbRgbColor(212, 208, 200), // COLOR_SCROLLBAR
            0x01 => new VbRgbColor(58, 110, 165),   // COLOR_BACKGROUND
            0x02 => new VbRgbColor(10, 36, 106),    // COLOR_ACTIVECAPTION
            0x03 => new VbRgbColor(128, 128, 128),  // COLOR_INACTIVECAPTION
            0x04 => new VbRgbColor(212, 208, 200),  // COLOR_MENU
            0x05 => new VbRgbColor(255, 255, 255),  // COLOR_WINDOW
            0x06 => new VbRgbColor(0, 0, 0),        // COLOR_WINDOWFRAME
            0x07 => new VbRgbColor(0, 0, 0),        // COLOR_MENUTEXT
            0x08 => new VbRgbColor(0, 0, 0),        // COLOR_WINDOWTEXT
            0x09 => new VbRgbColor(255, 255, 255),  // COLOR_CAPTIONTEXT
            0x0A => new VbRgbColor(212, 208, 200),  // COLOR_ACTIVEBORDER
            0x0B => new VbRgbColor(212, 208, 200),  // COLOR_INACTIVEBORDER
            0x0C => new VbRgbColor(128, 128, 128),  // COLOR_APPWORKSPACE
            0x0D => new VbRgbColor(10, 36, 106),    // COLOR_HIGHLIGHT
            0x0E => new VbRgbColor(255, 255, 255),  // COLOR_HIGHLIGHTTEXT
            0x0F => new VbRgbColor(240, 240, 240),  // COLOR_BTNFACE
            0x10 => new VbRgbColor(160, 160, 160),  // COLOR_BTNSHADOW
            0x11 => new VbRgbColor(128, 128, 128),  // COLOR_GRAYTEXT
            0x12 => new VbRgbColor(0, 0, 0),        // COLOR_BTNTEXT
            0x13 => new VbRgbColor(212, 208, 200),  // COLOR_INACTIVECAPTIONTEXT
            0x14 => new VbRgbColor(255, 255, 255),  // COLOR_BTNHIGHLIGHT
            0x15 => new VbRgbColor(64, 64, 64),     // COLOR_3DDKSHADOW
            0x16 => new VbRgbColor(224, 224, 224),  // COLOR_3DLIGHT
            0x17 => new VbRgbColor(0, 0, 0),        // COLOR_INFOTEXT
            0x18 => new VbRgbColor(255, 255, 225),  // COLOR_INFOBK
            _ => fallback
        };
    }
}
