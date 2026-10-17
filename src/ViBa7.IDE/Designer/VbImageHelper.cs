using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ViBa7.ProjectModel.Resources;

namespace ViBa7.IDE.Designer;

/// <summary>
/// Provee decodificación y gestión de imágenes para controles de VB6 en el IDE (WPF),
/// con soporte completo de formatos clásicos y modernos: WEBP, PNG (con canal alfa),
/// JPEG, ICO, BMP y GIF.
/// </summary>
public static class VbImageHelper
{
    /// <summary>
    /// Decodifica un arreglo de bytes en un BitmapSource inmutable de WPF.
    /// Soporta WebP, PNG (con transparencia alfa completa), JPEG, ICO, BMP y GIF.
    /// </summary>
    public static BitmapSource? LoadBitmapFromBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        // Intento 0: Si es un formato Icono (.ico), usar decodificador especializado IconBitmapDecoder de WPF
        if (FrxResourceFile.DetectImageFormat(bytes) == VbImageFormat.Ico)
        {
            try
            {
                using var icoStream = new MemoryStream(bytes);
                var iconDecoder = new IconBitmapDecoder(icoStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (iconDecoder.Frames.Count > 0)
                {
                    // Buscar el fotograma más adecuado (preferir 32x32, 16x16 o el más cercano a tamaño estándar)
                    BitmapFrame bestFrame = iconDecoder.Frames[0];
                    int bestScore = int.MaxValue;
                    foreach (var f in iconDecoder.Frames)
                    {
                        int w = f.PixelWidth;
                        int h = f.PixelHeight;
                        int score = (w == 32 && h == 32) ? 0
                                  : (w == 16 && h == 16) ? 1
                                  : (w == 24 && h == 24) ? 2
                                  : (w == 48 && h == 48) ? 3
                                  : Math.Abs(w - 32) + 10;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestFrame = f;
                        }
                    }

                    int bw = bestFrame.PixelWidth;
                    int bh = bestFrame.PixelHeight;
                    int stride = bw * 4;
                    byte[] pixelBytes = new byte[bh * stride];
                    var conv = new FormatConvertedBitmap(bestFrame, PixelFormats.Bgra32, null, 0);
                    conv.CopyPixels(pixelBytes, stride, 0);
                    var icoBmp = BitmapSource.Create(bw, bh, 96, 96, PixelFormats.Bgra32, null, pixelBytes, stride);
                    icoBmp.Freeze();
                    return icoBmp;
                }
            }
            catch
            {
                // Continuar a ImageSharp o WIC si falla
            }
        }

        // Intento 1: ImageSharp (manejo universal y perfecto de WebP, PNG+alpha, JPG, BMP, GIF)
        try
        {
            using var stream = new MemoryStream(bytes);
            using var image = Image.Load<Bgra32>(stream);

            int width = image.Width;
            int height = image.Height;
            int stride = width * 4;
            byte[] pixelBytes = new byte[height * stride];

            image.CopyPixelDataTo(pixelBytes);

            var bitmapSource = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelBytes,
                stride
            );

            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            // Fallback a decodificador nativo WIC / WPF
        }

        // Intento 2: Decodificador WIC nativo de WPF (normalizado estrictamente a 96 DPI Bgra32)
        try
        {
            using var stream = new MemoryStream(bytes);
            var frame = BitmapFrame.Create(
                stream,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad
            );

            int width = frame.PixelWidth;
            int height = frame.PixelHeight;
            int stride = width * 4;
            byte[] pixelBytes = new byte[height * stride];

            var conv = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            conv.CopyPixels(pixelBytes, stride, 0);

            var wicSource = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelBytes,
                stride
            );

            wicSource.Freeze();
            return wicSource;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Carga una imagen directamente desde una ruta de archivo en disco.
    /// </summary>
    public static BitmapSource? LoadBitmapFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            return LoadBitmapFromBytes(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Obtiene una descripción legible del formato y estado para mostrar en la Ventana de Propiedades
    /// (ej. "(PNG)", "(WebP)", "(Bitmap)", "(None)").
    /// </summary>
    public static string GetFormatDescription(byte[]? bytes, string? reference)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return "(None)";
        }

        var format = FrxResourceFile.DetectImageFormat(bytes);
        return format switch
        {
            VbImageFormat.Bmp => "(Bitmap)",
            VbImageFormat.Ico => "(Icon)",
            VbImageFormat.Png => "(PNG)",
            VbImageFormat.WebP => "(WebP)",
            VbImageFormat.Jpeg => "(JPEG)",
            VbImageFormat.Gif => "(GIF)",
            _ => "(Picture)"
        };
    }
}
