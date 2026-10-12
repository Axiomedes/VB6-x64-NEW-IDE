using System;
using System.Collections.Generic;
using System.Text;
using VB7x64.ProjectModel.IntelliSense;

namespace VB7x64.ProjectModel.CodeBuilders;

/// <summary>
/// Formateador y corrector automático de líneas de código estilo Visual Basic 6.0 (Pretty-Printer).
/// Aplica mayúsculas canónicas a palabras clave, funciones intrínsecas y constantes,
/// normaliza espacios alrededor de operadores y tras comas, y cierra comillas de cadenas incompletas.
/// </summary>
public static class VB6LineFormatter
{
    private static readonly Dictionary<string, string> _canonicalWords = new(StringComparer.OrdinalIgnoreCase);

    static VB6LineFormatter()
    {
        InitializeCanonicalWords();
    }

    /// <summary>
    /// Formatea una línea completa de código VB6, respetando comentarios e indentación.
    /// </summary>
    public static string FormatLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return line;

        // 1. Extraer indentación inicial
        int indentLen = 0;
        while (indentLen < line.Length && (line[indentLen] == ' ' || line[indentLen] == '\t'))
        {
            indentLen++;
        }
        string indent = line[..indentLen];
        string content = line[indentLen..];

        // 2. Si la línea es un comentario puro (' o Rem), no se modifica
        if (content.StartsWith('\'') || content.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase) || content.Equals("Rem", StringComparison.OrdinalIgnoreCase))
        {
            return line;
        }

        // 3. Separar comentario al final si existe (fuera de comillas)
        string codePart;
        string commentPart = string.Empty;
        int commentIdx = FindCommentStart(content);
        if (commentIdx >= 0)
        {
            codePart = content[..commentIdx];
            commentPart = content[commentIdx..];
        }
        else
        {
            codePart = content;
        }

        // 4. Auto-cierre de comillas si hay un número impar de comillas en la parte de código
        codePart = AutoCloseQuotes(codePart);

        // 5. Tokenizar y formatear la parte de código
        string formattedCode = FormatCodePart(codePart);

        // 6. Recomponer la línea final con indentación y comentario
        if (!string.IsNullOrEmpty(commentPart))
        {
            if (formattedCode.Length > 0 && !formattedCode.EndsWith(' ') && !commentPart.StartsWith(' '))
            {
                return indent + formattedCode + " " + commentPart;
            }
            return indent + formattedCode + commentPart;
        }

        return indent + formattedCode;
    }

    private static int FindCommentStart(string text)
    {
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (c == '\'') return i;
                if ((i == 0 || char.IsWhiteSpace(text[i - 1])) &&
                    i + 3 < text.Length &&
                    text.Substring(i, 4).Equals("Rem ", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static string AutoCloseQuotes(string text)
    {
        int quoteCount = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '"') quoteCount++;
        }

        if (quoteCount % 2 != 0)
        {
            return text + "\"";
        }
        return text;
    }

    private enum TokenType
    {
        Identifier,
        Number,
        StringLiteral,
        DateLiteral,
        Operator,
        Comma,
        OpenParen,
        CloseParen,
        Dot,
        Colon,
        Semicolon,
        Exclamation,
        Other
    }

    private record struct FormatterToken(TokenType Type, string Text, bool IsUnary);

    private static string FormatCodePart(string code)
    {
        var tokens = Tokenize(code);
        if (tokens.Count == 0) return code;

        var sb = new StringBuilder();

        for (int i = 0; i < tokens.Count; i++)
        {
            var curr = tokens[i];
            var prev = i > 0 ? tokens[i - 1] : (FormatterToken?)null;

            string text = curr.Text;
            if (curr.Type == TokenType.Identifier)
            {
                text = GetCanonicalIdentifier(text);
            }

            if (prev.HasValue)
            {
                if (NeedsSpaceBetween(prev.Value, curr))
                {
                    sb.Append(' ');
                }
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    private static List<FormatterToken> Tokenize(string text)
    {
        var tokens = new List<FormatterToken>();
        int i = 0;
        int len = text.Length;

        while (i < len)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Literal de cadena: " ... "
            if (c == '"')
            {
                int start = i;
                i++;
                while (i < len)
                {
                    if (text[i] == '"')
                    {
                        i++;
                        if (i < len && text[i] == '"')
                        {
                            i++; // comilla escapada ""
                            continue;
                        }
                        break;
                    }
                    i++;
                }
                tokens.Add(new FormatterToken(TokenType.StringLiteral, text[start..i], false));
                continue;
            }

            // Literal de fecha: # ... #
            if (c == '#')
            {
                int start = i;
                i++;
                while (i < len && text[i] != '#') i++;
                if (i < len && text[i] == '#') i++;
                tokens.Add(new FormatterToken(TokenType.DateLiteral, text[start..i], false));
                continue;
            }

            if (c == ',')
            {
                tokens.Add(new FormatterToken(TokenType.Comma, ",", false));
                i++;
                continue;
            }
            if (c == '(')
            {
                tokens.Add(new FormatterToken(TokenType.OpenParen, "(", false));
                i++;
                continue;
            }
            if (c == ')')
            {
                tokens.Add(new FormatterToken(TokenType.CloseParen, ")", false));
                i++;
                continue;
            }
            if (c == '.')
            {
                tokens.Add(new FormatterToken(TokenType.Dot, ".", false));
                i++;
                continue;
            }
            if (c == ':')
            {
                if (i + 1 < len && text[i + 1] == '=')
                {
                    tokens.Add(new FormatterToken(TokenType.Operator, ":=", false));
                    i += 2;
                }
                else
                {
                    tokens.Add(new FormatterToken(TokenType.Colon, ":", false));
                    i++;
                }
                continue;
            }
            if (c == ';')
            {
                tokens.Add(new FormatterToken(TokenType.Semicolon, ";", false));
                i++;
                continue;
            }
            if (c == '!')
            {
                tokens.Add(new FormatterToken(TokenType.Exclamation, "!", false));
                i++;
                continue;
            }

            // Operadores relacionales de dos caracteres: <=, >=, <>
            if (c == '<')
            {
                if (i + 1 < len && (text[i + 1] == '=' || text[i + 1] == '>'))
                {
                    tokens.Add(new FormatterToken(TokenType.Operator, text.Substring(i, 2), false));
                    i += 2;
                }
                else
                {
                    tokens.Add(new FormatterToken(TokenType.Operator, "<", false));
                    i++;
                }
                continue;
            }
            if (c == '>')
            {
                if (i + 1 < len && text[i + 1] == '=')
                {
                    tokens.Add(new FormatterToken(TokenType.Operator, ">=", false));
                    i += 2;
                }
                else
                {
                    tokens.Add(new FormatterToken(TokenType.Operator, ">", false));
                    i++;
                }
                continue;
            }

            // Operadores de un carácter: =, +, -, *, /, \, ^, &
            if (c is '=' or '+' or '-' or '*' or '/' or '\\' or '^' or '&')
            {
                bool isUnary = false;
                if (c is '+' or '-')
                {
                    var prev = tokens.Count > 0 ? tokens[^1] : (FormatterToken?)null;
                    if (prev == null ||
                        prev.Value.Type is TokenType.Operator or TokenType.OpenParen or TokenType.Comma or TokenType.Colon)
                    {
                        isUnary = true;
                    }
                }
                tokens.Add(new FormatterToken(TokenType.Operator, c.ToString(), isUnary));
                i++;
                continue;
            }

            // Números: &H..., &O..., o numéricos decimales
            if (c == '&' && i + 1 < len && (text[i + 1] is 'H' or 'h' or 'O' or 'o'))
            {
                int start = i;
                i += 2;
                while (i < len && (char.IsLetterOrDigit(text[i]) || text[i] is '&' or '%')) i++;
                tokens.Add(new FormatterToken(TokenType.Number, text[start..i], false));
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                while (i < len && (char.IsDigit(text[i]) || text[i] == '.' || text[i] is 'e' or 'E' or '+' or '-' or '!' or '#' or '%' or '&' or '@'))
                {
                    if (text[i] is '+' or '-' && !(i > start && (text[i - 1] is 'e' or 'E')))
                    {
                        break;
                    }
                    i++;
                }
                tokens.Add(new FormatterToken(TokenType.Number, text[start..i], false));
                continue;
            }

            // Identificadores y palabras clave
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < len && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }
                // Sufijos de tipo en identificadores de VB6: %, &, !, #, @, $
                if (i < len && text[i] is '%' or '&' or '!' or '#' or '@' or '$')
                {
                    i++;
                }
                string word = text[start..i];
                tokens.Add(new FormatterToken(TokenType.Identifier, word, false));
                continue;
            }

            tokens.Add(new FormatterToken(TokenType.Other, c.ToString(), false));
            i++;
        }

        return tokens;
    }

    private static bool NeedsSpaceBetween(FormatterToken prev, FormatterToken curr)
    {
        // 1. Después de coma SIEMPRE espacio
        if (prev.Type == TokenType.Comma) return true;

        // 2. Antes de coma NUNCA espacio
        if (curr.Type == TokenType.Comma) return false;

        // 3. Antes de cerrar paréntesis NUNCA espacio
        if (curr.Type == TokenType.CloseParen) return false;

        // 4. Después de abrir paréntesis NUNCA espacio
        if (prev.Type == TokenType.OpenParen) return false;

        // 5. Alrededor de punto '.' NUNCA espacio
        if (prev.Type == TokenType.Dot || curr.Type == TokenType.Dot) return false;

        // 6. Alrededor de '!' (member access) NUNCA espacio
        if (prev.Type == TokenType.Exclamation || curr.Type == TokenType.Exclamation) return false;

        // 7. Dos paréntesis juntos: (( o ))
        if (prev.Type == TokenType.OpenParen && curr.Type == TokenType.OpenParen) return false;
        if (prev.Type == TokenType.CloseParen && curr.Type == TokenType.CloseParen) return false;

        // 8. Antes de abrir paréntesis '('
        if (curr.Type == TokenType.OpenParen)
        {
            if (prev.Type == TokenType.Operator) return true;
            if (prev.Type == TokenType.Identifier && IsControlKeyword(prev.Text)) return true;
            return false;
        }

        // 9. Operadores
        if (curr.Type == TokenType.Operator)
        {
            if (curr.IsUnary)
            {
                if (prev.Type is TokenType.Identifier or TokenType.Number or TokenType.CloseParen) return true;
                if (prev.Type == TokenType.Operator) return true;
                return false;
            }
            return true;
        }

        if (prev.Type == TokenType.Operator)
        {
            if (prev.IsUnary) return false;
            return true;
        }

        // 10. Después de dos puntos ':' en sentencias múltiples
        if (prev.Type == TokenType.Colon) return true;
        if (curr.Type == TokenType.Colon) return false;

        // 11. Punto y coma ';' en Print
        if (prev.Type == TokenType.Semicolon) return true;
        if (curr.Type == TokenType.Semicolon) return false;

        // 12. Identificadores, números y literales entre sí
        if (prev.Type is TokenType.Identifier or TokenType.Number or TokenType.StringLiteral or TokenType.DateLiteral or TokenType.CloseParen &&
            curr.Type is TokenType.Identifier or TokenType.Number or TokenType.StringLiteral or TokenType.DateLiteral)
        {
            return true;
        }

        return false;
    }

    private static bool IsControlKeyword(string word)
    {
        return word.Equals("If", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("While", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("Until", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("Case", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("Select", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("And", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("Or", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("Xor", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("Not", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCanonicalIdentifier(string word)
    {
        string baseWord = word.TrimEnd('%', '&', '!', '#', '@', '$');
        string suffix = word.Length > baseWord.Length ? word[baseWord.Length..] : string.Empty;

        if (_canonicalWords.TryGetValue(baseWord, out var canonical))
        {
            return canonical + suffix;
        }
        return word;
    }

    private static void InitializeCanonicalWords()
    {
        // 1. Añadir símbolos de catálogo de runtime de VB6 (funciones, constantes, tipos, palabras clave)
        foreach (var sym in VbRuntimeCatalog.GetAllGlobalSymbols())
        {
            _canonicalWords[sym.Name] = sym.Name;
        }

        // 2. Palabras clave adicionales y compuestas de VB6
        string[] extraKeywords =
        [
            "As", "ByVal", "ByRef", "Optional", "ParamArray", "Is", "Not", "And", "Or", "Xor", "Eqv", "Imp",
            "Mod", "Like", "Dim", "ReDim", "Preserve", "Set", "Let", "Sub", "Function", "Property", "Get",
            "If", "Then", "Else", "ElseIf", "End", "Select", "Case", "For", "To", "Step", "Next", "Each",
            "In", "Do", "While", "Until", "Loop", "Wend", "With", "Exit", "On", "Error", "GoTo", "GoSub",
            "Return", "Resume", "Option", "Explicit", "Base", "Compare", "Binary", "Text", "Private", "Public",
            "Friend", "Global", "Static", "Const", "Type", "Enum", "Event", "RaiseEvent", "Implements",
            "Call", "New", "Nothing", "True", "False", "Debug", "Print", "Open", "Close", "Input", "Output",
            "Append", "Random", "Access", "Read", "Write", "Lock", "Shared", "Line", "Seek", "Loc", "Lof",
            "Eof", "FreeFile", "Put", "Kill", "Name", "MkDir", "RmDir", "ChDir", "ChDrive", "FileCopy",
            "SetAttr", "GetAttr", "FileDateTime", "FileLen", "Width", "AppActivate", "SendKeys", "SaveSetting",
            "GetSetting", "DeleteSetting", "GetAllSettings", "Load", "Unload", "Me", "Stop", "IIf",
            "Caption", "Text", "Enabled", "Visible", "Left", "Top", "Height", "Tag", "ToolTipText",
            "BackColor", "ForeColor", "Font", "FontSize", "FontBold", "FontItalic", "FontUnderline",
            "FontName", "Index", "TabIndex", "TabStop", "MousePointer", "MouseIcon", "Value", "Interval",
            "ListIndex", "ListCount", "ItemData", "SelStart", "SelLength", "SelText", "MultiLine",
            "ScrollBars", "Alignment", "BorderStyle", "Appearance", "Picture", "Icon", "Clear", "AddItem",
            "RemoveItem", "SetFocus", "Refresh", "Cls", "Move", "ZOrder", "Show", "Hide"
        ];

        foreach (var kw in extraKeywords)
        {
            _canonicalWords[kw] = kw;
        }
    }
}
