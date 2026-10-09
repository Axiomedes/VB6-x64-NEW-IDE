using System;
using System.Collections.Generic;
using System.Text;

namespace VB7x64.ProjectModel.CodeBuilders;

public class SnippetDefinition
{
    public string Shortcut { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Template { get; set; } = "";
}

public static class SnippetManager
{
    private static readonly List<SnippetDefinition> DefaultSnippets =
    [
        new()
        {
            Shortcut = "ife",
            Title = "If ... Else ... End If",
            Description = "Estructura condicional completa",
            Template = "If $CARET$ Then\r\n$INDENT$    \r\n$INDENT$Else\r\n$INDENT$    \r\n$INDENT$End If"
        },
        new()
        {
            Shortcut = "if",
            Title = "If ... End If",
            Description = "Estructura condicional simple",
            Template = "If $CARET$ Then\r\n$INDENT$    \r\n$INDENT$End If"
        },
        new()
        {
            Shortcut = "sel",
            Title = "Select Case ... End Select",
            Description = "Estructura de bifurcación múltiple",
            Template = "Select Case $CARET$\r\n$INDENT$    Case 1\r\n$INDENT$        \r\n$INDENT$    Case Else\r\n$INDENT$        \r\n$INDENT$End Select"
        },
        new()
        {
            Shortcut = "for",
            Title = "For ... Next",
            Description = "Bucle de conteo numérico",
            Template = "For i = 0 To $CARET$\r\n$INDENT$    \r\n$INDENT$Next i"
        },
        new()
        {
            Shortcut = "fore",
            Title = "For Each ... Next",
            Description = "Bucle de iteración de colección",
            Template = "For Each item In $CARET$\r\n$INDENT$    \r\n$INDENT$Next item"
        },
        new()
        {
            Shortcut = "dow",
            Title = "Do While ... Loop",
            Description = "Bucle condicional Do While",
            Template = "Do While $CARET$\r\n$INDENT$    \r\n$INDENT$Loop"
        },
        new()
        {
            Shortcut = "dou",
            Title = "Do Until ... Loop",
            Description = "Bucle condicional Do Until",
            Template = "Do Until $CARET$\r\n$INDENT$    \r\n$INDENT$Loop"
        },
        new()
        {
            Shortcut = "whi",
            Title = "While ... Wend",
            Description = "Bucle clásico While ... Wend",
            Template = "While $CARET$\r\n$INDENT$    \r\n$INDENT$Wend"
        },
        new()
        {
            Shortcut = "with",
            Title = "With ... End With",
            Description = "Bloque de ámbito de objeto With",
            Template = "With $CARET$\r\n$INDENT$    \r\n$INDENT$End With"
        },
        new()
        {
            Shortcut = "prop",
            Title = "Property Get / Let",
            Description = "Par de propiedad estándar de lectura/escritura",
            Template = "Public Property Get $CARET$() As Variant\r\n$INDENT$    \r\n$INDENT$End Property\r\n$INDENT$\r\n$INDENT$Public Property Let PropertyName(ByVal value As Variant)\r\n$INDENT$    \r\n$INDENT$End Property"
        },
        new()
        {
            Shortcut = "sub",
            Title = "Sub ... End Sub",
            Description = "Procedimiento Sub estándar",
            Template = "Private Sub $CARET$()\r\n$INDENT$    \r\n$INDENT$End Sub"
        },
        new()
        {
            Shortcut = "fn",
            Title = "Function ... End Function",
            Description = "Función estándar con retorno",
            Template = "Private Function $CARET$() As Boolean\r\n$INDENT$    \r\n$INDENT$End Function"
        },
        new()
        {
            Shortcut = "err",
            Title = "Manejo de Errores (On Error GoTo)",
            Description = "Bloque estructurado estándar de control de excepciones",
            Template = "On Error GoTo ErrorHandler\r\n$INDENT$\r\n$INDENT$$CARET$\r\n$INDENT$\r\n$INDENT$Exit Sub\r\n$INDENT$\r\n$INDENT$ErrorHandler:\r\n$INDENT$    MsgBox \"Error \" & Err.Number & \": \" & Err.Description, vbCritical"
        }
    ];

    public static IReadOnlyList<SnippetDefinition> GetAllSnippets() => DefaultSnippets;

    public static SnippetDefinition? FindSnippet(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return null;
        string key = shortcut.Trim().ToLowerInvariant();
        return DefaultSnippets.Find(s => string.Equals(s.Shortcut, key, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryExpandSnippet(string shortcut, string indent, out string expandedText, out int caretRelativeOffset)
    {
        expandedText = string.Empty;
        caretRelativeOffset = 0;

        var snippet = FindSnippet(shortcut);
        if (snippet == null)
            return false;

        string template = snippet.Template;
        template = template.Replace("$INDENT$", indent);

        int caretIdx = template.IndexOf("$CARET$", StringComparison.Ordinal);
        if (caretIdx >= 0)
        {
            caretRelativeOffset = caretIdx;
            expandedText = template.Remove(caretIdx, "$CARET$".Length);
        }
        else
        {
            expandedText = template;
            caretRelativeOffset = expandedText.Length;
        }

        return true;
    }
}
