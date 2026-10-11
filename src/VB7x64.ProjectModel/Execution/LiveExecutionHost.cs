using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VB7x64.ProjectModel.Navigation;
using VB7x64.ProjectModel.Vbp;

namespace VB7x64.ProjectModel.Execution;

public record LoadedProcedure(
    string Name,
    string? ContainerName,
    List<string> Parameters,
    List<string> BodyLines,
    int StartLine
);

/// <summary>
/// Orquestador general del motor de ejecución en caliente (Hot-Run Host).
/// Indexa los procedimientos de los archivos del proyecto, resuelve invocaciones
/// de eventos y coordina la ejecución en vivo sin compilar a disco.
/// </summary>
public class LiveExecutionHost
{
    private readonly VbExecutionContext _context;
    private readonly VbStatementInterpreter _interpreter;
    private readonly Dictionary<string, LoadedProcedure> _procedures = new(StringComparer.OrdinalIgnoreCase);

    public VbExecutionContext Context => _context;
    public VbStatementInterpreter Interpreter => _interpreter;

    public LiveExecutionHost(VbExecutionContext? context = null)
    {
        _context = context ?? new VbExecutionContext();
        _interpreter = new VbStatementInterpreter(_context);
    }

    /// <summary>
    /// Indexa los procedimientos de un archivo o código fuente para hacerlos ejecutables.
    /// </summary>
    public void LoadSourceCode(string code, string? containerName = null)
    {
        var blocks = Advanced.VB6BlockParser.ParseBlocks(code);
        string[] allLines = code.Replace("\r\n", "\n").Split('\n');

        var procRegex = new System.Text.RegularExpressions.Regex(
            @"^\s*(?:(?:Public|Private|Friend|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\s+([a-zA-Z0-9_]+)(?:\((.*?)\))?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        foreach (var block in blocks)
        {
            if (block.BlockType is "Sub" or "Function" or "Property")
            {
                var match = procRegex.Match(block.Header);
                if (!match.Success) continue;

                string procName = match.Groups[1].Value;
                string rawParams = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;

                var paramNames = new List<string>();
                if (!string.IsNullOrWhiteSpace(rawParams))
                {
                    foreach (var p in rawParams.Split(','))
                    {
                        string pTrim = p.Trim();
                        pTrim = System.Text.RegularExpressions.Regex.Replace(pTrim, @"^(?:ByVal|ByRef|Optional)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        int asIdx = pTrim.IndexOf(" As ", StringComparison.OrdinalIgnoreCase);
                        string pName = asIdx > 0 ? pTrim[..asIdx].Trim() : pTrim;
                        if (!string.IsNullOrEmpty(pName)) paramNames.Add(pName);
                    }
                }

                int start = Math.Max(0, block.StartLine - 1);
                int end = Math.Min(allLines.Length - 1, block.EndLine - 1);

                var body = new List<string>();
                for (int l = start + 1; l < end; l++)
                {
                    body.Add(allLines[l]);
                }

                var proc = new LoadedProcedure(
                    Name: procName,
                    ContainerName: containerName,
                    Parameters: paramNames,
                    BodyLines: body,
                    StartLine: block.StartLine
                );

                string key = !string.IsNullOrEmpty(containerName)
                    ? $"{containerName}.{procName}"
                    : procName;

                _procedures[key] = proc;
                _procedures[procName] = proc;
            }
        }
    }

    /// <summary>
    /// Invoca un procedimiento por nombre (ej. "Form_Load" o "Command1_Click").
    /// </summary>
    public FlowSignal InvokeProcedure(string procedureName, string? containerName = null, params VbValue[] args)
    {
        string lookupKey = !string.IsNullOrEmpty(containerName)
            ? $"{containerName}.{procedureName}"
            : procedureName;

        if (!_procedures.TryGetValue(lookupKey, out var proc))
        {
            if (!_procedures.TryGetValue(procedureName, out proc))
            {
                return FlowSignal.Normal; // Si no existe el manejador de evento, no hace nada
            }
        }

        if (_context.State == ExecutionState.Stopped)
        {
            _context.State = ExecutionState.Running;
        }

        _context.PushFrame(proc.Name, proc.ContainerName);

        try
        {
            // Asignar argumentos a los parámetros del procedimiento
            for (int i = 0; i < proc.Parameters.Count; i++)
            {
                string paramName = proc.Parameters[i];
                var val = i < args.Length ? args[i] : VbValue.Empty;
                _context.SetVariable(paramName, val);
            }

            return _interpreter.ExecuteBlock(proc.BodyLines, containerName, proc.StartLine);
        }
        finally
        {
            _context.PopFrame();
        }
    }

    /// <summary>
    /// Evalúa una expresión interactiva (útil para la Ventana Inmediata con `?` o comandos directos).
    /// </summary>
    public VbValue EvaluateImmediate(string input)
    {
        string trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return VbValue.Empty;

        // Comando de impresión: `? expr` o `Print expr`
        if (trimmed.StartsWith('?'))
        {
            string expr = trimmed[1..].Trim();
            return VbExpressionEvaluator.Evaluate(expr, _context);
        }

        if (trimmed.StartsWith("Print ", StringComparison.OrdinalIgnoreCase))
        {
            string expr = trimmed[6..].Trim();
            return VbExpressionEvaluator.Evaluate(expr, _context);
        }

        // Ejecutar como sentencia
        _interpreter.ExecuteSingleStatement(trimmed);
        return VbValue.Empty;
    }

    public void Reset()
    {
        _procedures.Clear();
        _context.Clear();
    }
}
