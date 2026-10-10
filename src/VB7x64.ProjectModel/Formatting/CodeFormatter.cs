using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Formatting;

public class CodeFormatterOptions
{
    public int IndentSize { get; set; } = 4;
    public bool UseTabs { get; set; } = false;
    public int MaxConsecutiveBlankLines { get; set; } = 1;
    public bool EnsureBlankLineBetweenProcedures { get; set; } = true;
    public bool AlignDeclarations { get; set; } = false;
}

public static class CodeFormatter
{
    private static readonly Regex ProcedureStartRegex = new(
        @"^(?:(?:Public|Private|Friend|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeclareFunctionRegex = new(
        @"^(?:(?:Public|Private)\s+)?Declare\s+(?:Sub|Function)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeStartRegex = new(
        @"^(?:(?:Public|Private)\s+)?Type\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EnumStartRegex = new(
        @"^(?:(?:Public|Private)\s+)?Enum\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LabelRegex = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*:\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NumericLabelRegex = new(
        @"^[0-9]+:\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DeclarationLineRegex = new(
        @"^((?:(?:Public|Private|Global|Dim|Static|Const)\s+)+)([a-zA-Z0-9_]+(?:\s*\([^)]*\))?)\s+(As\s+.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Formatea un archivo o bloque completo de código VB6.
    /// </summary>
    public static string FormatCode(string code, CodeFormatterOptions? options = null)
    {
        if (string.IsNullOrEmpty(code))
            return code;

        options ??= new CodeFormatterOptions();

        string indentUnit = options.UseTabs ? "\t" : new string(' ', Math.Max(1, options.IndentSize));

        // 1. Opcional: Alinear declaraciones 'As' antes o durante el formateo
        if (options.AlignDeclarations)
        {
            code = AlignAsInDeclarations(code);
        }

        string[] rawLines = code.Replace("\r\n", "\n").Split('\n');
        var outputLines = new List<string>();

        int currentIndent = 0;
        var selectDepthStack = new Stack<int>(); // guarda si un Select Case tiene Case abierto
        bool isContinuation = false;
        int blankCount = 0;
        bool previousWasEndProc = false;

        for (int i = 0; i < rawLines.Length; i++)
        {
            string rawLine = rawLines[i];
            string trimmed = rawLine.Trim();

            // Gestión de líneas en blanco
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                blankCount++;
                if (blankCount <= options.MaxConsecutiveBlankLines)
                {
                    outputLines.Add(string.Empty);
                }
                continue;
            }

            blankCount = 0;

            // Separación de procedimientos: si la línea anterior fue un End Sub/Function/Property y viene un nuevo procedimiento
            if (options.EnsureBlankLineBetweenProcedures && previousWasEndProc)
            {
                string cleanTrimmedCheck = StripCommentsAndStrings(trimmed).Trim();
                if (ProcedureStartRegex.IsMatch(cleanTrimmedCheck) && !DeclareFunctionRegex.IsMatch(cleanTrimmedCheck))
                {
                    if (outputLines.Count > 0 && !string.IsNullOrWhiteSpace(outputLines[^1]))
                    {
                        outputLines.Add(string.Empty);
                    }
                }
            }
            previousWasEndProc = false;

            string clean = StripCommentsAndStrings(trimmed).Trim();

            // Verificar si es etiqueta (ErrorHandler:)
            bool isLabel = LabelRegex.IsMatch(trimmed) || NumericLabelRegex.IsMatch(trimmed);

            // Calcular desindents para la línea actual
            int lineIndent = currentIndent;

            // 1. End blocks (reducen indent antes de la línea)
            bool isEndProc = false;
            if (clean.StartsWith("End Sub", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("End Function", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("End Property", StringComparison.OrdinalIgnoreCase))
            {
                currentIndent = Math.Max(0, currentIndent - 1);
                lineIndent = currentIndent;
                isEndProc = true;
                previousWasEndProc = true;
            }
            else if (clean.StartsWith("End If", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("End With", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("End Type", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("End Enum", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("Loop", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("Next", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("Wend", StringComparison.OrdinalIgnoreCase))
            {
                currentIndent = Math.Max(0, currentIndent - 1);
                lineIndent = currentIndent;
            }
            else if (clean.StartsWith("End Select", StringComparison.OrdinalIgnoreCase))
            {
                // Si había un Case abierto dentro del Select, cerramos el Case (-1) y cerramos el Select (-1)
                if (selectDepthStack.Count > 0)
                {
                    int caseOpened = selectDepthStack.Pop();
                    if (caseOpened > 0)
                    {
                        currentIndent = Math.Max(0, currentIndent - 1);
                    }
                }
                currentIndent = Math.Max(0, currentIndent - 1);
                lineIndent = currentIndent;
            }
            else if (clean.StartsWith("ElseIf", StringComparison.OrdinalIgnoreCase) ||
                     clean.Equals("Else", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("Else ", StringComparison.OrdinalIgnoreCase))
            {
                // Mid-block: la línea se indenta 1 nivel menos que el contenido interno
                lineIndent = Math.Max(0, currentIndent - 1);
            }
            else if (clean.StartsWith("Case ", StringComparison.OrdinalIgnoreCase) ||
                     clean.Equals("Case Else", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("Case Else", StringComparison.OrdinalIgnoreCase))
            {
                // Dentro de Select Case:
                if (selectDepthStack.Count > 0)
                {
                    int caseOpened = selectDepthStack.Peek();
                    if (caseOpened > 0)
                    {
                        // Ya había un Case previo, retroceder la indentación de las sentencias del Case previo
                        currentIndent = Math.Max(0, currentIndent - 1);
                    }
                    lineIndent = currentIndent;
                }
            }

            // Aplicar sangría
            int effectiveIndent = lineIndent;
            if (isContinuation)
            {
                effectiveIndent += 1;
            }

            if (isLabel)
            {
                // Las etiquetas van sin sangría o al margen
                effectiveIndent = 0;
            }

            string indentedLine = BuildIndentString(indentUnit, effectiveIndent) + trimmed;
            outputLines.Add(indentedLine);

            // 2. Comprobar si la línea actual abre un nuevo bloque para las líneas SUBSIGUIENTES
            if (!isEndProc)
            {
                if (ProcedureStartRegex.IsMatch(clean) && !DeclareFunctionRegex.IsMatch(clean))
                {
                    currentIndent++;
                }
                else if (TypeStartRegex.IsMatch(clean) && !clean.StartsWith("End Type", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndent++;
                }
                else if (EnumStartRegex.IsMatch(clean) && !clean.StartsWith("End Enum", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndent++;
                }
                else if (IsMultiLineIf(clean))
                {
                    currentIndent++;
                }
                else if (clean.StartsWith("Select Case", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndent++; // para el bloque Select
                    selectDepthStack.Push(0); // 0 = aún no hay Case abierto
                }
                else if (clean.StartsWith("Case ", StringComparison.OrdinalIgnoreCase) ||
                         clean.StartsWith("Case Else", StringComparison.OrdinalIgnoreCase))
                {
                    if (selectDepthStack.Count > 0)
                    {
                        // Marcamos que hay un Case activo y aumentamos indent para sus sentencias
                        selectDepthStack.Pop();
                        selectDepthStack.Push(1);
                        currentIndent++;
                    }
                }
                else if (clean.StartsWith("With ", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndent++;
                }
                else if (clean.StartsWith("For ", StringComparison.OrdinalIgnoreCase) ||
                         clean.StartsWith("For Each ", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndent++;
                }
                else if (clean.StartsWith("Do", StringComparison.OrdinalIgnoreCase))
                {
                    // Do, Do While ..., Do Until ...
                    if (clean.Equals("Do", StringComparison.OrdinalIgnoreCase) ||
                        clean.StartsWith("Do ", StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndent++;
                    }
                }
                else if (clean.StartsWith("While ", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndent++;
                }
            }

            // Continuación de línea con guión bajo ( _)
            isContinuation = clean.EndsWith(" _");
        }

        return string.Join("\r\n", outputLines);
    }

    /// <summary>
    /// Comprueba si una sentencia If es multilínea o de una sola línea.
    /// Si hay sentencias tras el Then (que no sean comentarios), es de una sola línea.
    /// </summary>
    private static bool IsMultiLineIf(string cleanLine)
    {
        if (!cleanLine.StartsWith("If ", StringComparison.OrdinalIgnoreCase) &&
            !cleanLine.StartsWith("If(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int thenIndex = cleanLine.LastIndexOf(" Then", StringComparison.OrdinalIgnoreCase);
        if (thenIndex < 0)
            return false;

        string afterThen = cleanLine.Substring(thenIndex + 5).Trim();
        // Si después de Then no hay nada o solo un comentario, es multilínea
        return string.IsNullOrEmpty(afterThen) || afterThen.StartsWith('\'');
    }

    private static string BuildIndentString(string indentUnit, int level)
    {
        if (level <= 0) return string.Empty;
        var sb = new StringBuilder(level * indentUnit.Length);
        for (int i = 0; i < level; i++)
        {
            sb.Append(indentUnit);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Alinea verticalmente las declaraciones consecutivas 'Dim / Public / Private ... As ...'
    /// </summary>
    public static string AlignAsInDeclarations(string code)
    {
        if (string.IsNullOrEmpty(code))
            return code;

        string[] lines = code.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>();

        int i = 0;
        while (i < lines.Length)
        {
            // Detectar bloque contiguo de declaraciones
            int j = i;
            var groupMatches = new List<(int lineIndex, string prefix, string varName, string asPart)>();

            while (j < lines.Length)
            {
                string line = lines[j];
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('\''))
                    break;

                var match = DeclarationLineRegex.Match(trimmed);
                if (match.Success)
                {
                    groupMatches.Add((j, match.Groups[1].Value.TrimEnd(), match.Groups[2].Value.Trim(), match.Groups[3].Value.Trim()));
                    j++;
                }
                else
                {
                    break;
                }
            }

            if (groupMatches.Count >= 2)
            {
                // Encontrar longitud máxima de 'prefix + varName'
                int maxPrefixVarLen = groupMatches.Max(g => $"{g.prefix} {g.varName}".Length);

                for (int k = 0; k < groupMatches.Count; k++)
                {
                    var item = groupMatches[k];
                    string originalLine = lines[item.lineIndex];
                    int leadingSpaces = originalLine.Length - originalLine.TrimStart().Length;
                    string indent = originalLine.Substring(0, leadingSpaces);

                    string combined = $"{item.prefix} {item.varName}";
                    int padNeeded = Math.Max(1, maxPrefixVarLen - combined.Length + 1);
                    string alignedLine = indent + combined + new string(' ', padNeeded) + item.asPart;

                    result.Add(alignedLine);
                }

                i = j;
            }
            else
            {
                result.Add(lines[i]);
                i++;
            }
        }

        return string.Join("\r\n", result);
    }

    /// <summary>
    /// Elimina comentarios e interiores de literales de cadena para analizar la sintaxis.
    /// </summary>
    public static string StripCommentsAndStrings(string line)
    {
        var sb = new StringBuilder(line.Length);
        bool inString = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inString = !inString;
                sb.Append('"');
                continue;
            }

            if (!inString)
            {
                if (c == '\'')
                {
                    break; // Comentario hasta el final de la línea
                }
                if (c == 'R' || c == 'r')
                {
                    if (i + 4 <= line.Length &&
                        line.Substring(i, 4).Equals("Rem ", StringComparison.OrdinalIgnoreCase) &&
                        (i == 0 || char.IsWhiteSpace(line[i - 1]) || line[i - 1] == ':'))
                    {
                        break;
                    }
                }
                sb.Append(c);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }
}
