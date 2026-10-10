using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VB7x64.ProjectModel.Documents;
using VB7x64.ProjectModel.Formatting;
using VB7x64.ProjectModel.Navigation;
using VB7x64.ProjectModel.Vbp;

namespace VB7x64.ProjectModel.Analysis;

public enum IssueSeverity
{
    Error,
    Warning,
    Info
}

public class AnalysisIssue
{
    public string RuleId { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; } = IssueSeverity.Warning;
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string ContainerName { get; set; } = string.Empty;

    public string DisplaySeverity => Severity switch
    {
        IssueSeverity.Error => "🔴 Error",
        IssueSeverity.Warning => "🟠 Advertencia",
        IssueSeverity.Info => "🔵 Sugerencia",
        _ => "⚪ Info"
    };
}

public static class CodeAnalyzer
{
    private static readonly Regex VariableDeclarationRegex = new(
        @"^\s*Dim\s+([a-zA-Z0-9_]+)(?:\s*\([^)]*\))?\s+As\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> StandardEventSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "_Click", "_DblClick", "_Change", "_KeyDown", "_KeyUp", "_KeyPress",
        "_MouseDown", "_MouseMove", "_MouseUp", "_Timer", "_GotFocus", "_LostFocus",
        "_Load", "_Unload", "_QueryUnload", "_Resize", "_Paint", "_Initialize",
        "_Terminate", "_Activate", "_Deactivate", "_Scroll", "_Validate", "_ItemCheck"
    };

    /// <summary>
    /// Analiza el código de un archivo o búfer en memoria y devuelve la lista de advertencias y problemas detectados.
    /// </summary>
    public static List<AnalysisIssue> AnalyzeText(string code, string filePath = "Documento")
    {
        var issues = new List<AnalysisIssue>();
        if (string.IsNullOrWhiteSpace(code)) return issues;

        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) fileName = filePath;

        string[] lines = code.Replace("\r\n", "\n").Split('\n');

        // 1. Regla: Falta de 'Option Explicit'
        CheckOptionExplicit(lines, filePath, fileName, issues);

        // 2. Regla: Procedimientos privados no utilizados (Código muerto)
        CheckUnusedProcedures(code, lines, filePath, fileName, issues);

        // 3. Regla: Variables declaradas pero nunca utilizadas
        CheckUnusedVariables(code, lines, filePath, fileName, issues);

        // 4. Regla: Procedimientos completamente vacíos
        CheckEmptyProcedures(code, filePath, fileName, issues);

        return issues.OrderBy(i => i.LineNumber).ToList();
    }

    /// <summary>
    /// Analiza un archivo de código en disco (omitiendo cabeceras de formulario para concentrarse en código).
    /// </summary>
    public static List<AnalysisIssue> AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath)) return new List<AnalysisIssue>();

        try
        {
            var doc = SafeFileStorage.LoadDocument(filePath);
            return AnalyzeText(doc.Text, filePath);
        }
        catch
        {
            try
            {
                string raw = File.ReadAllText(filePath);
                return AnalyzeText(raw, filePath);
            }
            catch
            {
                return new List<AnalysisIssue>();
            }
        }
    }

    /// <summary>
    /// Analiza todos los archivos de un proyecto VB6 completo (.vbp).
    /// </summary>
    public static List<AnalysisIssue> AnalyzeProject(VB6Project project)
    {
        var issues = new List<AnalysisIssue>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in project.Items.Where(i => i.IsEditable))
        {
            string fullPath = item.AbsolutePath;
            if (!string.IsNullOrWhiteSpace(fullPath) && visitedPaths.Add(fullPath))
            {
                issues.AddRange(AnalyzeFile(fullPath));
            }
        }

        return issues.OrderBy(i => i.Severity).ThenBy(i => i.FileName).ThenBy(i => i.LineNumber).ToList();
    }

    private static void CheckOptionExplicit(string[] lines, string filePath, string fileName, List<AnalysisIssue> issues)
    {
        bool hasOptionExplicit = false;

        for (int i = 0; i < Math.Min(lines.Length, 100); i++)
        {
            string clean = CodeFormatter.StripCommentsAndStrings(lines[i]).Trim();
            if (clean.StartsWith("Option Explicit", StringComparison.OrdinalIgnoreCase))
            {
                hasOptionExplicit = true;
                break;
            }
        }

        if (!hasOptionExplicit)
        {
            issues.Add(new AnalysisIssue
            {
                RuleId = "VB7_W003_NO_OPTION_EXPLICIT",
                Severity = IssueSeverity.Warning,
                Message = "Falta la directiva 'Option Explicit'. Las variables no declaradas pueden causar errores difíciles de detectar.",
                FilePath = filePath,
                FileName = fileName,
                LineNumber = 1,
                ContainerName = "Declaraciones Generales"
            });
        }
    }

    private static void CheckUnusedProcedures(string fullCode, string[] lines, string filePath, string fileName, List<AnalysisIssue> issues)
    {
        var procs = ProcedureParser.ParseProcedures(fullCode);

        foreach (var proc in procs)
        {
            // Solo nos interesan procedimientos privados
            if (!proc.Scope.Equals("Private", StringComparison.OrdinalIgnoreCase))
                continue;

            // Omitir manejadores de eventos conocidos
            if (IsKnownEventHandler(proc.Name))
                continue;

            // Contar cuántas veces aparece el nombre del procedimiento como token en todo el código
            var regex = new Regex($@"\b{Regex.Escape(proc.Name)}\b", RegexOptions.IgnoreCase);
            var matches = regex.Matches(fullCode);

            // Si solo coincide una vez (su propia declaración), es código muerto
            if (matches.Count <= 1)
            {
                issues.Add(new AnalysisIssue
                {
                    RuleId = "VB7_W001_DEAD_CODE",
                    Severity = IssueSeverity.Warning,
                    Message = $"El procedimiento privado '{proc.Name}' nunca es invocado dentro de este archivo (código muerto).",
                    FilePath = filePath,
                    FileName = fileName,
                    LineNumber = proc.LineNumber,
                    ContainerName = proc.Name
                });
            }
        }
    }

    private static void CheckUnusedVariables(string fullCode, string[] lines, string filePath, string fileName, List<AnalysisIssue> issues)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string clean = CodeFormatter.StripCommentsAndStrings(raw).Trim();

            var match = VariableDeclarationRegex.Match(clean);
            if (match.Success)
            {
                string varName = match.Groups[1].Value.Trim();

                // Contar apariciones de la variable en todo el código
                var regex = new Regex($@"\b{Regex.Escape(varName)}\b", RegexOptions.IgnoreCase);
                var matches = regex.Matches(fullCode);

                if (matches.Count <= 1)
                {
                    issues.Add(new AnalysisIssue
                    {
                        RuleId = "VB7_W002_UNUSED_VAR",
                        Severity = IssueSeverity.Warning,
                        Message = $"La variable '{varName}' fue declarada pero nunca se utiliza.",
                        FilePath = filePath,
                        FileName = fileName,
                        LineNumber = i + 1,
                        ContainerName = "Variable"
                    });
                }
            }
        }
    }

    private static void CheckEmptyProcedures(string fullCode, string filePath, string fileName, List<AnalysisIssue> issues)
    {
        var (_, procs) = ProcedureSorter.ParseProcedures(fullCode);

        foreach (var proc in procs)
        {
            string[] procLines = proc.Code.Replace("\r\n", "\n").Split('\n');
            int executableLines = 0;

            // Omitir primera (header) y última (End)
            for (int i = 1; i < procLines.Length - 1; i++)
            {
                string clean = CodeFormatter.StripCommentsAndStrings(procLines[i]).Trim();
                if (!string.IsNullOrEmpty(clean))
                {
                    executableLines++;
                }
            }

            if (executableLines == 0)
            {
                issues.Add(new AnalysisIssue
                {
                    RuleId = "VB7_I001_EMPTY_PROC",
                    Severity = IssueSeverity.Info,
                    Message = $"El procedimiento '{proc.Name}' está vacío y no contiene sentencias ejecutables.",
                    FilePath = filePath,
                    FileName = fileName,
                    LineNumber = proc.StartLine,
                    ContainerName = proc.Name
                });
            }
        }
    }

    private static bool IsKnownEventHandler(string procName)
    {
        foreach (var suffix in StandardEventSuffixes)
        {
            if (procName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (procName.StartsWith("Form_", StringComparison.OrdinalIgnoreCase) ||
            procName.StartsWith("Class_", StringComparison.OrdinalIgnoreCase) ||
            procName.StartsWith("UserControl_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
