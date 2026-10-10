using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Formatting;

public enum ProcedureSortOrder
{
    AlphabeticalAsc,   // A-Z
    AlphabeticalDesc,  // Z-A
    ByType,            // Property -> Function -> Sub (alfabético dentro de cada tipo)
    ByScope            // Public -> Friend -> Private (alfabético dentro de cada ámbito)
}

public class ProcedureSortItem
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Sub"; // Sub, Function, Property Get, Property Let, Property Set
    public string Scope { get; set; } = "Public"; // Public, Private, Friend
    public string Signature { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string LeadingComments { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public string FullText => string.IsNullOrWhiteSpace(LeadingComments) 
        ? Code 
        : LeadingComments + "\r\n" + Code;

    public string DisplayType => Kind;
}

public class ProcedureSortOptions
{
    public ProcedureSortOrder Order { get; set; } = ProcedureSortOrder.AlphabeticalAsc;
    public bool GroupProperties { get; set; } = true;
    public bool PreserveComments { get; set; } = true;
}

public static class ProcedureSorter
{
    private static readonly Regex ProcedureStartRegex = new(
        @"^\s*(?:(Public|Private|Friend|Static)\s+)?(Sub|Function|Property\s+(?:Get|Let|Set))\s+([a-zA-Z0-9_]+)\s*(\(.*?\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeclareFunctionRegex = new(
        @"^\s*(?:(?:Public|Private)\s+)?Declare\s+(?:Sub|Function)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ProcedureEndRegex = new(
        @"^\s*End\s+(Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extrae la cabecera (declaraciones generales) y la lista de procedimientos de un módulo o formulario.
    /// </summary>
    public static (string Header, List<ProcedureSortItem> Procedures) ParseProcedures(string code)
    {
        var procedures = new List<ProcedureSortItem>();
        if (string.IsNullOrEmpty(code))
            return (string.Empty, procedures);

        string[] lines = code.Replace("\r\n", "\n").Split('\n');
        var headerLines = new List<string>();
        int firstProcLineIndex = -1;

        // 1. Localizar dónde empieza el primer procedimiento
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (trimmed.StartsWith('\'') || trimmed.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ProcedureStartRegex.IsMatch(line) && !DeclareFunctionRegex.IsMatch(line))
            {
                firstProcLineIndex = i;
                break;
            }
        }

        // Si no hay procedimientos, todo es cabecera
        if (firstProcLineIndex == -1)
        {
            return (code, procedures);
        }

        // Retroceder para capturar comentarios de documentación que pertenecen al primer procedimiento
        int commentAnchor = firstProcLineIndex;
        while (commentAnchor > 0)
        {
            string prev = lines[commentAnchor - 1].Trim();
            if (prev.StartsWith('\'') || prev.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
            {
                commentAnchor--;
            }
            else
            {
                break;
            }
        }

        for (int i = 0; i < commentAnchor; i++)
        {
            headerLines.Add(lines[i]);
        }

        string header = string.Join("\r\n", headerLines).TrimEnd();

        // 2. Extraer cada procedimiento
        int currentIndex = commentAnchor;
        while (currentIndex < lines.Length)
        {
            // Acumular comentarios previos
            var leadingComments = new List<string>();
            while (currentIndex < lines.Length)
            {
                string line = lines[currentIndex];
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    currentIndex++;
                    continue;
                }
                if (trimmed.StartsWith('\'') || trimmed.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
                {
                    leadingComments.Add(line);
                    currentIndex++;
                }
                else
                {
                    break;
                }
            }

            if (currentIndex >= lines.Length)
            {
                // Comentarios al final del archivo
                break;
            }

            string procStartLine = lines[currentIndex];
            var match = ProcedureStartRegex.Match(procStartLine);
            if (match.Success && !DeclareFunctionRegex.IsMatch(procStartLine))
            {
                string scope = match.Groups[1].Success ? match.Groups[1].Value : "Public";
                string rawKind = match.Groups[2].Value.Trim();
                string kind = NormalizeKind(rawKind);
                string name = match.Groups[3].Value.Trim();
                string sig = match.Groups[4].Success ? match.Groups[4].Value : "()";

                int startLineNumber = currentIndex + 1;
                var procBodyLines = new List<string> { procStartLine };
                currentIndex++;

                while (currentIndex < lines.Length)
                {
                    string bodyLine = lines[currentIndex];
                    procBodyLines.Add(bodyLine);
                    currentIndex++;

                    if (ProcedureEndRegex.IsMatch(bodyLine))
                    {
                        break;
                    }
                }

                int endLineNumber = currentIndex;

                procedures.Add(new ProcedureSortItem
                {
                    Name = name,
                    Kind = kind,
                    Scope = char.ToUpperInvariant(scope[0]) + scope.Substring(1).ToLowerInvariant(),
                    Signature = sig,
                    StartLine = startLineNumber,
                    EndLine = endLineNumber,
                    LeadingComments = string.Join("\r\n", leadingComments),
                    Code = string.Join("\r\n", procBodyLines)
                });
            }
            else
            {
                currentIndex++;
            }
        }

        return (header, procedures);
    }

    private static string NormalizeKind(string rawKind)
    {
        string upper = rawKind.ToUpperInvariant().Replace("  ", " ");
        if (upper.StartsWith("PROPERTY GET")) return "Property Get";
        if (upper.StartsWith("PROPERTY LET")) return "Property Let";
        if (upper.StartsWith("PROPERTY SET")) return "Property Set";
        if (upper.StartsWith("FUNCTION")) return "Function";
        return "Sub";
    }

    /// <summary>
    /// Ordena los procedimientos según las opciones especificadas.
    /// </summary>
    public static List<ProcedureSortItem> SortProcedures(List<ProcedureSortItem> procedures, ProcedureSortOptions options)
    {
        if (procedures == null || procedures.Count <= 1)
            return procedures?.ToList() ?? new List<ProcedureSortItem>();

        var list = new List<ProcedureSortItem>(procedures);

        IOrderedEnumerable<ProcedureSortItem> ordered;

        switch (options.Order)
        {
            case ProcedureSortOrder.AlphabeticalAsc:
                ordered = list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
                break;

            case ProcedureSortOrder.AlphabeticalDesc:
                ordered = list.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase);
                break;

            case ProcedureSortOrder.ByType:
                // Properties primero, luego Functions, luego Subs
                ordered = list.OrderBy(p => GetTypeWeight(p.Kind))
                              .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
                break;

            case ProcedureSortOrder.ByScope:
                // Public primero, luego Friend, luego Private
                ordered = list.OrderBy(p => GetScopeWeight(p.Scope))
                              .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
                break;

            default:
                ordered = list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
                break;
        }

        // Si se pide agrupar propiedades, asegurar que Get/Let/Set queden juntas consecutivamente
        if (options.GroupProperties)
        {
            // Para agrupamiento con tipo/ámbito mantenemos el criterio primario
            ordered = ordered.ThenBy(p => GetPropertyAccessorWeight(p.Kind));
        }

        return ordered.ToList();
    }

    private static int GetTypeWeight(string kind)
    {
        if (kind.StartsWith("Property", StringComparison.OrdinalIgnoreCase)) return 1;
        if (kind.Equals("Function", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3; // Sub
    }

    private static int GetScopeWeight(string scope)
    {
        if (scope.Equals("Public", StringComparison.OrdinalIgnoreCase)) return 1;
        if (scope.Equals("Friend", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3; // Private u otros
    }

    private static int GetPropertyAccessorWeight(string kind)
    {
        if (kind.Equals("Property Get", StringComparison.OrdinalIgnoreCase)) return 1;
        if (kind.Equals("Property Let", StringComparison.OrdinalIgnoreCase)) return 2;
        if (kind.Equals("Property Set", StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    /// <summary>
    /// Genera el código completo reordenado con cabecera y procedimientos formateados.
    /// </summary>
    public static string GenerateSortedCode(string header, List<ProcedureSortItem> sortedProcedures, bool preserveComments = true)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }

        for (int i = 0; i < sortedProcedures.Count; i++)
        {
            var proc = sortedProcedures[i];
            string procText = preserveComments ? proc.FullText : proc.Code;

            sb.AppendLine(procText.TrimEnd());

            if (i < sortedProcedures.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
