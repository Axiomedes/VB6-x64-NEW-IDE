using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VB7x64.ProjectModel.Documents;

public record VbFileSections(
    string Header,
    string Code,
    bool HasHeader,
    bool IsSafelyDelimited,
    string? WarningMessage = null
);

public static class VbHeaderHandler
{
    private static readonly HashSet<string> VisualExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".frm", ".ctl", ".pag", ".dob", ".dsr"
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".frm", ".ctl", ".pag", ".dob", ".dsr", ".cls", ".bas"
    };

    public static bool IsVbCodeFile(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        return CodeExtensions.Contains(extension);
    }

    public static VbFileSections SplitFileContent(string fullText, string? extension = null)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return new VbFileSections(string.Empty, string.Empty, false, true);
        }

        string ext = extension?.ToLowerInvariant() ?? string.Empty;

        if (VisualExtensions.Contains(ext))
        {
            return SplitVisualComponent(fullText);
        }

        if (ext == ".cls")
        {
            return SplitClassModule(fullText);
        }

        if (ext == ".bas")
        {
            return SplitStandardModule(fullText);
        }

        // Detección automática por contenido si la extensión es desconocida
        if (fullText.Contains("Begin VB.", StringComparison.OrdinalIgnoreCase) ||
            fullText.Contains("Begin {", StringComparison.OrdinalIgnoreCase))
        {
            return SplitVisualComponent(fullText);
        }

        if (fullText.StartsWith("VERSION 1.0 CLASS", StringComparison.OrdinalIgnoreCase))
        {
            return SplitClassModule(fullText);
        }

        if (fullText.TrimStart().StartsWith("Attribute VB_", StringComparison.OrdinalIgnoreCase))
        {
            return SplitStandardModule(fullText);
        }

        // Archivo sin cabecera técnica detectable
        return new VbFileSections(string.Empty, fullText, false, true);
    }

    private static VbFileSections SplitVisualComponent(string fullText)
    {
        using var reader = new StringReader(fullText);
        var headerBuilder = new StringBuilder();
        var codeBuilder = new StringBuilder();

        string? line;
        int beginCount = 0;
        bool foundBegin = false;
        bool reachedEndOfDesign = false;
        bool inAttributesSection = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();

            if (!reachedEndOfDesign)
            {
                if (trimmed.StartsWith("Begin ", StringComparison.OrdinalIgnoreCase))
                {
                    beginCount++;
                    foundBegin = true;
                }
                else if (trimmed.Equals("End", StringComparison.OrdinalIgnoreCase))
                {
                    if (beginCount > 0)
                    {
                        beginCount--;
                        if (beginCount == 0 && foundBegin)
                        {
                            reachedEndOfDesign = true;
                            inAttributesSection = true;
                        }
                    }
                }

                headerBuilder.AppendLine(line);
                continue;
            }

            // Sección de atributos post-diseño (Attribute VB_...)
            if (inAttributesSection)
            {
                if (trimmed.StartsWith("Attribute VB_", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
                {
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else
                {
                    // Primera línea que no es un atributo ni espacio en blanco -> Inicio del código de usuario
                    inAttributesSection = false;
                    codeBuilder.AppendLine(line);
                    continue;
                }
            }

            codeBuilder.AppendLine(line);
        }

        bool safelyDelimited = foundBegin && reachedEndOfDesign && beginCount == 0;
        string? warning = safelyDelimited
            ? null
            : "No se pudo delimitar la sección de diseño con certeza. Se recomienda abrir en modo de solo lectura para evitar corrupción.";

        return new VbFileSections(
            Header: headerBuilder.ToString(),
            Code: codeBuilder.ToString(),
            HasHeader: headerBuilder.Length > 0,
            IsSafelyDelimited: safelyDelimited,
            WarningMessage: warning
        );
    }

    private static VbFileSections SplitClassModule(string fullText)
    {
        using var reader = new StringReader(fullText);
        var headerBuilder = new StringBuilder();
        var codeBuilder = new StringBuilder();

        string? line;
        bool inBeginBlock = false;
        bool foundBegin = false;
        bool reachedEnd = false;
        bool inAttributesSection = false;
        bool inCodeSection = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();

            if (inCodeSection)
            {
                codeBuilder.AppendLine(line);
                continue;
            }

            if (!foundBegin)
            {
                if (trimmed.Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    inBeginBlock = true;
                    foundBegin = true;
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else if (trimmed.StartsWith("Attribute VB_", StringComparison.OrdinalIgnoreCase))
                {
                    // Clase sin bloque BEGIN...END previo
                    inAttributesSection = true;
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else if (trimmed.StartsWith("VERSION", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
                {
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else
                {
                    // Archivo empezó sin cabecera reconocible -> Código de usuario
                    inCodeSection = true;
                    codeBuilder.AppendLine(line);
                    continue;
                }
            }

            if (inBeginBlock)
            {
                headerBuilder.AppendLine(line);
                if (trimmed.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    inBeginBlock = false;
                    reachedEnd = true;
                    inAttributesSection = true;
                }
                continue;
            }

            if (inAttributesSection)
            {
                if (trimmed.StartsWith("Attribute VB_", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
                {
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else
                {
                    inAttributesSection = false;
                    inCodeSection = true;
                    codeBuilder.AppendLine(line);
                    continue;
                }
            }

            codeBuilder.AppendLine(line);
        }

        bool safelyDelimited = !foundBegin || (!inBeginBlock && reachedEnd);
        string? warning = safelyDelimited
            ? null
            : "El bloque BEGIN...END de la clase no fue cerrado correctamente. Se recomienda abrir en modo de solo lectura para evitar corrupción.";

        return new VbFileSections(
            Header: headerBuilder.ToString(),
            Code: codeBuilder.ToString(),
            HasHeader: headerBuilder.Length > 0,
            IsSafelyDelimited: safelyDelimited,
            WarningMessage: warning
        );
    }

    private static VbFileSections SplitStandardModule(string fullText)
    {
        using var reader = new StringReader(fullText);
        var headerBuilder = new StringBuilder();
        var codeBuilder = new StringBuilder();

        string? line;
        bool inAttributesSection = true;
        bool foundAttribute = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();

            if (inAttributesSection)
            {
                if (trimmed.StartsWith("Attribute VB_", StringComparison.OrdinalIgnoreCase))
                {
                    foundAttribute = true;
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else if (!foundAttribute && string.IsNullOrWhiteSpace(trimmed))
                {
                    // Línea vacía antes de cualquier atributo
                    continue;
                }
                else
                {
                    // Primera línea que no es atributo
                    inAttributesSection = false;
                    codeBuilder.AppendLine(line);
                    continue;
                }
            }

            codeBuilder.AppendLine(line);
        }

        return new VbFileSections(
            Header: headerBuilder.ToString(),
            Code: codeBuilder.ToString(),
            HasHeader: foundAttribute,
            IsSafelyDelimited: true,
            WarningMessage: null
        );
    }

    public static string CombineContent(string header, string code, NewLineConvention convention = NewLineConvention.CrLf)
    {
        if (string.IsNullOrEmpty(header))
        {
            return EditorDocument.NormalizeNewLines(code, convention);
        }

        string eol = convention switch
        {
            NewLineConvention.CrLf => "\r\n",
            NewLineConvention.Lf => "\n",
            NewLineConvention.Cr => "\r",
            _ => "\r\n"
        };

        string cleanHeader = EditorDocument.NormalizeNewLines(header.TrimEnd('\r', '\n'), convention);
        string cleanCode = EditorDocument.NormalizeNewLines(code.TrimStart('\r', '\n'), convention);

        if (string.IsNullOrEmpty(cleanCode))
        {
            return cleanHeader + eol;
        }

        return cleanHeader + eol + cleanCode;
    }
}
