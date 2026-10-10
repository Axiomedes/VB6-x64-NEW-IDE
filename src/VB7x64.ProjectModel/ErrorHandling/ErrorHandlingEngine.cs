using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using VB7x64.ProjectModel.Formatting;

namespace VB7x64.ProjectModel.ErrorHandling;

public class ErrorScheme
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HeaderStatement { get; set; } = "On Error GoTo ErrorHandler";
    public string HandlerBlock { get; set; } = string.Empty;
    public bool HasHandlerBlock => !string.IsNullOrWhiteSpace(HandlerBlock);

    public static List<ErrorScheme> GetPredefinedSchemes()
    {
        return new List<ErrorScheme>
        {
            new ErrorScheme
            {
                Name = "Estándar con Mensaje (MsgBox)",
                Description = "Muestra una ventana informativa crítica con número, descripción del error y ubicación.",
                HeaderStatement = "On Error GoTo ErrorHandler",
                HandlerBlock =
@"%EXIT_TYPE%

ErrorHandler:
    MsgBox ""Error "" & Err.Number & "" ("" & Err.Description & "") en %MODULE_NAME%.%PROC_NAME%"", vbCritical, ""Error del Sistema"""
            },
            new ErrorScheme
            {
                Name = "Registro en Depuración (Debug.Print)",
                Description = "Registra el error en la ventana Inmediato/Consola de depuración con marca de tiempo.",
                HeaderStatement = "On Error GoTo ErrorHandler",
                HandlerBlock =
@"%EXIT_TYPE%

ErrorHandler:
    Debug.Print ""[ERROR "" & Err.Number & ""] "" & Err.Description & "" en %MODULE_NAME%.%PROC_NAME% ["" & ""%DATE%"" & ""]"""
            },
            new ErrorScheme
            {
                Name = "Llamada Centralizada (HandleError)",
                Description = "Invoca una subrutina centralizada del proyecto para control y auditoría unificada.",
                HeaderStatement = "On Error GoTo ErrorHandler",
                HandlerBlock =
@"%EXIT_TYPE%

ErrorHandler:
    Call HandleError(Err.Number, Err.Description, ""%MODULE_NAME%"", ""%PROC_NAME%"")"
            },
            new ErrorScheme
            {
                Name = "Silencioso / Continuar (On Error Resume Next)",
                Description = "Omite el error y continúa con la siguiente línea de ejecución.",
                HeaderStatement = "On Error Resume Next",
                HandlerBlock = string.Empty
            }
        };
    }
}

public static class ErrorHandlingEngine
{
    private static readonly Regex OnErrorRegex = new(
        @"^\s*On\s+Error\s+(?:GoTo\s+[a-zA-Z0-9_]+|Resume\s+Next|GoTo\s+0)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExitProcRegex = new(
        @"^\s*Exit\s+(Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndProcRegex = new(
        @"^\s*End\s+(Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ErrorLabelRegex = new(
        @"^\s*(?:ErrorHandler|CatchError|ErrHandler|ErrorRoutine|Error_Handler):\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Comprueba si un procedimiento ya contiene una sentencia 'On Error'.
    /// </summary>
    public static bool HasErrorHandling(string procCode)
    {
        if (string.IsNullOrWhiteSpace(procCode)) return false;
        string[] lines = procCode.Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in lines)
        {
            string clean = CodeFormatter.StripCommentsAndStrings(rawLine).Trim();
            if (OnErrorRegex.IsMatch(clean))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Aplica el esquema de manejo de errores a un procedimiento individual.
    /// </summary>
    public static string ApplySchemeToProcedure(string procCode, string procName, string procKind, string moduleName, ErrorScheme scheme, bool overwriteExisting = false)
    {
        if (string.IsNullOrWhiteSpace(procCode)) return procCode;

        if (HasErrorHandling(procCode))
        {
            if (!overwriteExisting)
            {
                return procCode;
            }
            procCode = RemoveErrorHandlerFromProcedure(procCode, procKind);
        }

        string[] lines = procCode.Replace("\r\n", "\n").Split('\n');
        var outputLines = new List<string>();

        // 1. Localizar final de cabecera del procedimiento (teniendo en cuenta continuaciones con ' _')
        int headerEndIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string clean = CodeFormatter.StripCommentsAndStrings(lines[i]).Trim();
            if (!clean.EndsWith(" _"))
            {
                headerEndIndex = i;
                break;
            }
        }

        if (headerEndIndex == -1 || headerEndIndex >= lines.Length)
        {
            return procCode;
        }

        for (int i = 0; i <= headerEndIndex; i++)
        {
            outputLines.Add(lines[i]);
        }

        // 2. Insertar sentencia 'On Error ...' al inicio del cuerpo
        string headerStatement = ExpandMacros(scheme.HeaderStatement, procName, procKind, moduleName);
        outputLines.Add("    " + headerStatement);

        // 3. Localizar 'End Sub / End Function / End Property'
        int endProcIndex = -1;
        for (int i = lines.Length - 1; i > headerEndIndex; i--)
        {
            string clean = CodeFormatter.StripCommentsAndStrings(lines[i]).Trim();
            if (EndProcRegex.IsMatch(clean))
            {
                endProcIndex = i;
                break;
            }
        }

        if (endProcIndex == -1)
        {
            // Procedimiento malformado, agregar resto tal cual
            for (int i = headerEndIndex + 1; i < lines.Length; i++)
            {
                outputLines.Add(lines[i]);
            }
            return string.Join("\r\n", outputLines);
        }

        // Agregar cuerpo intermedio
        for (int i = headerEndIndex + 1; i < endProcIndex; i++)
        {
            outputLines.Add(lines[i]);
        }

        // 4. Si el esquema tiene bloque de captura ('ErrorHandler: ...'), insertarlo antes de End Sub/Function/Property
        if (scheme.HasHandlerBlock)
        {
            string handlerExpanded = ExpandMacros(scheme.HandlerBlock, procName, procKind, moduleName);
            string[] handlerLines = handlerExpanded.Replace("\r\n", "\n").Split('\n');

            // Asegurar una separación si la última línea del cuerpo no estaba vacía
            if (outputLines.Count > 0 && !string.IsNullOrWhiteSpace(outputLines[^1]))
            {
                outputLines.Add(string.Empty);
            }

            foreach (var hl in handlerLines)
            {
                string trimmed = hl.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    outputLines.Add(string.Empty);
                }
                else if (trimmed.EndsWith(':') || trimmed.Equals("%EXIT_TYPE%", StringComparison.OrdinalIgnoreCase))
                {
                    outputLines.Add("    " + trimmed);
                }
                else
                {
                    // Contenido del manejador indented
                    outputLines.Add("    " + hl.TrimStart());
                }
            }
        }

        // Agregar la línea de cierre End Sub/Function/Property
        outputLines.Add(lines[endProcIndex]);

        // Agregar cualquier línea sobrante tras End Sub (ej. líneas en blanco finales)
        for (int i = endProcIndex + 1; i < lines.Length; i++)
        {
            outputLines.Add(lines[i]);
        }

        return string.Join("\r\n", outputLines);
    }

    /// <summary>
    /// Retira el esquema de manejo de errores de un procedimiento individual.
    /// </summary>
    public static string RemoveErrorHandlerFromProcedure(string procCode, string procKind)
    {
        if (string.IsNullOrWhiteSpace(procCode)) return procCode;

        string[] lines = procCode.Replace("\r\n", "\n").Split('\n');
        var outputLines = new List<string>();

        // 1. Eliminar On Error
        int errorLabelIndex = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string clean = CodeFormatter.StripCommentsAndStrings(raw).Trim();

            if (OnErrorRegex.IsMatch(clean))
            {
                continue; // Omitir On Error
            }

            if (ErrorLabelRegex.IsMatch(clean))
            {
                errorLabelIndex = i;
                break;
            }

            outputLines.Add(raw);
        }

        // Si se encontró etiqueta de error, retroceder en outputLines para remover un posible Exit Sub/Function/Property previo
        if (errorLabelIndex != -1)
        {
            // Remover líneas vacías al final de outputLines y el Exit Sub si estaba inmediatamente antes
            while (outputLines.Count > 0 && string.IsNullOrWhiteSpace(outputLines[^1]))
            {
                outputLines.RemoveAt(outputLines.Count - 1);
            }

            if (outputLines.Count > 0)
            {
                string cleanLast = CodeFormatter.StripCommentsAndStrings(outputLines[^1]).Trim();
                if (ExitProcRegex.IsMatch(cleanLast))
                {
                    outputLines.RemoveAt(outputLines.Count - 1);
                }
            }

            // Localizar el End Sub / End Function / End Property en el resto de las líneas
            int endProcIndex = -1;
            for (int i = lines.Length - 1; i >= errorLabelIndex; i--)
            {
                string clean = CodeFormatter.StripCommentsAndStrings(lines[i]).Trim();
                if (EndProcRegex.IsMatch(clean))
                {
                    endProcIndex = i;
                    break;
                }
            }

            if (endProcIndex != -1)
            {
                outputLines.Add(lines[endProcIndex]);
                for (int i = endProcIndex + 1; i < lines.Length; i++)
                {
                    outputLines.Add(lines[i]);
                }
            }
        }

        return string.Join("\r\n", outputLines);
    }

    /// <summary>
    /// Aplica un esquema a todos los procedimientos de un módulo o código completo.
    /// </summary>
    public static string ApplyToModule(string code, string moduleName, ErrorScheme scheme, bool overwriteExisting, out int modifiedCount)
    {
        modifiedCount = 0;
        if (string.IsNullOrWhiteSpace(code)) return code;

        var (header, procs) = ProcedureSorter.ParseProcedures(code);
        if (procs.Count == 0) return code;

        var updatedProcs = new List<ProcedureSortItem>();

        foreach (var p in procs)
        {
            bool hasHandling = HasErrorHandling(p.Code);
            if (!hasHandling || overwriteExisting)
            {
                string updatedCode = ApplySchemeToProcedure(p.Code, p.Name, p.Kind, moduleName, scheme, overwriteExisting);
                if (!string.Equals(p.Code, updatedCode, StringComparison.Ordinal))
                {
                    p.Code = updatedCode;
                    modifiedCount++;
                }
            }
            updatedProcs.Add(p);
        }

        return ProcedureSorter.GenerateSortedCode(header, updatedProcs, preserveComments: true);
    }

    /// <summary>
    /// Retira el manejo de errores de todos los procedimientos de un módulo o código completo.
    /// </summary>
    public static string RemoveFromModule(string code, out int removedCount)
    {
        removedCount = 0;
        if (string.IsNullOrWhiteSpace(code)) return code;

        var (header, procs) = ProcedureSorter.ParseProcedures(code);
        if (procs.Count == 0) return code;

        var updatedProcs = new List<ProcedureSortItem>();

        foreach (var p in procs)
        {
            if (HasErrorHandling(p.Code))
            {
                string cleaned = RemoveErrorHandlerFromProcedure(p.Code, p.Kind);
                if (!string.Equals(p.Code, cleaned, StringComparison.Ordinal))
                {
                    p.Code = cleaned;
                    removedCount++;
                }
            }
            updatedProcs.Add(p);
        }

        return ProcedureSorter.GenerateSortedCode(header, updatedProcs, preserveComments: true);
    }

    private static string ExpandMacros(string template, string procName, string procKind, string moduleName)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        string exitType = "Exit Sub";
        string kindUpper = procKind.ToUpperInvariant();
        if (kindUpper.Contains("FUNCTION")) exitType = "Exit Function";
        else if (kindUpper.Contains("PROPERTY")) exitType = "Exit Property";

        return template
            .Replace("%PROC_NAME%", procName, StringComparison.OrdinalIgnoreCase)
            .Replace("%MODULE_NAME%", string.IsNullOrWhiteSpace(moduleName) ? "Module" : moduleName, StringComparison.OrdinalIgnoreCase)
            .Replace("%EXIT_TYPE%", exitType, StringComparison.OrdinalIgnoreCase)
            .Replace("%DATE%", DateTime.Now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("%USER%", Environment.UserName, StringComparison.OrdinalIgnoreCase);
    }
}
