using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ViBa7.ProjectModel.Documents;

public static partial class EncodingDetector
{
    static EncodingDetector()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern int GetACP();

    private static readonly Lazy<Encoding> _windowsAnsiEncoding = new(() =>
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int acp = GetACP();
                if (acp > 0)
                {
                    return Encoding.GetEncoding(acp);
                }
            }
        }
        catch
        {
            // Ignorar y caer en 1252
        }

        return Encoding.GetEncoding(1252);
    });

    public static Encoding WindowsAnsiEncoding => _windowsAnsiEncoding.Value;

    public record DetectionResult(
        Encoding Encoding,
        bool HasBom,
        int PreambleLength,
        NewLineConvention NewLineConvention
    );

    public static DetectionResult Detect(ReadOnlySpan<byte> buffer, Encoding? customFallbackEncoding = null)
    {
        // 1. Detección de BOM
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            var text = Encoding.UTF8.GetString(buffer[3..]);
            return new DetectionResult(new UTF8Encoding(true), true, 3, DetectNewLine(text));
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            var text = Encoding.Unicode.GetString(buffer[2..]);
            return new DetectionResult(new UnicodeEncoding(false, true), true, 2, DetectNewLine(text));
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            var text = Encoding.BigEndianUnicode.GetString(buffer[2..]);
            return new DetectionResult(new UnicodeEncoding(true, true), true, 2, DetectNewLine(text));
        }

        // 2. Si no hay BOM, verificar si es UTF-8 estricto o ANSI
        // Para archivos VB6 clásicos sin BOM, el estándar dominante histórico es ANSI (Windows-1252 o ACP del sistema).
        Encoding fallback = customFallbackEncoding ?? WindowsAnsiEncoding;

        if (IsValidUtf8(buffer, out bool hasNonAscii))
        {
            // Si tiene caracteres no-ASCII y es UTF-8 válido
            if (hasNonAscii)
            {
                // Podría ser UTF-8 sin BOM, pero si el usuario prefiere ANSI, se respeta la preferencia si se indica
                var utf8Text = Encoding.UTF8.GetString(buffer);
                return new DetectionResult(new UTF8Encoding(false), false, 0, DetectNewLine(utf8Text));
            }
            // Si es puro ASCII (<= 127), ANSI y UTF-8 son idénticos byte a byte. Por compatibilidad VB6, usamos ANSI con fallback.
            var asciiText = fallback.GetString(buffer);
            return new DetectionResult(fallback, false, 0, DetectNewLine(asciiText));
        }

        // Si contiene bytes inválidos para UTF-8, es definitivamente ANSI (o la página de códigos local)
        string detectedText = fallback.GetString(buffer);
        return new DetectionResult(fallback, false, 0, DetectNewLine(detectedText));
    }

    public static NewLineConvention DetectNewLine(string text)
    {
        int crlfCount = 0;
        int lfCount = 0;
        int crCount = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlfCount++;
                    i++;
                }
                else
                {
                    crCount++;
                }
            }
            else if (text[i] == '\n')
            {
                lfCount++;
            }
        }

        if (crlfCount >= lfCount && crlfCount >= crCount) return NewLineConvention.CrLf;
        if (lfCount > crlfCount && lfCount >= crCount) return NewLineConvention.Lf;
        if (crCount > 0) return NewLineConvention.Cr;

        return NewLineConvention.CrLf; // Por defecto para VB6
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes, out bool hasNonAscii)
    {
        hasNonAscii = false;
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            if (b <= 0x7F)
            {
                i++;
                continue;
            }

            hasNonAscii = true;
            int sequenceLength;
            if ((b & 0xE0) == 0xC0) sequenceLength = 2;
            else if ((b & 0xF0) == 0xE0) sequenceLength = 3;
            else if ((b & 0xF8) == 0xF0) sequenceLength = 4;
            else return false;

            if (i + sequenceLength > bytes.Length) return false;

            for (int j = 1; j < sequenceLength; j++)
            {
                if ((bytes[i + j] & 0xC0) != 0x80) return false;
            }

            i += sequenceLength;
        }

        return true;
    }
}
