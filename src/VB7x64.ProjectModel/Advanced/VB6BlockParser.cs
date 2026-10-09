using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Advanced;

public static class VB6BlockParser
{
    private static readonly Regex ProcedureStartRegex = new(
        @"^\s*(?:(?:Public|Private|Friend|Static)\s+)?(?:(Sub|Function)\s+([a-zA-Z0-9_]+)|(Property)\s+(?:Get|Let|Set)\s+([a-zA-Z0-9_]+))",
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

            string cleanLine = StripCommentsAndStrings(rawLine);
            string cleanTrimmed = cleanLine.Trim();

            if (string.IsNullOrWhiteSpace(cleanTrimmed))
                continue;

            int column = GetIndentColumn(rawLine);

            // 1. Detectar si inicia un nuevo procedimiento (Sub, Function, Property)
            if (TryMatchProcedureOpen(rawLine, cleanTrimmed, column, lineNumber, out VB6Block? procBlock) && procBlock != null)
            {
                // VB6 no permite procedimientos anidados. Si había un procedimiento abierto sin cerrar, lo descartamos
                if (blockStack.Count > 0)
                {
                    blockStack.Clear();
                }
                blockStack.Push(procBlock);
                continue;
            }

            // Si no estamos dentro de un procedimiento, no procesamos bloques (las declaraciones generales no llevan guías)
            if (blockStack.Count == 0)
                continue;

            // 2. Detectar cierres de bloques
            if (TryMatchBlockClose(cleanTrimmed, out string closeType))
            {
                if (IsProcedureType(closeType))
                {
                    // Cierre del procedimiento raíz: desenrollar bloques internos y cerrar el procedimiento
                    while (blockStack.Count > 1)
                    {
                        blockStack.Pop();
                    }

                    if (blockStack.Count == 1 && CanClose(blockStack.Peek().BlockType, closeType))
                    {
                        var rootProc = blockStack.Pop();
                        rootProc.EndLine = lineNumber;
                        result.Add(rootProc);
                    }
                    else
                    {
                        blockStack.Clear();
                    }
                }
                else
                {
                    // Cierre de bloque interno (If, Select, For, Do, While, With)
                    if (blockStack.Count > 1 && CanClose(blockStack.Peek().BlockType, closeType))
                    {
                        var innerBlock = blockStack.Pop();
                        innerBlock.EndLine = lineNumber;
                    }
                    else if (blockStack.Count > 2)
                    {
                        // Intentar desenrollar si hubo un bloque anidado no cerrado, pero sin desapilar el procedimiento raíz
                        var tempStack = new Stack<VB6Block>();
                        bool found = false;
                        while (blockStack.Count > 1)
                        {
                            var top = blockStack.Pop();
                            if (CanClose(top.BlockType, closeType))
                            {
                                top.EndLine = lineNumber;
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

            // 3. Detectar ramas intermedias (Else, ElseIf, Case)
            if (blockStack.Count > 1 && TryMatchBlockIntermediate(cleanTrimmed, blockStack.Peek().BlockType))
            {
                blockStack.Peek().IntermediateLines.Add(lineNumber);
                continue;
            }

            // 4. Detectar aperturas de bloques internos (If, Select, For, Do, While, With)
            if (TryMatchInternalBlockOpen(rawLines, i, column, lineNumber, out VB6Block? innerOpenBlock) && innerOpenBlock != null)
            {
                var parent = blockStack.Peek();
                innerOpenBlock.Parent = parent;
                parent.Children.Add(innerOpenBlock);
                blockStack.Push(innerOpenBlock);
            }
        }

        // Bloques sin cerrar al final del archivo NO se agregan (evita líneas infinitas hasta el final del documento)
        return result;
    }

    private static bool IsProcedureType(string type) =>
        string.Equals(type, "Sub", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Function", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Property", StringComparison.OrdinalIgnoreCase);

    private static bool TryMatchProcedureOpen(string rawLine, string cleanLine, int column, int lineNumber, out VB6Block? block)
    {
        block = null;
        string upper = cleanLine.ToUpperInvariant();

        // Evitar declaraciones de API: 'Declare Sub ...' o 'Declare Function ...'
        if (upper.Contains("DECLARE"))
            return false;

        var procMatch = ProcedureStartRegex.Match(rawLine);
        if (procMatch.Success)
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

        return false;
    }

    private static bool TryMatchInternalBlockOpen(string[] rawLines, int lineIndex, int column, int lineNumber, out VB6Block? block)
    {
        block = null;
        string rawLine = rawLines[lineIndex];
        string cleanLine = StripCommentsAndStrings(rawLine).Trim();
        string upper = cleanLine.ToUpperInvariant();

        // A. Multi-line If ... Then (puede continuar con '_' en las siguientes líneas)
        if (upper.StartsWith("IF ", StringComparison.Ordinal) || upper == "IF")
        {
            string combined = cleanLine;
            int nextIdx = lineIndex;
            while (combined.EndsWith('_') && nextIdx + 1 < rawLines.Length)
            {
                nextIdx++;
                string nextClean = StripCommentsAndStrings(rawLines[nextIdx]).Trim();
                combined = combined[..^1].TrimEnd() + " " + nextClean;
            }

            string combinedUpper = combined.ToUpperInvariant();
            if (combinedUpper.EndsWith(" THEN", StringComparison.Ordinal) || combinedUpper == "THEN" || combinedUpper.EndsWith("\tTHEN", StringComparison.Ordinal))
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

        return false;
    }

    private static bool CanClose(string openedType, string closeType)
    {
        return string.Equals(openedType, closeType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMatchBlockIntermediate(string cleanLine, string currentBlockType)
    {
        string upper = cleanLine.ToUpperInvariant();
        if (string.Equals(currentBlockType, "If", StringComparison.OrdinalIgnoreCase))
        {
            if (upper == "ELSE" || upper.StartsWith("ELSE ", StringComparison.Ordinal) ||
                upper.StartsWith("ELSEIF ", StringComparison.Ordinal) ||
                (upper.StartsWith("ELSEIF", StringComparison.Ordinal) && upper.EndsWith("THEN", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        else if (string.Equals(currentBlockType, "Select", StringComparison.OrdinalIgnoreCase))
        {
            if (upper.StartsWith("CASE ", StringComparison.Ordinal) || upper == "CASE ELSE")
            {
                return true;
            }
        }
        return false;
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
