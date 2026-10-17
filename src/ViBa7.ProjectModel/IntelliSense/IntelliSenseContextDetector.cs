using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.Navigation;

namespace ViBa7.ProjectModel.IntelliSense;

public enum IntelliSenseContextKind
{
    None,
    MemberAccess,
    ParameterInfo,
    GlobalSymbols,
    EnumValues
}

/// <summary>
/// Representa el contexto semántico detectado en la posición del cursor (caret).
/// </summary>
public class IntelliSenseContext
{
    public IntelliSenseContextKind Kind { get; set; } = IntelliSenseContextKind.None;

    /// <summary>
    /// Expresión objetivo a la izquierda del punto (e.g. "Me", "Command1", "Text1")
    /// o el objeto inferido del bloque With activo.
    /// </summary>
    public string? TargetExpression { get; set; }

    /// <summary>
    /// Prefijo del identificador que el usuario ya ha comenzado a escribir para filtrar la lista.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Desplazamiento en el texto del documento donde debe iniciarse el reemplazo de texto.
    /// </summary>
    public int ReplaceStartPosition { get; set; }

    /// <summary>
    /// Longitud del texto a reemplazar al autocompletar.
    /// </summary>
    public int ReplaceLength { get; set; }

    /// <summary>
    /// Nombre de la función, subrutina o método que se está invocando (para Parameter Info).
    /// </summary>
    public string? FunctionOrMethodName { get; set; }

    /// <summary>
    /// Índice basado en 0 del parámetro actualmente activo.
    /// </summary>
    public int ParameterIndex { get; set; }

    /// <summary>
    /// Símbolo invocado resuelto con su lista de parámetros.
    /// </summary>
    public VbSymbolItem? InvokedSymbol { get; set; }

    /// <summary>
    /// Símbolos candidatos a presentar en la lista desplegable de autocompletado.
    /// </summary>
    public List<VbSymbolItem> Candidates { get; set; } = [];
}

/// <summary>
/// Analizador semántico y detector de contexto para disparar IntelliSense,
/// Auto List Members y Parameter Info.
/// </summary>
public static class IntelliSenseContextDetector
{
    private static readonly Regex WithStatementRegex = new(
        @"^\s*With\s+([a-zA-Z0-9_.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndWithStatementRegex = new(
        @"^\s*End\s+With\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ProcedureHeaderRegex = new(
        @"^\s*(?:(Public|Private|Friend|Static)\s+)?(Sub|Function|Property\s+(?:Get|Let|Set))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndProcedureHeaderRegex = new(
        @"^\s*End\s+(?:Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DimOrStaticRegex = new(
        @"^\s*(?:Dim|Static)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Analiza el documento y la posición del caret para determinar el contexto de IntelliSense.
    /// </summary>
    /// <param name="documentText">Texto completo del documento.</param>
    /// <param name="caretOffset">Posición absoluta del cursor (0 a documentText.Length).</param>
    /// <param name="formModel">Modelo visual del formulario si el documento actual es un Formulario.</param>
    /// <param name="explicitTrigger">True si fue disparado explícitamente con Ctrl+J, Ctrl+I o Ctrl+Espacio.</param>
    /// <param name="isParameterInfoTrigger">True si se solicita específicamente la información de parámetros (Ctrl+I o '(').</param>
    public static IntelliSenseContext DetectContext(
        string documentText,
        int caretOffset,
        FormDesignModel? formModel = null,
        bool explicitTrigger = false,
        bool isParameterInfoTrigger = false)
    {
        var context = new IntelliSenseContext();
        if (string.IsNullOrEmpty(documentText) || caretOffset < 0 || caretOffset > documentText.Length)
        {
            return context;
        }

        // Obtener la línea actual y la posición relativa en la línea
        int lineStart = documentText.LastIndexOf('\n', Math.Max(0, caretOffset - 1));
        lineStart = lineStart == -1 ? 0 : lineStart + 1;

        int lineEnd = documentText.IndexOf('\n', caretOffset);
        lineEnd = lineEnd == -1 ? documentText.Length : lineEnd;
        if (lineEnd > lineStart && documentText[lineEnd - 1] == '\r') lineEnd--;

        string fullLine = documentText[lineStart..lineEnd];
        int colInLine = caretOffset - lineStart;
        string lineUpToCaret = fullLine[..Math.Min(colInLine, fullLine.Length)];

        // Si el cursor se encuentra dentro de un comentario o un literal de cadena, no mostrar IntelliSense
        if (IsInsideCommentOrString(lineUpToCaret, out _))
        {
            return context;
        }

        // 1. Detección de Parameter Info (si se pide explícitamente o si estamos dentro de paréntesis/llamada)
        IntelliSenseContext? paramContext = null;
        if (DetectParameterInfo(lineUpToCaret, documentText, caretOffset, formModel, out paramContext) || isParameterInfoTrigger)
        {
            if (paramContext != null && paramContext.InvokedSymbol != null)
            {
                // Si el parámetro activo requiere un tipo de enumeración (e.g. VbMsgBoxStyle),
                // poblar la lista de candidatos para autocompletado manteniendo ParameterInfo
                string? paramType = null;
                if (paramContext.ParameterIndex >= 0 && paramContext.ParameterIndex < paramContext.InvokedSymbol.Parameters.Count)
                {
                    paramType = paramContext.InvokedSymbol.Parameters[paramContext.ParameterIndex].Type;
                }

                if (IsEnumType(paramType))
                {
                    var enumConsts = VbRuntimeCatalog.GetConstantsForType(paramType!);
                    if (enumConsts.Count > 0)
                    {
                        string wordAtCaret = ExtractWordAtCaret(lineUpToCaret);
                        paramContext.Prefix = wordAtCaret;
                        paramContext.ReplaceStartPosition = caretOffset - wordAtCaret.Length;
                        paramContext.ReplaceLength = wordAtCaret.Length;
                        paramContext.Candidates.AddRange(enumConsts);
                    }
                }

                return paramContext;
            }
        }

        // 2. Detección de Member Access (disparado por '.' o miembro de objeto)
        int lastDotIndex = lineUpToCaret.LastIndexOf('.');
        if (lastDotIndex != -1)
        {
            string afterDot = lineUpToCaret[(lastDotIndex + 1)..];
            // Verificar si afterDot contiene únicamente caracteres de identificador
            if (IsIdentifierOrEmpty(afterDot))
            {
                string beforeDot = lineUpToCaret[..lastDotIndex].TrimEnd();
                string targetExpression = string.Empty;

                if (string.IsNullOrEmpty(beforeDot) || beforeDot.EndsWith(":") || beforeDot.EndsWith("\n"))
                {
                    // Posible bloque With activo
                    string? withTarget = FindEnclosingWithTarget(documentText, caretOffset);
                    if (!string.IsNullOrEmpty(withTarget))
                    {
                        targetExpression = withTarget;
                    }
                }
                else
                {
                    // Extraer el identificador inmediatamente antes del punto
                    targetExpression = ExtractTargetIdentifier(beforeDot);
                }

                if (!string.IsNullOrEmpty(targetExpression))
                {
                    context.Kind = IntelliSenseContextKind.MemberAccess;
                    context.TargetExpression = targetExpression;
                    context.Prefix = afterDot;
                    context.ReplaceStartPosition = lineStart + lastDotIndex + 1;
                    context.ReplaceLength = afterDot.Length;

                    PopulateMemberCandidates(context, targetExpression, formModel, documentText);
                    return context;
                }
            }
        }

        // 3. Autocompletado general de palabras (Global Symbols / Keywords / Variables / Controls)
        // Extraer palabra en la que está situado el cursor
        string currentWord = ExtractWordAtCaret(lineUpToCaret);
        if (explicitTrigger || (!string.IsNullOrEmpty(currentWord) && currentWord.Length >= 2))
        {
            context.Kind = IntelliSenseContextKind.GlobalSymbols;
            context.Prefix = currentWord;
            context.ReplaceStartPosition = caretOffset - currentWord.Length;
            context.ReplaceLength = currentWord.Length;

            PopulateGlobalCandidates(context, formModel, documentText, caretOffset);
            return context;
        }

        return context;
    }

    /// <summary>
    /// Puebla los candidatos de miembros para un objeto o formulario específico.
    /// </summary>
    public static void PopulateMemberCandidates(
        IntelliSenseContext context,
        string targetExpression,
        FormDesignModel? formModel,
        string documentText)
    {
        var result = new List<VbSymbolItem>();

        // Si el objetivo es "Me" o el nombre del propio formulario:
        bool isMeOrForm = targetExpression.Equals("Me", StringComparison.OrdinalIgnoreCase) ||
            (formModel != null && targetExpression.Equals(formModel.Name, StringComparison.OrdinalIgnoreCase));

        if (isMeOrForm)
        {
            // Miembros del Form
            result.AddRange(VbControlMemberCatalog.GetFormMembers());

            // Controles del Formulario (disponibles directamente como Me.Command1)
            if (formModel != null)
            {
                foreach (var ctrl in formModel.GetAllControls())
                {
                    result.Add(new VbSymbolItem
                    {
                        Name = ctrl.Name,
                        Kind = VbSymbolKind.Control,
                        ReturnType = ctrl.TypeName,
                        Description = $"Control {ctrl.TypeName} en {formModel.Name}"
                    });
                }
            }

            // Procedimientos del formulario declarados en el código
            var procedures = CodeStructureParser.ParseMembers(documentText);
            foreach (var p in procedures.Where(m => m.Kind is CodeMemberKind.Sub or CodeMemberKind.Function or CodeMemberKind.PropertyGet or CodeMemberKind.PropertyLet or CodeMemberKind.PropertySet))
            {
                result.Add(new VbSymbolItem
                {
                    Name = p.Name,
                    Kind = VbSymbolKind.Method,
                    ReturnType = p.TypeName,
                    Description = $"{p.KindDescription} {p.Name} ({p.Signature})"
                });
            }
        }
        else
        {
            // Comprobar si el objetivo es un control del formulario (ej. "Command1")
            FormDesignerControl? ctrl = formModel?.FindControl(targetExpression);
            if (ctrl != null)
            {
                var members = VbControlMemberCatalog.GetMembersForControl(ctrl.TypeName);
                result.AddRange(members);
                // Añadir miembros externos provenientes del JSON si existen
                if (ExternalIntelliSenseLoader.ControlMap.TryGetValue(ctrl.TypeName, out var extInfo))
                {
                    foreach (var mem in extInfo.Members)
                    {
                        result.Add(new VbSymbolItem
                        {
                            Name = mem,
                            Kind = VbSymbolKind.Method,
                            ReturnType = "Variant",
                            Description = $"External member {mem} from {ctrl.TypeName}"
                        });
                    }
                }
            }
            else
            {
                // Comprobar si es una variable declarada con un tipo conocido en el código
                var declaredType = FindVariableType(documentText, targetExpression);
                if (!string.IsNullOrEmpty(declaredType))
                {
                    var members = VbControlMemberCatalog.GetMembersForControl(declaredType);
                    result.AddRange(members);
                    // Añadir miembros externos si el tipo tiene entradas en el JSON
                    if (ExternalIntelliSenseLoader.ControlMap.TryGetValue(declaredType, out var extInfo2))
                    {
                        foreach (var mem in extInfo2.Members)
                        {
                            result.Add(new VbSymbolItem
                            {
                                Name = mem,
                                Kind = VbSymbolKind.Method,
                                ReturnType = "Variant",
                                Description = $"External member {mem} from {declaredType}"
                            });
                        }
                    }
                }
            }
        }

        // Filtrar por prefijo si existe
        if (!string.IsNullOrEmpty(context.Prefix))
        {
            context.Candidates = result
                .Where(c => c.Name.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .ToList();
        }
        else
        {
            context.Candidates = result.OrderBy(c => c.Name).ToList();
        }
    }

    /// <summary>
    /// Puebla los candidatos de símbolos globales, funciones, tipos, palabras clave y controles locales.
    /// </summary>
    public static void PopulateGlobalCandidates(
        IntelliSenseContext context,
        FormDesignModel? formModel,
        string documentText,
        int caretOffset = -1)
    {
        var result = new List<VbSymbolItem>();

        // 1. Símbolos del Runtime (Funciones, Constantes, Tipos, Palabras clave)
        result.AddRange(VbRuntimeCatalog.GetAllGlobalSymbols());

        // 2. Si hay formulario, añadir "Me" y todos los controles
        if (formModel != null)
        {
            result.Add(new VbSymbolItem
            {
                Name = "Me",
                Kind = VbSymbolKind.Keyword,
                ReturnType = "Form",
                Description = $"Referencia a la instancia actual del formulario ({formModel.Name})"
            });

            foreach (var ctrl in formModel.GetAllControls())
            {
                result.Add(new VbSymbolItem
                {
                    Name = ctrl.Name,
                    Kind = VbSymbolKind.Control,
                    ReturnType = ctrl.TypeName,
                    Description = $"Control {ctrl.TypeName} en {formModel.Name}"
                });
            }
        }

        // 3. Añadir variables y métodos declarados localmente en el módulo
        var members = CodeStructureParser.ParseMembers(documentText);
        foreach (var m in members)
        {
            var kind = m.Kind switch
            {
                CodeMemberKind.Sub or CodeMemberKind.Function or CodeMemberKind.PropertyGet or CodeMemberKind.PropertyLet or CodeMemberKind.PropertySet => VbSymbolKind.Method,
                CodeMemberKind.Constant => VbSymbolKind.Constant,
                CodeMemberKind.UserType => VbSymbolKind.DataType,
                CodeMemberKind.Enum => VbSymbolKind.Enum,
                CodeMemberKind.Event => VbSymbolKind.Event,
                _ => VbSymbolKind.Variable
            };

            result.Add(new VbSymbolItem
            {
                Name = m.Name,
                Kind = kind,
                ReturnType = m.TypeName,
                Description = $"{m.KindDescription} {m.Name} ({m.Signature})"
            });
        }

        // 4. Añadir variables y parámetros locales del procedimiento activo
        if (caretOffset >= 0)
        {
            CollectProcedureLocals(documentText, caretOffset, result);
        }

        // Filtrar y ordenar
        if (!string.IsNullOrEmpty(context.Prefix))
        {
            context.Candidates = result
                .Where(c => c.Name.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();
        }
        else
        {
            context.Candidates = result
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();
        }
    }

    /// <summary>
    /// Detecta si el cursor se encuentra en una invocación de procedimiento para mostrar Parameter Info.
    /// </summary>
    public static bool DetectParameterInfo(
        string lineUpToCaret,
        string documentText,
        int caretOffset,
        FormDesignModel? formModel,
        out IntelliSenseContext? resultContext)
    {
        resultContext = null;

        // Buscar paréntesis de apertura '(' más reciente que no esté cerrado
        int openParen = -1;
        int parenDepth = 0;
        bool inQuotes = false;

        for (int i = lineUpToCaret.Length - 1; i >= 0; i--)
        {
            char c = lineUpToCaret[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;

            if (c == ')')
            {
                parenDepth++;
            }
            else if (c == '(')
            {
                if (parenDepth > 0)
                {
                    parenDepth--;
                }
                else
                {
                    openParen = i;
                    break;
                }
            }
        }

        string callName = string.Empty;
        string? targetExpr = null;
        int paramIndex = 0;

        if (openParen != -1)
        {
            // Hay un paréntesis de apertura activo
            string beforeParen = lineUpToCaret[..openParen].TrimEnd();
            callName = ExtractTargetIdentifier(beforeParen);

            // Verificar si tiene prefijo de objeto, ej: "Command1.Move"
            int dotIdx = beforeParen.LastIndexOf('.');
            if (dotIdx != -1)
            {
                string beforeDot = beforeParen[..dotIdx].TrimEnd();
                targetExpr = ExtractTargetIdentifier(beforeDot);
            }

            // Calcular el índice de parámetro contando comas entre openParen y el caret
            string argsSlice = lineUpToCaret[(openParen + 1)..];
            paramIndex = CountArguments(argsSlice);
        }
        else
        {
            // Invocación estilo VB6 sin paréntesis (e.g. `MsgBox "Hola", vbInformation` o `Command1.Move 100, 200`)
            // Inspeccionar el inicio de la instrucción
            var tokens = TokenizeStatementArguments(lineUpToCaret, out string invocationToken, out string? target);
            if (!string.IsNullOrEmpty(invocationToken))
            {
                callName = invocationToken;
                targetExpr = target;
                paramIndex = tokens;
            }
        }

        if (string.IsNullOrEmpty(callName))
        {
            return false;
        }

        // Buscar el símbolo de la función o método
        VbSymbolItem? symbol = null;
        if (!string.IsNullOrEmpty(targetExpr))
        {
            // Método de objeto / control / formulario
            string typeName = targetExpr;
            if (targetExpr.Equals("Me", StringComparison.OrdinalIgnoreCase))
            {
                typeName = "Form";
            }
            else
            {
                var ctrl = formModel?.FindControl(targetExpr);
                if (ctrl != null)
                {
                    typeName = ctrl.TypeName;
                }
                else
                {
                    var declaredType = FindVariableType(documentText, targetExpr);
                    if (!string.IsNullOrEmpty(declaredType))
                    {
                        typeName = declaredType;
                    }
                }
            }

            symbol = VbControlMemberCatalog.FindMember(typeName, callName);
        }
        else
        {
            // Función intrínseca de VB6
            symbol = VbRuntimeCatalog.FindFunction(callName);

            // O procedimiento definido en el código
            if (symbol == null)
            {
                var members = CodeStructureParser.ParseMembers(documentText);
                var proc = members.FirstOrDefault(m => m.Name.Equals(callName, StringComparison.OrdinalIgnoreCase));
                if (proc != null)
                {
                    symbol = new VbSymbolItem
                    {
                        Name = proc.Name,
                        Kind = VbSymbolKind.Method,
                        ReturnType = proc.TypeName,
                        CustomSignature = proc.Signature,
                        Description = $"{proc.KindDescription} {proc.Name}"
                    };
                }
            }
        }

        if (symbol != null && (symbol.Parameters.Count > 0 || !string.IsNullOrEmpty(symbol.CustomSignature)))
        {
            resultContext = new IntelliSenseContext
            {
                Kind = IntelliSenseContextKind.ParameterInfo,
                FunctionOrMethodName = callName,
                TargetExpression = targetExpr,
                ParameterIndex = paramIndex,
                InvokedSymbol = symbol
            };
            return true;
        }

        return false;
    }

    #region Helpers

    private static int CountArguments(string argsText)
    {
        int count = 0;
        int depth = 0;
        bool inQuotes = false;

        for (int i = 0; i < argsText.Length; i++)
        {
            char c = argsText[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;

            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (c == ',' && depth == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int TokenizeStatementArguments(string line, out string invocationToken, out string? target)
    {
        invocationToken = string.Empty;
        target = null;

        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("Call ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].TrimStart();
        }

        // Si la instrucción es una palabra clave de control de flujo, ignorar
        string firstWord = trimmed.Split(new[] { ' ', '\t', '(' }, 2)[0];
        if (firstWord.Equals("If", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Dim", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Set", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Let", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("For", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("While", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Select", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Case", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Sub", StringComparison.OrdinalIgnoreCase) ||
            firstWord.Equals("Function", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        // Buscar el espacio que separa la invocación de sus argumentos
        int spaceIdx = -1;
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (char.IsWhiteSpace(trimmed[i]))
            {
                spaceIdx = i;
                break;
            }
        }

        if (spaceIdx == -1) return 0;

        string callPart = trimmed[..spaceIdx].Trim();
        string argsPart = trimmed[spaceIdx..];

        int dotIdx = callPart.LastIndexOf('.');
        if (dotIdx != -1)
        {
            target = callPart[..dotIdx];
            invocationToken = callPart[(dotIdx + 1)..];
        }
        else
        {
            invocationToken = callPart;
        }

        return CountArguments(argsPart);
    }

    private static bool IsEnumType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return false;
        return !typeName.Equals("Long", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Integer", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("String", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Variant", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Double", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Single", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Byte", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Date", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Object", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
               !typeName.Equals("Void", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideCommentOrString(string lineUpToCaret, out bool isInsideString)
    {
        isInsideString = false;
        for (int i = 0; i < lineUpToCaret.Length; i++)
        {
            char c = lineUpToCaret[i];
            if (c == '"')
            {
                isInsideString = !isInsideString;
            }
            else if (c == '\'' && !isInsideString)
            {
                return true; // Es comentario hasta el final de la línea
            }
        }
        return isInsideString;
    }

    private static bool IsIdentifierOrEmpty(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        return text.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private static string ExtractTargetIdentifier(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        int i = text.Length - 1;
        while (i >= 0 && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
        {
            i--;
        }
        return text[(i + 1)..];
    }

    private static string ExtractWordAtCaret(string lineUpToCaret)
    {
        if (string.IsNullOrEmpty(lineUpToCaret)) return string.Empty;
        int i = lineUpToCaret.Length - 1;
        while (i >= 0 && (char.IsLetterOrDigit(lineUpToCaret[i]) || lineUpToCaret[i] == '_'))
        {
            i--;
        }
        return lineUpToCaret[(i + 1)..];
    }

    private static string? FindEnclosingWithTarget(string documentText, int caretOffset)
    {
        // Buscar líneas hacia atrás desde el caret
        int currentPos = Math.Min(caretOffset, documentText.Length);
        int unclosedWithCount = 0;

        while (currentPos > 0)
        {
            int prevNewLine = documentText.LastIndexOf('\n', currentPos - 1);
            int lineStart = prevNewLine == -1 ? 0 : prevNewLine + 1;
            int lineEnd = currentPos;
            if (lineEnd > lineStart && documentText[lineEnd - 1] == '\r') lineEnd--;

            string line = documentText[lineStart..lineEnd].Trim();

            // Si llegamos a la cabecera de un procedimiento, no salir del ámbito
            if (ProcedureHeaderRegex.IsMatch(line))
            {
                break;
            }

            if (EndWithStatementRegex.IsMatch(line))
            {
                unclosedWithCount++;
            }
            else
            {
                var match = WithStatementRegex.Match(line);
                if (match.Success)
                {
                    if (unclosedWithCount > 0)
                    {
                        unclosedWithCount--;
                    }
                    else
                    {
                        // Encontrado el With que contiene este bloque
                        return match.Groups[1].Value.Trim();
                    }
                }
            }

            currentPos = prevNewLine == -1 ? 0 : prevNewLine;
        }

        return null;
    }

    private static string? FindVariableType(string documentText, string variableName)
    {
        var members = CodeStructureParser.ParseMembers(documentText);
        var member = members.FirstOrDefault(m => m.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
        return member?.TypeName;
    }

    private static void CollectProcedureLocals(string documentText, int caretOffset, List<VbSymbolItem> accumulator)
    {
        if (caretOffset < 0 || string.IsNullOrEmpty(documentText)) return;

        var lines = documentText.Split('\n');
        int charCount = 0;
        int currentLineIdx = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            int lineLen = lines[i].Length + 1; // +1 for newline
            if (charCount <= caretOffset && caretOffset <= charCount + lineLen)
            {
                currentLineIdx = i;
                break;
            }
            charCount += lineLen;
        }

        // Buscar el inicio del procedimiento hacia atrás
        int procStartLine = -1;
        for (int i = currentLineIdx; i >= 0; i--)
        {
            string line = lines[i].Trim();
            if (EndProcedureHeaderRegex.IsMatch(line) && i < currentLineIdx)
            {
                break;
            }
            if (ProcedureHeaderRegex.IsMatch(line))
            {
                procStartLine = i;
                break;
            }
        }

        if (procStartLine == -1) return;

        // 1. Extraer parámetros de la cabecera
        string headerLine = lines[procStartLine].Trim();
        int openParen = headerLine.IndexOf('(');
        int closeParen = headerLine.LastIndexOf(')');
        if (openParen != -1 && closeParen > openParen)
        {
            string paramSlice = headerLine.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            if (!string.IsNullOrEmpty(paramSlice))
            {
                var paramDefs = paramSlice.Split(',');
                foreach (var pDef in paramDefs)
                {
                    string p = pDef.Trim();
                    if (p.StartsWith("ByVal ", StringComparison.OrdinalIgnoreCase)) p = p[6..].Trim();
                    else if (p.StartsWith("ByRef ", StringComparison.OrdinalIgnoreCase)) p = p[6..].Trim();
                    else if (p.StartsWith("Optional ", StringComparison.OrdinalIgnoreCase)) p = p[9..].Trim();

                    string pName = p;
                    string pType = "Variant";
                    int asIdx = p.IndexOf(" As ", StringComparison.OrdinalIgnoreCase);
                    if (asIdx != -1)
                    {
                        pName = p[..asIdx].Trim();
                        pType = p[(asIdx + 4)..].Split(new[] { '=', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    }

                    if (!string.IsNullOrEmpty(pName) && IsIdentifierOrEmpty(pName))
                    {
                        accumulator.Add(new VbSymbolItem
                        {
                            Name = pName,
                            Kind = VbSymbolKind.Variable,
                            ReturnType = pType,
                            Description = $"Parámetro {pName} As {pType}"
                        });
                    }
                }
            }
        }

        // 2. Extraer declaraciones Dim / Static dentro del cuerpo del procedimiento
        for (int i = procStartLine + 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (EndProcedureHeaderRegex.IsMatch(line)) break;

            var match = DimOrStaticRegex.Match(line);
            if (match.Success)
            {
                string declList = match.Groups[1].Value.Trim();
                var decls = declList.Split(',');
                foreach (var d in decls)
                {
                    string item = d.Trim();
                    string varName = item;
                    string varType = "Variant";
                    int asIdx = item.IndexOf(" As ", StringComparison.OrdinalIgnoreCase);
                    if (asIdx != -1)
                    {
                        varName = item[..asIdx].Trim();
                        varType = item[(asIdx + 4)..].Trim();
                        if (varType.StartsWith("New ", StringComparison.OrdinalIgnoreCase))
                        {
                            varType = varType[4..].Trim();
                        }
                    }

                    int arrIdx = varName.IndexOf('(');
                    if (arrIdx != -1) varName = varName[..arrIdx].Trim();

                    if (!string.IsNullOrEmpty(varName) && IsIdentifierOrEmpty(varName))
                    {
                        accumulator.Add(new VbSymbolItem
                        {
                            Name = varName,
                            Kind = VbSymbolKind.Variable,
                            ReturnType = varType,
                            Description = $"Variable local {varName} As {varType}"
                        });
                    }
                }
            }
        }
    }

    #endregion
}
