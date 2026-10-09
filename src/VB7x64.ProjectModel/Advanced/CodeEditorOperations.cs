using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Advanced;

public enum TextCaseMode
{
    Upper,
    Lower,
    Title
}

public static class CodeEditorOperations
{
    /// <summary>
    /// Comenta una o más líneas anteponiendo "' ".
    /// Si una línea ya está indentada, coloca el apóstrofe en la columna de indentación.
    /// Si la línea está vacía, no la altera.
    /// </summary>
    public static string CommentLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        int indentLen = 0;
        while (indentLen < line.Length && (line[indentLen] == ' ' || line[indentLen] == '\t'))
        {
            indentLen++;
        }

        string indent = line[..indentLen];
        string content = line[indentLen..];
        return $"{indent}' {content}";
    }

    /// <summary>
    /// Comenta un bloque de texto línea por línea respetando los saltos de línea originales.
    /// </summary>
    public static string CommentText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var lines = SplitLines(text, out string delimiter);
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            result.Add(CommentLine(line));
        }

        return string.Join(delimiter, result);
    }

    /// <summary>
    /// Descomenta una línea retirando el "' " o "'" que encabeza el código.
    /// </summary>
    public static string UncommentLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        int indentLen = 0;
        while (indentLen < line.Length && (line[indentLen] == ' ' || line[indentLen] == '\t'))
        {
            indentLen++;
        }

        string indent = line[..indentLen];
        string content = line[indentLen..];

        if (content.StartsWith("' ", StringComparison.Ordinal))
        {
            return indent + content[2..];
        }
        if (content.StartsWith("'", StringComparison.Ordinal))
        {
            return indent + content[1..];
        }
        if (content.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
        {
            return indent + content[4..];
        }

        return line;
    }

    /// <summary>
    /// Descomenta un bloque de texto línea por línea respetando los saltos de línea originales.
    /// </summary>
    public static string UncommentText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var lines = SplitLines(text, out string delimiter);
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            result.Add(UncommentLine(line));
        }

        return string.Join(delimiter, result);
    }

    /// <summary>
    /// Invierte una asignación VB6 del tipo 'VariableA = VariableB' a 'VariableB = VariableA'.
    /// Preserva la indentación inicial y cualquier comentario al final de la línea.
    /// Ignora comparaciones lógicas (&lt;=, &gt;=, &lt;&gt;, ==) o líneas sin signo '=' fuera de comillas.
    /// </summary>
    public static string SwitchAssignmentLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        int indentLen = 0;
        while (indentLen < line.Length && (line[indentLen] == ' ' || line[indentLen] == '\t'))
        {
            indentLen++;
        }

        string indent = line[..indentLen];
        string trimmed = line[indentLen..];

        // Separar comentario al final si existe
        int commentIndex = FindCommentStart(trimmed);
        string codePart = commentIndex >= 0 ? trimmed[..commentIndex] : trimmed;
        string commentPart = commentIndex >= 0 ? trimmed[commentIndex..] : "";

        // Buscar el signo '=' de asignación fuera de comillas
        int eqIndex = FindAssignmentEqualsIndex(codePart);
        if (eqIndex < 0)
        {
            return line; // No es una asignación válida
        }

        string leftPart = codePart[..eqIndex].Trim();
        string rightPart = codePart[(eqIndex + 1)..].Trim();

        if (string.IsNullOrEmpty(leftPart) || string.IsNullOrEmpty(rightPart))
        {
            return line;
        }

        // Si leftPart empieza con 'Set ' o 'Let ', conservar la palabra clave con la nueva asignación si aplica
        string prefix = "";
        if (leftPart.StartsWith("Set ", StringComparison.OrdinalIgnoreCase))
        {
            prefix = leftPart[..4];
            leftPart = leftPart[4..].Trim();
        }
        else if (leftPart.StartsWith("Let ", StringComparison.OrdinalIgnoreCase))
        {
            prefix = leftPart[..4];
            leftPart = leftPart[4..].Trim();
        }

        string swappedCode = $"{prefix}{rightPart} = {leftPart}";
        if (!string.IsNullOrEmpty(commentPart))
        {
            swappedCode += " " + commentPart.TrimStart();
        }

        return indent + swappedCode;
    }

    /// <summary>
    /// Invierte asignaciones en un bloque de texto completo línea a línea.
    /// </summary>
    public static string SwitchAssignments(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var lines = SplitLines(text, out string delimiter);
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            result.Add(SwitchAssignmentLine(line));
        }

        return string.Join(delimiter, result);
    }

    /// <summary>
    /// Modifica las mayúsculas/minúsculas de un texto según el modo especificado.
    /// </summary>
    public static string ChangeCase(string text, TextCaseMode mode)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return mode switch
        {
            TextCaseMode.Upper => text.ToUpperInvariant(),
            TextCaseMode.Lower => text.ToLowerInvariant(),
            TextCaseMode.Title => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
            _ => text
        };
    }

    private static int FindCommentStart(string s)
    {
        bool inQuotes = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (c == '\'')
                    return i;
                if ((i == 0 || char.IsWhiteSpace(s[i - 1])) &&
                    i + 3 <= s.Length &&
                    s.Substring(i, 3).Equals("Rem", StringComparison.OrdinalIgnoreCase) &&
                    (i + 3 == s.Length || char.IsWhiteSpace(s[i + 3])))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static int FindAssignmentEqualsIndex(string code)
    {
        bool inQuotes = false;
        int foundIndex = -1;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes) continue;

            if (c == '=')
            {
                // Verificar que no sea parte de <=, >=, <>, ==
                if (i > 0 && (code[i - 1] == '<' || code[i - 1] == '>' || code[i - 1] == '='))
                    continue;
                if (i + 1 < code.Length && (code[i + 1] == '=' || code[i + 1] == '>'))
                    continue;

                if (foundIndex != -1)
                {
                    // Si hay múltiples signos '=' en la misma línea (por ejemplo If A = B Then C = D),
                    // no es una asignación simple segura para invertir.
                    return -1;
                }

                foundIndex = i;
            }
        }

        return foundIndex;
    }

    private static string[] SplitLines(string text, out string delimiter)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal))
        {
            delimiter = "\r\n";
            return text.Split("\r\n");
        }
        if (text.Contains('\n', StringComparison.Ordinal))
        {
            delimiter = "\n";
            return text.Split('\n');
        }
        delimiter = "\r\n";
        return [text];
    }
}
