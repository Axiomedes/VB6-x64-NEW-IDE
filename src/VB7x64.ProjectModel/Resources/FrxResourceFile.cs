using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Resources;

public enum VbImageFormat
{
    Unknown,
    Bmp,
    Ico,
    Jpeg,
    Gif,
    Png,
    WebP
}

/// <summary>
/// Gestiona la lectura y escritura de recursos binarios en archivos .frx (y .ctx / .pgx)
/// de formularios y controles de Visual Basic 6.0, manteniendo el formato binario canónico
/// de OLE StdPicture (preámbulo 0x0000746C + longitud + bytes) y compatibilidad total.
/// </summary>
public static class FrxResourceFile
{
    public const uint OlePictureSignature = 0x0000746C; // "lt\0\0" en little-endian

    private static readonly Regex FrxRefRegex = new(
        @"^""?(?:(?<file>[^""]+?):)?(?<offset>[0-9A-Fa-f]{1,8})""?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parsea una referencia clásica de recurso FRX (ej. `"Form1.frx":08CA`, `Form1.frx:08CA` o `08CA`).
    /// </summary>
    public static bool ParseReference(string? reference, out string frxFileName, out int offset)
    {
        frxFileName = string.Empty;
        offset = 0;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        string clean = reference.Trim();
        int colonIdx = clean.LastIndexOf(':');
        if (colonIdx >= 0)
        {
            string filePart = clean.Substring(0, colonIdx).Trim().Trim('"');
            string hexPart = clean.Substring(colonIdx + 1).Trim().Trim('"');
            frxFileName = filePart;
            if (int.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedOffset))
            {
                offset = parsedOffset;
                return true;
            }
        }
        else
        {
            string hexPart = clean.Trim().Trim('"');
            if (int.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedOffset))
            {
                frxFileName = string.Empty;
                offset = parsedOffset;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Formatea una referencia en sintaxis estándar de VB6 (ej. `"Form1.frx":0000`).
    /// </summary>
    public static string FormatReference(string frxFileName, int offset)
    {
        string hex = offset.ToString("X4", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(frxFileName) ? hex : $"\"{frxFileName}\":{hex}";
    }

    /// <summary>
    /// Lee la carga útil binaria de una imagen o recurso desde un archivo .frx en el desplazamiento especificado.
    /// Soporta preámbulos OLE StdPicture (8 y 12 bytes) e imágenes binarias directas.
    /// </summary>
    public static byte[]? ReadResource(string frxFilePath, int offset)
    {
        if (!File.Exists(frxFilePath))
        {
            return null;
        }

        try
        {
            byte[] allBytes = File.ReadAllBytes(frxFilePath);
            return ReadResource(allBytes, offset);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lee la carga útil de un recurso a partir de un arreglo de bytes en el desplazamiento indicado.
    /// </summary>
    public static byte[]? ReadResource(byte[] frxData, int offset)
    {
        if (frxData == null || offset < 0 || offset >= frxData.Length)
        {
            return null;
        }

        int available = frxData.Length - offset;
        if (available < 4)
        {
            return null;
        }

        uint magic = BitConverter.ToUInt32(frxData, offset);

        // Caso 1: Estándar OLE StdPicture (Magic 0x0000746C)
        if (magic == OlePictureSignature && available >= 8)
        {
            uint payloadLength = BitConverter.ToUInt32(frxData, offset + 4);

            // Variante estándar de 8 bytes de cabecera: [Magic 4B][Length 4B][Payload...]
            if (payloadLength > 0 && offset + 8 + payloadLength <= frxData.Length)
            {
                // Verificar si los datos comienzan en offset + 8
                var format8 = DetectImageFormat(new ReadOnlySpan<byte>(frxData, offset + 8, Math.Min(16, (int)payloadLength)));
                if (format8 != VbImageFormat.Unknown)
                {
                    byte[] result = new byte[payloadLength];
                    Buffer.BlockCopy(frxData, offset + 8, result, 0, (int)payloadLength);
                    return result;
                }

                // Variante de 12 bytes de cabecera: [Magic 4B][Type 4B][Length 4B][Payload...]
                if (available >= 12)
                {
                    uint payloadLength12 = BitConverter.ToUInt32(frxData, offset + 8);
                    if (payloadLength12 > 0 && offset + 12 + payloadLength12 <= frxData.Length)
                    {
                        var format12 = DetectImageFormat(new ReadOnlySpan<byte>(frxData, offset + 12, Math.Min(16, (int)payloadLength12)));
                        if (format12 != VbImageFormat.Unknown)
                        {
                            byte[] result12 = new byte[payloadLength12];
                            Buffer.BlockCopy(frxData, offset + 12, result12, 0, (int)payloadLength12);
                            return result12;
                        }
                    }
                }

                // Si no se reconoció una cabecera gráfica específica, extraer payload estándar
                byte[] rawResult = new byte[payloadLength];
                Buffer.BlockCopy(frxData, offset + 8, rawResult, 0, (int)payloadLength);
                return rawResult;
            }
        }

        // Caso 2: Carga binaria directa sin cabecera OLE
        var directFormat = DetectImageFormat(new ReadOnlySpan<byte>(frxData, offset, Math.Min(16, available)));
        if (directFormat != VbImageFormat.Unknown)
        {
            int length = GetDirectImageLength(frxData, offset, directFormat);
            if (length > 0 && offset + length <= frxData.Length)
            {
                byte[] result = new byte[length];
                Buffer.BlockCopy(frxData, offset, result, 0, length);
                return result;
            }

            // Fallback: leer hasta el final
            byte[] rest = new byte[available];
            Buffer.BlockCopy(frxData, offset, rest, 0, available);
            return rest;
        }

        return null;
    }

    /// <summary>
    /// Agrega una imagen o recurso binario al archivo .frx correspondiente utilizando
    /// la estructura canónica de VB6 (0x0000746C + longitud + bytes) y retorna el desplazamiento en bytes.
    /// Si el archivo no existe, lo crea automáticamente.
    /// </summary>
    public static int AppendResource(string frxFilePath, byte[] resourceData)
    {
        ArgumentNullException.ThrowIfNull(resourceData);

        string? dir = Path.GetDirectoryName(frxFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(frxFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        int offset = (int)stream.Length;
        stream.Seek(offset, SeekOrigin.Begin);

        using var writer = new BinaryWriter(stream);
        // Preámbulo canónico OLE StdPicture
        writer.Write(OlePictureSignature);
        // Longitud en bytes de la imagen
        writer.Write((uint)resourceData.Length);
        // Bytes de la imagen (WEBP, PNG, JPG, ICO, BMP, etc.)
        writer.Write(resourceData);
        writer.Flush();

        return offset;
    }

    /// <summary>
    /// Sincroniza y asegura que todos los recursos gráficos presentes en memoria (PictureData)
    /// se guarden en el archivo .frx correspondiente al formulario (.frm), asignando referencias
    /// canónicas de VB6 ("Form1.frx":0000) a cada control.
    /// </summary>
    public static void EnsureFrxResourcesSaved(Designers.FormDesignModel model, string frmFilePath)
    {
        if (model == null || string.IsNullOrWhiteSpace(frmFilePath)) return;

        string frxPath = Path.ChangeExtension(Path.GetFullPath(frmFilePath), ".frx");
        string frxFileName = Path.GetFileName(frxPath);

        bool hasFormPic = model.PictureData != null && model.PictureData.Length > 0;
        var allControls = model.GetAllControls();
        var controlsWithPictures = allControls
            .Where(c => c.PictureData != null && c.PictureData.Length > 0)
            .ToList();

        if (!hasFormPic && controlsWithPictures.Count == 0)
        {
            return;
        }

        string? dir = Path.GetDirectoryName(frxPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(frxPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        if (hasFormPic)
        {
            int offset = (int)stream.Position;
            writer.Write(OlePictureSignature);
            writer.Write((uint)model.PictureData!.Length);
            writer.Write(model.PictureData);
            model.Picture = FormatReference(frxFileName, offset);
        }

        foreach (var ctrl in controlsWithPictures)
        {
            int offset = (int)stream.Position;
            writer.Write(OlePictureSignature);
            writer.Write((uint)ctrl.PictureData!.Length);
            writer.Write(ctrl.PictureData);
            ctrl.Picture = FormatReference(frxFileName, offset);
        }

        writer.Flush();
    }

    /// <summary>
    /// Detecta el formato gráfico a partir de las firmas mágicas de cabecera.
    /// </summary>
    public static VbImageFormat DetectImageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return VbImageFormat.Unknown;

        // Windows Bitmap (BM)
        if (header[0] == 0x42 && header[1] == 0x4D)
        {
            return VbImageFormat.Bmp;
        }

        // Icono Windows / Cursor
        if (header.Length >= 4 && header[0] == 0x00 && header[1] == 0x00 &&
            (header[2] == 0x01 || header[2] == 0x02) && header[3] == 0x00)
        {
            return VbImageFormat.Ico;
        }

        // JPEG (0xFF, 0xD8, 0xFF)
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return VbImageFormat.Jpeg;
        }

        // GIF (GIF87a o GIF89a)
        if (header.Length >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
        {
            return VbImageFormat.Gif;
        }

        // PNG (0x89, 'P', 'N', 'G')
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return VbImageFormat.Png;
        }

        // WebP (RIFF .... WEBP)
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return VbImageFormat.WebP;
        }

        return VbImageFormat.Unknown;
    }

    private static int GetDirectImageLength(byte[] data, int offset, VbImageFormat format)
    {
        int available = data.Length - offset;

        switch (format)
        {
            case VbImageFormat.Bmp:
                if (available >= 6)
                {
                    int bmpSize = BitConverter.ToInt32(data, offset + 2);
                    if (bmpSize > 0 && bmpSize <= available) return bmpSize;
                }
                break;

            case VbImageFormat.WebP:
                if (available >= 8)
                {
                    uint riffSize = BitConverter.ToUInt32(data, offset + 4);
                    int totalSize = (int)riffSize + 8;
                    if (totalSize > 0 && totalSize <= available) return totalSize;
                }
                break;
        }

    return available;
    }

    /// <summary>
    /// Retorna una descripción legible del formato de imagen para la ventana de propiedades.
    /// </summary>
    public static string GetFormatDescription(byte[]? bytes, string? reference = null)
    {
        if (bytes == null || bytes.Length == 0)
        {
            if (!string.IsNullOrEmpty(reference))
            {
                return "(Picture)";
            }
            return "(None)";
        }

        var format = DetectImageFormat(bytes);
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
