using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using VB7x64.ProjectModel.Documents;

namespace VB7x64.ProjectModel.Navigation;

public record ReferenceMatch(
    string FilePath,
    string FileName,
    string ContainerName,
    int LineNumber,
    int ColumnNumber,
    string LineText,
    bool IsDeclaration
)
{
    public string DisplayType => IsDeclaration ? "Definición" : "Referencia / Uso";
    public string DisplayLocation => $"{FileName} (Línea {LineNumber})";
}

public static class ReferenceFinder
{
    private static readonly Regex ProcedureStartRegex = new(
        @"^\s*(?:(Public|Private|Friend|Static)\s+)?(Sub|Function|Property\s+(?:Get|Let|Set))\s+([a-zA-Z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndProcedureRegex = new(
        @"^\s*End\s+(Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Busca todas las referencias y definiciones de un identificador en una lista de archivos.
    /// Excluye coincidencias dentro de cadenas de texto y comentarios.
    /// </summary>
    public static List<ReferenceMatch> FindReferences(
        string symbolName,
        IEnumerable<string> filePaths,
        Func<string, string?>? getOpenDocumentText = null)
    {
        var results = new List<ReferenceMatch>();
        if (string.IsNullOrWhiteSpace(symbolName)) return results;

        string cleanSymbol = symbolName.Trim();
        if (!Regex.IsMatch(cleanSymbol, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            return results;
        }

        string wordPattern = $@"\b{Regex.Escape(cleanSymbol)}\b";
        var wordRegex = new Regex(wordPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var declarationRegex = new Regex(
            $@"^\s*(?:(?:(?:Public|Private|Friend|Static|Global)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set)|Event|Enum|Type|Const)\s+{Regex.Escape(cleanSymbol)}\b|(?:Dim|Public|Private|Global|Static)\s+(?:WithEvents\s+)?{Regex.Escape(cleanSymbol)}\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var filePath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(filePath)) continue;

            string? content = null;
            if (getOpenDocumentText != null)
            {
                content = getOpenDocumentText(filePath);
            }

            if (content == null)
            {
                if (!File.Exists(filePath)) continue;
                try
                {
                    var doc = SafeFileStorage.LoadDocument(filePath);
                    content = doc.Text;
                }
                catch
                {
                    continue;
                }
            }

            string fileName = Path.GetFileName(filePath);
            using var reader = new StringReader(content);
            string? line;
            int lineNumber = 0;
            string currentContainer = "(Declaraciones Generales)";

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Actualizar contenedor de procedimiento
                var procMatch = ProcedureStartRegex.Match(trimmed);
                if (procMatch.Success && !trimmed.Contains("Declare", StringComparison.OrdinalIgnoreCase))
                {
                    string kind = procMatch.Groups[2].Value;
                    string name = procMatch.Groups[3].Value;
                    currentContainer = $"{kind} {name}";
                }

                // Extraer rangos de código activo (excluyendo strings y comentarios)
                var codeRanges = ExtractCodeRanges(line);

                foreach (var (start, length) in codeRanges)
                {
                    string segment = line.Substring(start, length);
                    var matches = wordRegex.Matches(segment);

                    foreach (Match m in matches)
                    {
                        int col = start + m.Index + 1; // 1-indexed
                        bool isDeclaration = declarationRegex.IsMatch(trimmed);

                        results.Add(new ReferenceMatch(
                            FilePath: filePath,
                            FileName: fileName,
                            ContainerName: currentContainer,
                            LineNumber: lineNumber,
                            ColumnNumber: col,
                            LineText: line.Trim(),
                            IsDeclaration: isDeclaration
                        ));
                    }
                }

                if (EndProcedureRegex.IsMatch(trimmed))
                {
                    currentContainer = "(Declaraciones Generales)";
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Divide una línea de código VB6 en rangos (índice inicial y longitud) que corresponden
    /// a código ejecutable, excluyendo literales de cadena ("...") y comentarios ('...).
    /// </summary>
    public static List<(int Start, int Length)> ExtractCodeRanges(string line)
    {
        var ranges = new List<(int Start, int Length)>();
        if (string.IsNullOrEmpty(line)) return ranges;

        bool inString = false;
        int currentStart = 0;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (!inString)
                {
                    // Inicia cadena: guardar rango de código previo si tiene longitud
                    if (i > currentStart)
                    {
                        ranges.Add((currentStart, i - currentStart));
                    }
                    inString = true;
                }
                else
                {
                    // Si viene doble comilla escapada ("") dentro de una cadena
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++; // Saltear la comilla escapada
                    }
                    else
                    {
                        // Cierra la cadena
                        inString = false;
                        currentStart = i + 1;
                    }
                }
            }
            else if (!inString && (c == '\'' || IsRemComment(line, i)))
            {
                // Inicia comentario hasta el fin de la línea: guardar rango previo si existe y terminar
                if (i > currentStart)
                {
                    ranges.Add((currentStart, i - currentStart));
                }
                return ranges;
            }
        }

        // Si terminó la línea y quedó un rango de código
        if (!inString && currentStart < line.Length)
        {
            ranges.Add((currentStart, line.Length - currentStart));
        }

        return ranges;
    }

    private static bool IsRemComment(string line, int index)
    {
        if (index + 3 < line.Length &&
            (line[index] == 'R' || line[index] == 'r') &&
            (line[index + 1] == 'E' || line[index + 1] == 'e') &&
            (line[index + 2] == 'M' || line[index + 2] == 'm') &&
            (line[index + 3] == ' ' || line[index + 3] == '\t'))
        {
            // Solo si está al inicio de la línea o precedido por espacio o dos puntos ':'
            if (index == 0 || char.IsWhiteSpace(line[index - 1]) || line[index - 1] == ':')
            {
                return true;
            }
        }
        return false;
    }
}
