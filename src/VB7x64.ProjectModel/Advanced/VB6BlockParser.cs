using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Advanced;

public static class VB6BlockParser
{
    private static readonly Regex ProcedureStartRegex = new(
        @"^\s*(?:(?:Public|Private|Friend|Static)\s+)?(?:(Sub|Function)\s+([a-zA-Z0-9_]+)|(Property)\s+(?:Get|Let|Set)\s+([a-zA-Z0-9_]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeOrEnumStartRegex = new(
        @"^\s*(?:(?:Public|Private)\s+)?(Type|Enum)\s+([a-zA-Z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<VB6Block> ParseBlocks(string code)
    {
        var result = new List<VB6Block>();
        if (string.IsNullOrWhiteSpace(code))
            return result;

        string[] rawLines = code.Replace("\r\n", "\n").Split('\n');
        var blockStack = new Stack<VB6Block>();

        for (int i = 0; i < rawLines.Length; i++)
        {
            int lineNumber = i + 1; // 1-indexed
            string rawLine = rawLines[i];
            string trimmed = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Quitar comentarios y cadenas para analizar tokens
            string cleanLine = StripCommentsAndStrings(rawLine);
            string cleanTrimmed = cleanLine.Trim();

            if (string.IsNullOrWhiteSpace(cleanTrimmed))
                continue;

            int column = GetIndentColumn(rawLine);

            // 1. Detectar cierres de bloques
            if (TryMatchBlockClose(cleanTrimmed, out string closeType))
            {
                if (blockStack.Count > 0)
                {
                    // Buscar en el stack si el tipo coincide o cerrar hasta el más cercano coincidente
                    if (CanClose(blockStack.Peek().BlockType, closeType))
                    {
                        var block = blockStack.Pop();
                        block.EndLine = lineNumber;
                        if (block.Parent == null)
                        {
                            result.Add(block);
                        }
                    }
                    else
                    {
                        // Intentar desenrollar si hubo un bloque no cerrado previamente
                        var tempStack = new Stack<VB6Block>();
                        bool found = false;
                        while (blockStack.Count > 0)
                        {
                            var top = blockStack.Pop();
                            if (CanClose(top.BlockType, closeType))
                            {
                                top.EndLine = lineNumber;
                                if (top.Parent == null) result.Add(top);
                                found = true;
                                break;
                            }
                            tempStack.Push(top);
                        }

                        if (!found)
                        {
                            // Restaurar stack
                            while (tempStack.Count > 0) blockStack.Push(tempStack.Pop());
                        }
                    }
                }
                continue;
            }

            // 2. Detectar aperturas de bloques
            if (TryMatchBlockOpen(rawLine, cleanTrimmed, column, lineNumber, out VB6Block? newBlock) && newBlock != null)
            {
                if (blockStack.Count > 0)
                {
                    var parent = blockStack.Peek();
                    newBlock.Parent = parent;
                    parent.Children.Add(newBlock);
                }
                blockStack.Push(newBlock);
            }
        }

        // Si quedaron bloques abiertos al final del archivo, asignarles EndLine = total líneas
        while (blockStack.Count > 0)
        {
            var unclosed = blockStack.Pop();
            unclosed.EndLine = rawLines.Length;
            if (unclosed.Parent == null)
            {
                result.Add(unclosed);
            }
        }

        return result;
    }

    private static bool TryMatchBlockOpen(string rawLine, string cleanLine, int column, int lineNumber, out VB6Block? block)
    {
        block = null;
        string upper = cleanLine.ToUpperInvariant();

        // A. Multi-line If ... Then
        if (upper.StartsWith("IF ", StringComparison.Ordinal) || upper == "IF")
        {
            // Solo es multi-line If si 'Then' es lo último en cleanLine
            if (upper.EndsWith(" THEN", StringComparison.Ordinal) || upper.EndsWith("THEN", StringComparison.Ordinal))
            {
                block = new VB6Block
                {
                    StartLine = lineNumber,
                    StartColumn = column,
                    BlockType = "If",
                    Header = rawLine.Trim()
                };
                return true;
            }
            return false;
        }

        // B. Select Case ...
        if (upper.StartsWith("SELECT CASE", StringComparison.Ordinal))
        {
            block = new VB6Block
            {
                StartLine = lineNumber,
                StartColumn = column,
                BlockType = "Select",
                Header = rawLine.Trim()
            };
            return true;
        }

        // C. For ... / For Each ...
        if (upper.StartsWith("FOR ", StringComparison.Ordinal) || upper.StartsWith("FOR EACH ", StringComparison.Ordinal))
        {
            block = new VB6Block
            {
                StartLine = lineNumber,
                StartColumn = column,
                BlockType = "For",
                Header = rawLine.Trim()
            };
            return true;
        }

        // D. Do ... / Do While ... / Do Until ...
        if (upper == "DO" || upper.StartsWith("DO ", StringComparison.Ordinal))
        {
            block = new VB6Block
            {
                StartLine = lineNumber,
                StartColumn = column,
                BlockType = "Do",
                Header = rawLine.Trim()
            };
            return true;
        }

        // E. While ...
        if (upper.StartsWith("WHILE ", StringComparison.Ordinal))
        {
            block = new VB6Block
            {
                StartLine = lineNumber,
                StartColumn = column,
                BlockType = "While",
                Header = rawLine.Trim()
            };
            return true;
        }

        // F. With ...
        if (upper.StartsWith("WITH ", StringComparison.Ordinal))
        {
            block = new VB6Block
            {
                StartLine = lineNumber,
                StartColumn = column,
                BlockType = "With",
                Header = rawLine.Trim()
            };
            return true;
        }

        // G. Procedimientos: Sub, Function, Property
        var procMatch = ProcedureStartRegex.Match(rawLine);
        if (procMatch.Success)
        {
            // Evitar 'Declare Sub' o 'Declare Function'
            if (!upper.Contains("DECLARE "))
            {
                string bType = !string.IsNullOrEmpty(procMatch.Groups[1].Value)
                    ? procMatch.Groups[1].Value
                    : "Property";

                block = new VB6Block
                {
                    StartLine = lineNumber,
                    StartColumn = column,
                    BlockType = char.ToUpperInvariant(bType[0]) + bType[1..].ToLowerInvariant(),
                    Header = rawLine.Trim()
                };
                return true;
            }
        }

        // H. Type y Enum
        var typeMatch = TypeOrEnumStartRegex.Match(rawLine);
        if (typeMatch.Success)
        {
            string bType = typeMatch.Groups[1].Value;
            block = new VB6Block
            {
                StartLine = lineNumber,
                StartColumn = column,
                BlockType = char.ToUpperInvariant(bType[0]) + bType[1..].ToLowerInvariant(),
                Header = rawLine.Trim()
            };
            return true;
        }

        return false;
    }

    private static bool TryMatchBlockClose(string cleanLine, out string closeType)
    {
        closeType = string.Empty;
        string upper = cleanLine.ToUpperInvariant();

        if (upper == "END IF")
        {
            closeType = "If";
            return true;
        }
        if (upper == "END SELECT")
        {
            closeType = "Select";
            return true;
        }
        if (upper == "NEXT" || upper.StartsWith("NEXT ", StringComparison.Ordinal))
        {
            closeType = "For";
            return true;
        }
        if (upper == "LOOP" || upper.StartsWith("LOOP ", StringComparison.Ordinal))
        {
            closeType = "Do";
            return true;
        }
        if (upper == "WEND")
        {
            closeType = "While";
            return true;
        }
        if (upper == "END WITH")
        {
            closeType = "With";
            return true;
        }
        if (upper == "END SUB")
        {
            closeType = "Sub";
            return true;
        }
        if (upper == "END FUNCTION")
        {
            closeType = "Function";
            return true;
        }
        if (upper == "END PROPERTY")
        {
            closeType = "Property";
            return true;
        }
        if (upper == "END TYPE")
        {
            closeType = "Type";
            return true;
        }
        if (upper == "END ENUM")
        {
            closeType = "Enum";
            return true;
        }

        return false;
    }

    private static bool CanClose(string openedType, string closeType)
    {
        return string.Equals(openedType, closeType, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetIndentColumn(string rawLine)
    {
        int col = 0;
        foreach (char c in rawLine)
        {
            if (c == ' ') col++;
            else if (c == '\t') col += 4;
            else break;
        }
        return col;
    }

    private static string StripCommentsAndStrings(string line)
    {
        var chars = line.ToCharArray();
        bool inQuotes = false;
        int len = chars.Length;

        for (int i = 0; i < len; i++)
        {
            char c = chars[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                chars[i] = ' ';
            }
            else if (inQuotes)
            {
                chars[i] = ' ';
            }
            else if (c == '\'')
            {
                // El resto es comentario
                return new string(chars, 0, i);
            }
            else if ((i == 0 || char.IsWhiteSpace(chars[i - 1])) &&
                     i + 3 <= len &&
                     (chars[i] == 'R' || chars[i] == 'r'))
            {
                if (string.Equals(line.Substring(i, 3), "Rem", StringComparison.OrdinalIgnoreCase) &&
                    (i + 3 == len || char.IsWhiteSpace(chars[i + 3])))
                {
                    return new string(chars, 0, i);
                }
            }
        }

        return new string(chars);
    }
}
