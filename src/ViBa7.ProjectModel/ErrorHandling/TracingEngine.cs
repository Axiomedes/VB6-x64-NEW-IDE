using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ViBa7.ProjectModel.Formatting;

namespace ViBa7.ProjectModel.ErrorHandling;

public class TracingOptions
{
    public const string DefaultTag = "' [VB7_TRACE]";

    public string EntryTemplate { get; set; } = "Debug.Print \"--> Entrando a %MODULE_NAME%.%PROC_NAME% [%DATE%]\" ' [VB7_TRACE]";
    public string ExitTemplate { get; set; } = "Debug.Print \"<-- Saliendo de %MODULE_NAME%.%PROC_NAME%\" ' [VB7_TRACE]";
    public bool InjectExitTrace { get; set; } = true;
    public string TraceTag { get; set; } = DefaultTag;
}

public static class TracingEngine
{
    private static readonly Regex OnErrorRegex = new(
        @"^\s*On\s+Error\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExitProcRegex = new(
        @"^\s*Exit\s+(?:Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndProcRegex = new(
        @"^\s*End\s+(?:Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Comprueba si un procedimiento ya contiene trazas inyectadas.
    /// </summary>
    public static bool HasInjectedTraces(string procCode, string traceTag = TracingOptions.DefaultTag)
    {
        if (string.IsNullOrWhiteSpace(procCode)) return false;
        return procCode.Contains(traceTag, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Inyecta trazas de entrada y salida en un procedimiento individual.
    /// </summary>
    public static string InjectTracesIntoProcedure(string procCode, string procName, string procKind, string moduleName, TracingOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(procCode)) return procCode;
        options ??= new TracingOptions();

        // Si ya tiene trazas inyectadas, primero retirarlas para evitar duplicados
        if (HasInjectedTraces(procCode, options.TraceTag))
        {
            procCode = RemoveTracesFromProcedure(procCode, options.TraceTag);
        }

        string entryLine = ExpandMacros(options.EntryTemplate, procName, procKind, moduleName);
        string exitLine = ExpandMacros(options.ExitTemplate, procName, procKind, moduleName);

        string[] lines = procCode.Replace("\r\n", "\n").Split('\n');
        var outputLines = new List<string>();

        // 1. Localizar el final de la cabecera (teniendo en cuenta ' _')
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

        // 2. Si la siguiente línea es 'On Error ...', la incluimos primero antes de la traza de entrada
        int nextBodyIndex = headerEndIndex + 1;
        if (nextBodyIndex < lines.Length)
        {
            string nextClean = CodeFormatter.StripCommentsAndStrings(lines[nextBodyIndex]).Trim();
            if (OnErrorRegex.IsMatch(nextClean))
            {
                outputLines.Add(lines[nextBodyIndex]);
                nextBodyIndex++;
            }
        }

        // Insertar traza de entrada
        outputLines.Add("    " + entryLine.Trim());

        // 3. Procesar el resto del cuerpo, inyectando traza de salida antes de 'Exit Sub/Function/Property' y antes de 'End Sub/Function/Property'
        for (int i = nextBodyIndex; i < lines.Length; i++)
        {
            string line = lines[i];
            string clean = CodeFormatter.StripCommentsAndStrings(line).Trim();

            if (options.InjectExitTrace)
            {
                if (ExitProcRegex.IsMatch(clean) || EndProcRegex.IsMatch(clean))
                {
                    // No duplicar si la línea anterior ya fue una traza de salida
                    if (outputLines.Count == 0 || !outputLines[^1].Contains(options.TraceTag, StringComparison.OrdinalIgnoreCase))
                    {
                        outputLines.Add("    " + exitLine.Trim());
                    }
                }
            }

            outputLines.Add(line);
        }

        return string.Join("\r\n", outputLines);
    }

    /// <summary>
    /// Elimina todas las trazas inyectadas de un procedimiento individual.
    /// </summary>
    public static string RemoveTracesFromProcedure(string procCode, string traceTag = TracingOptions.DefaultTag)
    {
        if (string.IsNullOrWhiteSpace(procCode)) return procCode;

        string[] lines = procCode.Replace("\r\n", "\n").Split('\n');
        var outputLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.Contains(traceTag, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Omitir línea de traza inyectada
            }
            outputLines.Add(line);
        }

        return string.Join("\r\n", outputLines);
    }

    /// <summary>
    /// Inyecta trazas en todos los procedimientos de un módulo o código completo.
    /// </summary>
    public static string InjectTracesIntoModule(string code, string moduleName, TracingOptions options, out int modifiedProceduresCount)
    {
        modifiedProceduresCount = 0;
        if (string.IsNullOrWhiteSpace(code)) return code;

        var (header, procs) = ProcedureSorter.ParseProcedures(code);
        if (procs.Count == 0) return code;

        var updatedProcs = new List<ProcedureSortItem>();

        foreach (var p in procs)
        {
            string updated = InjectTracesIntoProcedure(p.Code, p.Name, p.Kind, moduleName, options);
            if (!string.Equals(p.Code, updated, StringComparison.Ordinal))
            {
                p.Code = updated;
                modifiedProceduresCount++;
            }
            updatedProcs.Add(p);
        }

        return ProcedureSorter.GenerateSortedCode(header, updatedProcs, preserveComments: true);
    }

    /// <summary>
    /// Elimina todas las trazas de todos los procedimientos de un módulo.
    /// </summary>
    public static string RemoveTracesFromModule(string code, string traceTag, out int cleanedProceduresCount)
    {
        cleanedProceduresCount = 0;
        if (string.IsNullOrWhiteSpace(code)) return code;

        var (header, procs) = ProcedureSorter.ParseProcedures(code);
        if (procs.Count == 0) return code;

        var updatedProcs = new List<ProcedureSortItem>();

        foreach (var p in procs)
        {
            if (HasInjectedTraces(p.Code, traceTag))
            {
                string cleaned = RemoveTracesFromProcedure(p.Code, traceTag);
                if (!string.Equals(p.Code, cleaned, StringComparison.Ordinal))
                {
                    p.Code = cleaned;
                    cleanedProceduresCount++;
                }
            }
            updatedProcs.Add(p);
        }

        return ProcedureSorter.GenerateSortedCode(header, updatedProcs, preserveComments: true);
    }

    private static string ExpandMacros(string template, string procName, string procKind, string moduleName)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return template
            .Replace("%PROC_NAME%", procName, StringComparison.OrdinalIgnoreCase)
            .Replace("%MODULE_NAME%", string.IsNullOrWhiteSpace(moduleName) ? "Module" : moduleName, StringComparison.OrdinalIgnoreCase)
            .Replace("%DATE%", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), StringComparison.OrdinalIgnoreCase)
            .Replace("%USER%", Environment.UserName, StringComparison.OrdinalIgnoreCase);
    }
}
