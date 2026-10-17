using System;
using System.Collections.Generic;
using System.Linq;

namespace ViBa7.ProjectModel.Designers;

/// <summary>
/// Mapeador bidireccional entre la sintaxis interna de atajos de teclado de VB6 en archivos .frm
/// (ej. '^N', '{F5}', '+{F1}', '^+{F1}') y su representación textual legible en la interfaz (ej. 'Ctrl+N', 'F5', 'Shift+F1').
/// </summary>
public static class VbMenuShortcutHelper
{
    private static readonly Dictionary<string, string> DisplayToCode = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> CodeToDisplay = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> AvailableShortcuts { get; }

    static VbMenuShortcutHelper()
    {
        var list = new List<string> { "(None)" };

        void Add(string display, string code)
        {
            DisplayToCode[display] = code;
            CodeToDisplay[code] = display;
            list.Add(display);
        }

        // 1. Ctrl + [A..Z]
        for (char c = 'A'; c <= 'Z'; c++)
        {
            Add($"Ctrl+{c}", $"^{c}");
        }

        // 2. F1..F12
        for (int i = 1; i <= 12; i++)
        {
            Add($"F{i}", $"{{F{i}}}");
        }

        // 3. Ctrl + F1..F12
        for (int i = 1; i <= 12; i++)
        {
            Add($"Ctrl+F{i}", $"^{{F{i}}}");
        }

        // 4. Shift + F1..F12
        for (int i = 1; i <= 12; i++)
        {
            Add($"Shift+F{i}", $"+{{F{i}}}");
        }

        // 5. Ctrl + Shift + F1..F12
        for (int i = 1; i <= 12; i++)
        {
            Add($"Ctrl+Shift+F{i}", $"^+{{F{i}}}");
        }

        // 6. Del, Insert
        Add("Del", "{DEL}");
        Add("Shift+Del", "+{DEL}");
        Add("Ctrl+Del", "^{DEL}");
        Add("Ctrl+Ins", "^{INSERT}");
        Add("Shift+Ins", "+{INSERT}");

        AvailableShortcuts = list;
    }

    /// <summary>
    /// Convierte el código de atajo interno de VB6 a texto legible en la UI (ej. '^N' -> 'Ctrl+N').
    /// </summary>
    public static string ToDisplayText(string? vbpCode)
    {
        if (string.IsNullOrWhiteSpace(vbpCode)) return "(None)";
        string clean = vbpCode.Trim();
        if (CodeToDisplay.TryGetValue(clean, out string? display))
        {
            return display;
        }

        // Si ya viene formateado legiblemente (ej. "Ctrl+N")
        if (DisplayToCode.ContainsKey(clean))
        {
            return clean;
        }

        return clean;
    }

    /// <summary>
    /// Convierte el código a texto legible para InputGestureText de menús (vacío si no tiene atajo).
    /// </summary>
    public static string ToDisplayString(string? vbpCode)
    {
        string text = ToDisplayText(vbpCode);
        return text.Equals("(None)", StringComparison.OrdinalIgnoreCase) ? string.Empty : text;
    }

    /// <summary>
    /// Convierte el texto legible de la UI al código interno del archivo .frm (ej. 'Ctrl+N' -> '^N').
    /// </summary>
    public static string ToVbCode(string? displayText)
    {
        if (string.IsNullOrWhiteSpace(displayText) || displayText.Equals("(None)", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string clean = displayText.Trim();
        if (DisplayToCode.TryGetValue(clean, out string? code))
        {
            return code;
        }

        return clean;
    }
}
