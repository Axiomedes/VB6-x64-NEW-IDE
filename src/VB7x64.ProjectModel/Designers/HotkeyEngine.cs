using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Designers;

public class HotkeyItem
{
    public string ControlName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string OriginalCaption { get; set; } = string.Empty;
    public string NewCaption { get; set; } = string.Empty;
    public char? HotkeyChar { get; set; }
    public bool HasConflict { get; set; }
    public int ConflictCount { get; set; }

    public string DisplayType => TypeName.StartsWith("VB.", StringComparison.OrdinalIgnoreCase)
        ? TypeName.Substring(3)
        : TypeName;

    public string DisplayHotkey => HotkeyChar.HasValue ? $"Alt+{HotkeyChar.Value}" : "(Sin tecla)";

    public string StatusBadge => HasConflict 
        ? "⚠️ Conflicto" 
        : (HotkeyChar.HasValue ? "✅ Válido" : "ℹ️ Sin tecla");
}

public static class HotkeyEngine
{
    private static readonly Regex BeginControlRegex = new(
        @"^\s*Begin\s+([a-zA-Z0-9_.]+)\s+([a-zA-Z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndBlockRegex = new(
        @"^\s*End\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CaptionRegex = new(
        @"^\s*Caption\s*=\s*""(.*)""",
        RegexOptions.Compiled);

    /// <summary>
    /// Extrae todos los controles con Caption y analiza sus aceleradores (teclas de acceso rápido Alt+Letra).
    /// </summary>
    public static List<HotkeyItem> ParseHotkeys(string headerAndDesign)
    {
        var items = new List<HotkeyItem>();
        if (string.IsNullOrWhiteSpace(headerAndDesign))
            return items;

        string[] lines = headerAndDesign.Replace("\r\n", "\n").Split('\n');
        var controlStack = new Stack<(string TypeName, string CtrlName)>();
        int depth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            var beginMatch = BeginControlRegex.Match(trimmed);
            if (beginMatch.Success)
            {
                depth++;
                string typeName = beginMatch.Groups[1].Value.Trim();
                string ctrlName = beginMatch.Groups[2].Value.Trim();
                controlStack.Push((typeName, ctrlName));
                continue;
            }

            if (EndBlockRegex.IsMatch(trimmed))
            {
                if (depth > 0) depth--;
                if (controlStack.Count > 0)
                {
                    controlStack.Pop();
                }
                continue;
            }

            if (controlStack.Count > 0)
            {
                var (currentTypeName, currentCtrlName) = controlStack.Peek();

                // Ignorar el formulario raíz principal (VB.Form, VB.MDIForm, etc.)
                if (depth == 1 && (currentTypeName.Equals("VB.Form", StringComparison.OrdinalIgnoreCase) ||
                                  currentTypeName.Equals("VB.MDIForm", StringComparison.OrdinalIgnoreCase) ||
                                  currentTypeName.Equals("VB.UserControl", StringComparison.OrdinalIgnoreCase) ||
                                  currentTypeName.Equals("VB.PropertyPage", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var captionMatch = CaptionRegex.Match(trimmed);
                if (captionMatch.Success)
                {
                    string caption = captionMatch.Groups[1].Value;
                    char? hotkey = ExtractHotkey(caption);

                    items.Add(new HotkeyItem
                    {
                        ControlName = currentCtrlName,
                        TypeName = currentTypeName,
                        OriginalCaption = caption,
                        NewCaption = caption,
                        HotkeyChar = hotkey
                    });
                }
            }
        }

        // Detectar conflictos (teclas duplicadas en el mismo formulario)
        var hotkeyGroups = items
            .Where(item => item.HotkeyChar.HasValue)
            .GroupBy(item => item.HotkeyChar!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var item in items)
        {
            if (item.HotkeyChar.HasValue && hotkeyGroups.TryGetValue(item.HotkeyChar.Value, out int count) && count > 1)
            {
                item.HasConflict = true;
                item.ConflictCount = count;
            }
        }

        return items;
    }

    /// <summary>
    /// Extrae el carácter acelerador marcado con '&' (omitiendo '&&' que representa un ampersand literal).
    /// </summary>
    public static char? ExtractHotkey(string caption)
    {
        if (string.IsNullOrEmpty(caption)) return null;

        for (int i = 0; i < caption.Length - 1; i++)
        {
            if (caption[i] == '&')
            {
                if (caption[i + 1] == '&')
                {
                    i++; // Ampersand escapado "&&"
                    continue;
                }

                char next = caption[i + 1];
                if (char.IsLetterOrDigit(next))
                {
                    return char.ToUpperInvariant(next);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Obtiene la lista de letras de la A a la Z que no están utilizadas por ningún control.
    /// </summary>
    public static List<char> GetAvailableLetters(List<HotkeyItem> items)
    {
        var used = new HashSet<char>(
            items.Where(i => i.HotkeyChar.HasValue).Select(i => i.HotkeyChar!.Value));

        var available = new List<char>();
        for (char c = 'A'; c <= 'Z'; c++)
        {
            if (!used.Contains(c))
            {
                available.Add(c);
            }
        }

        return available;
    }

    /// <summary>
    /// Aplica las modificaciones de Caption basadas en una colección de HotkeyItem a la sección de cabecera visual del archivo .frm.
    /// </summary>
    public static string ApplyCaptions(string headerAndDesign, IEnumerable<HotkeyItem> items)
    {
        if (items == null) return headerAndDesign;
        var map = items
            .Where(i => !string.IsNullOrEmpty(i.ControlName) && !string.Equals(i.OriginalCaption, i.NewCaption, StringComparison.Ordinal))
            .ToDictionary(i => i.ControlName, i => i.NewCaption);
        return ApplyCaptions(headerAndDesign, map);
    }

    /// <summary>
    /// Aplica las modificaciones de Caption de vuelta a la sección de cabecera visual del archivo .frm.
    /// </summary>
    public static string ApplyCaptions(string headerAndDesign, Dictionary<string, string> newCaptionsMap)
    {
        if (string.IsNullOrWhiteSpace(headerAndDesign) || newCaptionsMap == null || newCaptionsMap.Count == 0)
            return headerAndDesign;

        string[] lines = headerAndDesign.Replace("\r\n", "\n").Split('\n');
        var resultLines = new List<string>();
        string? currentCtrlName = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            var beginMatch = BeginControlRegex.Match(trimmed);
            if (beginMatch.Success)
            {
                currentCtrlName = beginMatch.Groups[2].Value.Trim();
                resultLines.Add(line);
                continue;
            }

            if (EndBlockRegex.IsMatch(trimmed))
            {
                currentCtrlName = null;
                resultLines.Add(line);
                continue;
            }

            if (currentCtrlName != null && newCaptionsMap.TryGetValue(currentCtrlName, out string? newCaption))
            {
                if (trimmed.StartsWith("Caption", StringComparison.OrdinalIgnoreCase))
                {
                    int leadingSpaces = line.Length - line.TrimStart().Length;
                    string indent = leadingSpaces > 0 ? line.Substring(0, leadingSpaces) : "      ";
                    resultLines.Add($"{indent}Caption         =   \"{newCaption}\"");
                    continue;
                }
            }

            resultLines.Add(line);
        }

        return string.Join("\r\n", resultLines);
    }
}
