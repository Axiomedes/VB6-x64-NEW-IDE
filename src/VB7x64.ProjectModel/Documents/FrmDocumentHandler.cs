using System;
using System.IO;
using System.Text;

namespace VB7x64.ProjectModel.Documents;

public record FrmSections(
    string HeaderAndDesign,
    string Code,
    bool IsSafelyDelimited,
    string? WarningMessage = null
);

public static class FrmDocumentHandler
{
    public static FrmSections SplitFrmContent(string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return new FrmSections(string.Empty, string.Empty, false, "El archivo .frm está vacío.");
        }

        using var reader = new StringReader(fullText);
        var headerBuilder = new StringBuilder();
        var codeBuilder = new StringBuilder();

        string? line;
        int beginCount = 0;
        bool foundFormBegin = false;
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
                    foundFormBegin = true;
                }
                else if (trimmed.Equals("End", StringComparison.OrdinalIgnoreCase))
                {
                    if (beginCount > 0)
                    {
                        beginCount--;
                        if (beginCount == 0 && foundFormBegin)
                        {
                            reachedEndOfDesign = true;
                            inAttributesSection = true;
                        }
                    }
                }

                headerBuilder.AppendLine(line);
                continue;
            }

            // Estamos en la sección post-diseño (Atributos de módulo y luego código)
            if (inAttributesSection)
            {
                if (trimmed.StartsWith("Attribute VB_", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
                {
                    headerBuilder.AppendLine(line);
                    continue;
                }
                else
                {
                    // Primer línea que no es un atributo ni espacio en blanco -> Inicio del código de usuario
                    inAttributesSection = false;
                    codeBuilder.AppendLine(line);
                    continue;
                }
            }

            codeBuilder.AppendLine(line);
        }

        bool safelyDelimited = foundFormBegin && reachedEndOfDesign && beginCount == 0;
        string? warning = null;

        if (!safelyDelimited)
        {
            warning = "No se pudo delimitar la sección de diseño con certeza. Se recomienda abrir en modo de solo lectura para evitar corrupción.";
        }

        return new FrmSections(
            HeaderAndDesign: headerBuilder.ToString(),
            Code: codeBuilder.ToString(),
            IsSafelyDelimited: safelyDelimited,
            WarningMessage: warning
        );
    }

    public static string CombineFrmContent(string headerAndDesign, string newCode, NewLineConvention convention = NewLineConvention.CrLf)
    {
        string eol = convention switch
        {
            NewLineConvention.CrLf => "\r\n",
            NewLineConvention.Lf => "\n",
            NewLineConvention.Cr => "\r",
            _ => "\r\n"
        };

        string cleanHeader = EditorDocument.NormalizeNewLines(headerAndDesign.TrimEnd('\r', '\n'), convention);
        string cleanCode = EditorDocument.NormalizeNewLines(newCode.TrimStart('\r', '\n'), convention);

        return cleanHeader + eol + cleanCode;
    }
}
