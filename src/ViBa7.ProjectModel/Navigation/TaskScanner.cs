using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ViBa7.ProjectModel.Documents;

namespace ViBa7.ProjectModel.Navigation;

public enum TaskPriority
{
    High,
    Normal,
    Low
}

public record TaskItem(
    string Tag,
    TaskPriority Priority,
    string Description,
    string FilePath,
    string FileName,
    int LineNumber,
    string LineText
)
{
    public string PriorityDisplay => Priority switch
    {
        TaskPriority.High => "Alta",
        TaskPriority.Normal => "Normal",
        TaskPriority.Low => "Baja",
        _ => "Normal"
    };

    public string PriorityIcon => Priority switch
    {
        TaskPriority.High => "🔴",
        TaskPriority.Normal => "🟠",
        TaskPriority.Low => "🔵",
        _ => "⚪"
    };

    public string LocationDisplay => $"{FileName} (Línea {LineNumber})";
}

public static class TaskScanner
{
    private static readonly Regex TaskRegex = new(
        @"(?:'|Rem\s+).*?\b(BUG|FIXME|TODO|HACK|NOTE|UNDONE|INFO)\b(?::|\s+-|\s+)?\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Escanea una lista de archivos buscando marcadores de tareas en comentarios (TODO, FIXME, BUG, etc.).
    /// </summary>
    public static List<TaskItem> ScanTasks(
        IEnumerable<string> filePaths,
        Func<string, string?>? getOpenDocumentText = null)
    {
        var tasks = new List<TaskItem>();

        foreach (var filePath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(filePath)) continue;

            string? content = null;
            if (getOpenDocumentText != null)
            {
                content = getOpenDocumentText(filePath);
            }

            if (content == null)
            {
                if (!File.Exists(filePath)) continue;
                try
                {
                    var doc = SafeFileStorage.LoadDocument(filePath);
                    content = doc.Text;
                }
                catch
                {
                    continue;
                }
            }

            string fileName = Path.GetFileName(filePath);
            using var reader = new StringReader(content);
            string? line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Verificar si la línea contiene un comentario con etiqueta de tarea
                var match = TaskRegex.Match(line);
                if (match.Success)
                {
                    string tag = match.Groups[1].Value.ToUpperInvariant();
                    string desc = match.Groups[2].Value.Trim();

                    // Si la descripción quedó vacía, usar el texto completo después de la etiqueta
                    if (string.IsNullOrEmpty(desc))
                    {
                        desc = $"(Sin descripción: {tag})";
                    }

                    TaskPriority priority = tag switch
                    {
                        "BUG" or "FIXME" => TaskPriority.High,
                        "TODO" or "HACK" => TaskPriority.Normal,
                        "NOTE" or "UNDONE" or "INFO" => TaskPriority.Low,
                        _ => TaskPriority.Normal
                    };

                    tasks.Add(new TaskItem(
                        Tag: tag,
                        Priority: priority,
                        Description: desc,
                        FilePath: filePath,
                        FileName: fileName,
                        LineNumber: lineNumber,
                        LineText: trimmed
                    ));
                }
            }
        }

        // Ordenar por prioridad (Alta primero), luego por archivo y línea
        tasks.Sort((a, b) =>
        {
            int pComp = a.Priority.CompareTo(b.Priority);
            if (pComp != 0) return pComp;
            int fComp = string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            if (fComp != 0) return fComp;
            return a.LineNumber.CompareTo(b.LineNumber);
        });

        return tasks;
    }
}
