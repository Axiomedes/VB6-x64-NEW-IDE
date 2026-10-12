using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VB7x64.ProjectModel.Execution;

public enum TokenType
{
    Number,
    String,
    Date,
    Identifier,
    Operator,
    OpenParen,
    CloseParen,
    Comma,
    Dot,
    EndOfExpression
}

public record Token(TokenType Type, string Value, int Position);

/// <summary>
/// Evaluador en caliente de expresiones de Visual Basic 6.0 con soporte para literales,
/// operadores aritméticos, lógicos y relacionales, funciones intrínsecas y acceso a miembros de objetos.
/// </summary>
public static class VbExpressionEvaluator
{
    public static VbValue Evaluate(string expression, VbExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(expression)) return VbValue.Empty;

        var tokens = Tokenize(expression);
        if (tokens.Count == 0) return VbValue.Empty;

        int index = 0;
        return ParseLogicalOr(tokens, ref index, context);
    }

    #region Tokenizador

    private static List<Token> Tokenize(string expr)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < expr.Length)
        {
            char c = expr[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Comentario en línea (') o Rem
            if (c == '\'')
            {
                break;
            }

            // Literal de cadena: "texto"
            if (c == '"')
            {
                int start = i + 1;
                i++;
                var sb = new StringBuilder();
                while (i < expr.Length)
                {
                    if (expr[i] == '"')
                    {
                        if (i + 1 < expr.Length && expr[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(expr[i]);
                        i++;
                    }
                }
                tokens.Add(new Token(TokenType.String, sb.ToString(), start));
                continue;
            }

            // Literal de fecha: #01/01/2026#
            if (c == '#')
            {
                int start = i + 1;
                i++;
                int end = expr.IndexOf('#', i);
                if (end != -1)
                {
                    string dateStr = expr[start..end];
                    tokens.Add(new Token(TokenType.Date, dateStr, start));
                    i = end + 1;
                }
                else
                {
                    tokens.Add(new Token(TokenType.Date, expr[start..], start));
                    i = expr.Length;
                }
                continue;
            }

            // Delimitadores y separadores
            if (c == '(') { tokens.Add(new Token(TokenType.OpenParen, "(", i++)); continue; }
            if (c == ')') { tokens.Add(new Token(TokenType.CloseParen, ")", i++)); continue; }
            if (c == ',') { tokens.Add(new Token(TokenType.Comma, ",", i++)); continue; }
            if (c == '.') { tokens.Add(new Token(TokenType.Dot, ".", i++)); continue; }

            // Números hexadecimales (&H) u octales (&O)
            if (c == '&' && i + 1 < expr.Length && (char.ToUpperInvariant(expr[i + 1]) == 'H' || char.ToUpperInvariant(expr[i + 1]) == 'O'))
            {
                int start = i;
                char kind = char.ToUpperInvariant(expr[i + 1]);
                i += 2;
                while (i < expr.Length && (char.IsAsciiHexDigit(expr[i]) || expr[i] == '&'))
                {
                    if (expr[i] == '&') { i++; break; }
                    i++;
                }
                tokens.Add(new Token(TokenType.Number, expr[start..i], start));
                continue;
            }

            // Operadores de 1 o 2 caracteres: <>, <=, >=, =, <, >, +, -, *, /, \, ^, &
            if (c == '<')
            {
                if (i + 1 < expr.Length && expr[i + 1] == '>') { tokens.Add(new Token(TokenType.Operator, "<>", i)); i += 2; continue; }
                if (i + 1 < expr.Length && expr[i + 1] == '=') { tokens.Add(new Token(TokenType.Operator, "<=", i)); i += 2; continue; }
                tokens.Add(new Token(TokenType.Operator, "<", i++));
                continue;
            }
            if (c == '>')
            {
                if (i + 1 < expr.Length && expr[i + 1] == '=') { tokens.Add(new Token(TokenType.Operator, ">=", i)); i += 2; continue; }
                tokens.Add(new Token(TokenType.Operator, ">", i++));
                continue;
            }
            if (c is '=' or '+' or '-' or '*' or '/' or '\\' or '^' or '&')
            {
                tokens.Add(new Token(TokenType.Operator, c.ToString(), i++));
                continue;
            }

            // Números decimales
            if (char.IsDigit(c) || (c == '.' && i + 1 < expr.Length && char.IsDigit(expr[i + 1])))
            {
                int start = i;
                bool hasDot = false;
                while (i < expr.Length && (char.IsDigit(expr[i]) || (!hasDot && expr[i] == '.' && i + 1 < expr.Length && char.IsDigit(expr[i + 1]))))
                {
                    if (expr[i] == '.') hasDot = true;
                    i++;
                }
                tokens.Add(new Token(TokenType.Number, expr[start..i], start));
                continue;
            }

            // Identificadores y palabras clave (And, Or, Not, Mod, Xor, Eqv, Imp, etc.)
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_'))
                {
                    i++;
                }
                string word = expr[start..i];
                string upper = word.ToUpperInvariant();

                if (upper is "AND" or "OR" or "NOT" or "XOR" or "EQV" or "IMP" or "MOD")
                {
                    tokens.Add(new Token(TokenType.Operator, upper, start));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Identifier, word, start));
                }
                continue;
            }

            // Carácter desconocido, avanzar
            i++;
        }

        return tokens;
    }

    #endregion

    #region Parsers con Precedencia de Operadores

    // 1. Lógico OR / XOR / EQV / IMP
    private static VbValue ParseLogicalOr(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseLogicalAnd(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator &&
               tokens[index].Value is "OR" or "XOR" or "EQV" or "IMP")
        {
            string op = tokens[index++].Value;
            var right = ParseLogicalAnd(tokens, ref index, ctx);

            left = op switch
            {
                "OR" => VbValue.VbOr(left, right),
                "XOR" => VbValue.VbXor(left, right),
                "EQV" => VbValue.VbEqv(left, right),
                "IMP" => VbValue.VbImp(left, right),
                _ => left
            };
        }

        return left;
    }

    // 2. Lógico AND
    private static VbValue ParseLogicalAnd(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseRelational(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value == "AND")
        {
            index++;
            var right = ParseRelational(tokens, ref index, ctx);
            left = VbValue.VbAnd(left, right);
        }

        return left;
    }

    // 3. Relacional (=, <>, <, >, <=, >=)
    private static VbValue ParseRelational(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseConcatenation(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator &&
               tokens[index].Value is "=" or "<>" or "<" or ">" or "<=" or ">=")
        {
            string op = tokens[index++].Value;
            var right = ParseConcatenation(tokens, ref index, ctx);

            left = op switch
            {
                "=" => VbValue.CompareEqual(left, right),
                "<>" => VbValue.CompareNotEqual(left, right),
                "<" => VbValue.CompareLessThan(left, right),
                ">" => VbValue.CompareGreaterThan(left, right),
                "<=" => VbValue.CompareLessOrEqual(left, right),
                ">=" => VbValue.CompareGreaterOrEqual(left, right),
                _ => left
            };
        }

        return left;
    }

    // 4. Concatenación (&)
    private static VbValue ParseConcatenation(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseAddSub(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value == "&")
        {
            index++;
            var right = ParseAddSub(tokens, ref index, ctx);
            left = VbValue.Concat(left, right);
        }

        return left;
    }

    // 5. Suma y Resta (+, -)
    private static VbValue ParseAddSub(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseMod(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value is "+" or "-")
        {
            string op = tokens[index++].Value;
            var right = ParseMod(tokens, ref index, ctx);

            left = op == "+" ? (left + right) : (left - right);
        }

        return left;
    }

    // 6. Módulo (Mod)
    private static VbValue ParseMod(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseIntDivide(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value == "MOD")
        {
            index++;
            var right = ParseIntDivide(tokens, ref index, ctx);
            left = VbValue.Modulo(left, right);
        }

        return left;
    }

    // 7. División entera (\)
    private static VbValue ParseIntDivide(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseMulDiv(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value == "\\")
        {
            index++;
            var right = ParseMulDiv(tokens, ref index, ctx);
            left = VbValue.IntegerDivide(left, right);
        }

        return left;
    }

    // 8. Multiplicación y División (*, /)
    private static VbValue ParseMulDiv(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParseUnary(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value is "*" or "/")
        {
            string op = tokens[index++].Value;
            var right = ParseUnary(tokens, ref index, ctx);

            left = op == "*" ? (left * right) : (left / right);
        }

        return left;
    }

    // 9. Operadores Unarios (+, -, Not)
    private static VbValue ParseUnary(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        if (index < tokens.Count && tokens[index].Type == TokenType.Operator)
        {
            string op = tokens[index].Value;
            if (op == "+")
            {
                index++;
                return ParseUnary(tokens, ref index, ctx);
            }
            if (op == "-")
            {
                index++;
                var val = ParseUnary(tokens, ref index, ctx);
                return VbValue.FromDouble(-val.AsDouble());
            }
            if (op == "NOT")
            {
                index++;
                var val = ParseUnary(tokens, ref index, ctx);
                return VbValue.VbNot(val);
            }
        }

        return ParsePower(tokens, ref index, ctx);
    }

    // 10. Potencia (^)
    private static VbValue ParsePower(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var left = ParsePrimary(tokens, ref index, ctx);

        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && tokens[index].Value == "^")
        {
            index++;
            var right = ParseUnary(tokens, ref index, ctx);
            left = VbValue.Power(left, right);
        }

        return left;
    }

    // 11. Primarios: literales, paréntesis, identificadores, llamadas y miembros
    private static VbValue ParsePrimary(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        if (index >= tokens.Count) return VbValue.Empty;

        var token = tokens[index++];

        // Paréntesis: (expr)
        if (token.Type == TokenType.OpenParen)
        {
            var val = ParseLogicalOr(tokens, ref index, ctx);
            if (index < tokens.Count && tokens[index].Type == TokenType.CloseParen)
            {
                index++;
            }
            return ParseMemberAccess(val, tokens, ref index, ctx);
        }

        // Literales
        if (token.Type == TokenType.String)
        {
            return ParseMemberAccess(VbValue.FromString(token.Value), tokens, ref index, ctx);
        }

        if (token.Type == TokenType.Number)
        {
            string numStr = token.Value;
            if (numStr.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = numStr[2..].TrimEnd('&');
                int hexVal = int.Parse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return ParseMemberAccess(VbValue.FromLong(hexVal), tokens, ref index, ctx);
            }

            if (double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
            {
                return ParseMemberAccess(VbValue.FromDouble(dVal), tokens, ref index, ctx);
            }
            return VbValue.FromLong(0);
        }

        if (token.Type == TokenType.Date)
        {
            if (DateTime.TryParse(token.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return ParseMemberAccess(VbValue.FromDate(dt), tokens, ref index, ctx);
            }
            return VbValue.FromDate(DateTime.MinValue);
        }

        // Identificador (variable, función, constante o control)
        if (token.Type == TokenType.Identifier)
        {
            string name = token.Value;

            // Constantes booleanas y nulas
            if (name.Equals("True", StringComparison.OrdinalIgnoreCase)) return VbValue.True;
            if (name.Equals("False", StringComparison.OrdinalIgnoreCase)) return VbValue.False;
            if (name.Equals("Null", StringComparison.OrdinalIgnoreCase)) return VbValue.Null;
            if (name.Equals("Empty", StringComparison.OrdinalIgnoreCase)) return VbValue.Empty;
            if (name.Equals("Nothing", StringComparison.OrdinalIgnoreCase)) return VbValue.Nothing;

            // Invocación a función con argumentos: fn(...)
            if (index < tokens.Count && tokens[index].Type == TokenType.OpenParen)
            {
                index++; // consumir '('
                var args = ParseArguments(tokens, ref index, ctx);

                // Función MsgBox con retorno numérico (vbOK, vbYes, vbNo, etc.)
                if (name.Equals("MsgBox", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
                {
                    string prompt = args[0].AsString();
                    int buttons = args.Count > 1 ? (int)args[1].AsLong() : 0;
                    string title = args.Count > 2 ? args[2].AsString() : string.Empty;
                    int ret = ctx.MsgBoxHandler?.Invoke(prompt, buttons, title) ?? 1;
                    return ParseMemberAccess(VbValue.FromLong(ret), tokens, ref index, ctx);
                }

                // Función InputBox con retorno de texto
                if (name.Equals("InputBox", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
                {
                    string prompt = args[0].AsString();
                    string title = args.Count > 1 ? args[1].AsString() : string.Empty;
                    string def = args.Count > 2 ? args[2].AsString() : string.Empty;
                    string ret = ctx.InputBoxHandler?.Invoke(prompt, title, def) ?? string.Empty;
                    return ParseMemberAccess(VbValue.FromString(ret), tokens, ref index, ctx);
                }

                // Función DoEvents
                if (name.Equals("DoEvents", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.DoEvents();
                    return ParseMemberAccess(VbValue.FromLong(0), tokens, ref index, ctx);
                }

                // 1. Probar funciones intrínsecas del runtime
                if (VbRuntimeFunctions.TryInvoke(name, args.ToArray(), out var fnResult))
                {
                    return ParseMemberAccess(fnResult, tokens, ref index, ctx);
                }

                // 2. Si es objeto/control con llamada a método directo
                var objVal = ctx.GetVariable(name);
                if (objVal.RawValue is ILiveControl ctrl)
                {
                    var res = ctrl.InvokeMethod(string.Empty, args.ToArray());
                    return ParseMemberAccess(res, tokens, ref index, ctx);
                }
            }

            // Constantes intrínsecas estándar de VB6 (vbCritical, vbInformation, vbYesNo, vbCrLf, etc.)
            if (VbConstants.TryGetConstant(name, out var constVal))
            {
                return ParseMemberAccess(constVal, tokens, ref index, ctx);
            }

            // Variable o referencia a control/formulario
            var val = ctx.GetVariable(name);
            return ParseMemberAccess(val, tokens, ref index, ctx);
        }

        return VbValue.Empty;
    }

    private static List<VbValue> ParseArguments(List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var args = new List<VbValue>();

        if (index < tokens.Count && tokens[index].Type == TokenType.CloseParen)
        {
            index++; // consumir ')'
            return args;
        }

        while (index < tokens.Count)
        {
            // Parámetro omitido, e.g. fn(a, , b)
            if (tokens[index].Type is TokenType.Comma or TokenType.CloseParen)
            {
                args.Add(VbValue.Empty);
            }
            else
            {
                args.Add(ParseLogicalOr(tokens, ref index, ctx));
            }

            if (index < tokens.Count && tokens[index].Type == TokenType.Comma)
            {
                index++;
                continue;
            }

            if (index < tokens.Count && tokens[index].Type == TokenType.CloseParen)
            {
                index++;
                break;
            }

            break;
        }

        return args;
    }

    private static VbValue ParseMemberAccess(VbValue target, List<Token> tokens, ref int index, VbExecutionContext ctx)
    {
        var current = target;

        while (index < tokens.Count && tokens[index].Type == TokenType.Dot)
        {
            index++; // consumir '.'
            if (index >= tokens.Count || tokens[index].Type != TokenType.Identifier)
            {
                break;
            }

            string memberName = tokens[index++].Value;

            // Invocación de método con paréntesis: target.Metodo(...)
            List<VbValue>? methodArgs = null;
            if (index < tokens.Count && tokens[index].Type == TokenType.OpenParen)
            {
                index++;
                methodArgs = ParseArguments(tokens, ref index, ctx);
            }

            if (current.RawValue is ILiveControl ctrl)
            {
                if (methodArgs != null)
                {
                    current = ctrl.InvokeMethod(memberName, methodArgs.ToArray());
                }
                else
                {
                    current = ctrl.GetProperty(memberName);
                }
            }
            else if (current.RawValue is VbErrObject err)
            {
                if (memberName.Equals("Number", StringComparison.OrdinalIgnoreCase)) current = VbValue.FromLong(err.Number);
                else if (memberName.Equals("Description", StringComparison.OrdinalIgnoreCase)) current = VbValue.FromString(err.Description);
                else if (memberName.Equals("Source", StringComparison.OrdinalIgnoreCase)) current = VbValue.FromString(err.Source);
                else current = VbValue.Empty;
            }
            else
            {
                current = VbValue.Empty;
            }
        }

        return current;
    }

    #endregion
}
