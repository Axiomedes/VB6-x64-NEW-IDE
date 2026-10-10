using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VB7x64.ProjectModel.Documents;
using VB7x64.ProjectModel.Navigation;
using VB7x64.ProjectModel.Vbp;

namespace VB7x64.ProjectModel.Analysis;

public class FileCodeStatistics
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int TotalLines { get; set; }
    public int CodeLines { get; set; } // SLOC
    public int CommentLines { get; set; }
    public int BlankLines { get; set; }
    public int ProcedureCount { get; set; }

    public double CommentPercentage => TotalLines > 0 
        ? Math.Round((CommentLines * 100.0) / TotalLines, 1) 
        : 0.0;
}

public class ProjectStatisticsSummary
{
    public string ProjectName { get; set; } = string.Empty;
    public List<FileCodeStatistics> Files { get; set; } = new();

    public int TotalFiles => Files.Count;
    public int TotalLines => Files.Sum(f => f.TotalLines);
    public int TotalCodeLines => Files.Sum(f => f.CodeLines);
    public int TotalCommentLines => Files.Sum(f => f.CommentLines);
    public int TotalBlankLines => Files.Sum(f => f.BlankLines);
    public int TotalProcedures => Files.Sum(f => f.ProcedureCount);

    public int FormCount => Files.Count(f => f.FileType.Contains("Formulario", StringComparison.OrdinalIgnoreCase));
    public int ModuleCount => Files.Count(f => f.FileType.Contains("Módulo", StringComparison.OrdinalIgnoreCase));
    public int ClassCount => Files.Count(f => f.FileType.Contains("Clase", StringComparison.OrdinalIgnoreCase));
    public int UserControlCount => Files.Count(f => f.FileType.Contains("Control", StringComparison.OrdinalIgnoreCase));

    public double OverallCommentPercentage => TotalLines > 0 
        ? Math.Round((TotalCommentLines * 100.0) / TotalLines, 1) 
        : 0.0;
}

public static class ProjectStatistics
{
    /// <summary>
    /// Analiza métricas de código a partir del texto de un búfer.
    /// </summary>
    public static FileCodeStatistics AnalyzeText(string text, string filePath = "Documento")
    {
        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) fileName = filePath;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        string fileType = GetFileTypeDescription(ext);

        if (string.IsNullOrEmpty(text))
        {
            return new FileCodeStatistics
            {
                FilePath = filePath,
                FileName = fileName,
                FileType = fileType,
                TotalLines = 0,
                CodeLines = 0,
                CommentLines = 0,
                BlankLines = 0,
                ProcedureCount = 0
            };
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        int total = lines.Length;
        int blank = 0;
        int comment = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                blank++;
            }
            else if (trimmed.StartsWith('\'') || trimmed.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
            {
                comment++;
            }
        }

        int codeLines = Math.Max(0, total - blank - comment);
        int procCount = ProcedureParser.ParseProcedures(text).Count;

        return new FileCodeStatistics
        {
            FilePath = filePath,
            FileName = fileName,
            FileType = fileType,
            TotalLines = total,
            CodeLines = codeLines,
            CommentLines = comment,
            BlankLines = blank,
            ProcedureCount = procCount
        };
    }

    /// <summary>
    /// Analiza un archivo en disco, extrayendo el código limpio (omitiendo cabeceras de formulario si aplica).
    /// </summary>
    public static FileCodeStatistics AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return AnalyzeText(string.Empty, filePath);
        }

        try
        {
            var doc = SafeFileStorage.LoadDocument(filePath);
            return AnalyzeText(doc.Text, filePath);
        }
        catch
        {
            // Si no se puede abrir como EditorDocument, intentar lectura directa
            try
            {
                string raw = File.ReadAllText(filePath);
                return AnalyzeText(raw, filePath);
            }
            catch
            {
                return AnalyzeText(string.Empty, filePath);
            }
        }
    }

    /// <summary>
    /// Analiza un proyecto VB6 (.vbp) completo.
    /// </summary>
    public static ProjectStatisticsSummary AnalyzeProject(VB6Project project)
    {
        var summary = new ProjectStatisticsSummary
        {
            ProjectName = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileNameWithoutExtension(project.ProjectFilePath) : project.Name
        };

        if (string.IsNullOrWhiteSpace(summary.ProjectName))
        {
            summary.ProjectName = "Proyecto VB6";
        }

        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in project.Items.Where(i => i.IsEditable))
        {
            string fullPath = item.AbsolutePath;
            if (!string.IsNullOrWhiteSpace(fullPath) && visitedPaths.Add(fullPath))
            {
                summary.Files.Add(AnalyzeFile(fullPath));
            }
        }

        return summary;
    }

    private static string GetFileTypeDescription(string ext) => ext switch
    {
        ".bas" => "Módulo Estándar (.bas)",
        ".frm" => "Formulario (.frm)",
        ".cls" => "Módulo de Clase (.cls)",
        ".ctl" => "Control de Usuario (.ctl)",
        ".pag" => "Página de Propiedades (.pag)",
        ".dob" => "Documento de Usuario (.dob)",
        ".dsr" => "Diseñador (.dsr)",
        _ => "Archivo de Código"
    };
}
