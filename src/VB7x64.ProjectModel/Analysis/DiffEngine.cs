using System;
using System.Collections.Generic;
using System.Linq;

namespace VB7x64.ProjectModel.Analysis;

public enum DiffLineType
{
    Unchanged,
    Inserted,
    Deleted,
    Modified
}

public class DiffLine
{
    public DiffLineType Type { get; set; }
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? OldText { get; set; }

    public string TypeSymbol => Type switch
    {
        DiffLineType.Inserted => "+",
        DiffLineType.Deleted => "-",
        DiffLineType.Modified => "~",
        _ => " "
    };
}

public class DiffSummary
{
    public int InsertedCount { get; set; }
    public int DeletedCount { get; set; }
    public int ModifiedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int TotalChanges => InsertedCount + DeletedCount + ModifiedCount;
    public bool HasDifferences => TotalChanges > 0;
}

public class DiffResult
{
    public List<DiffLine> Lines { get; set; } = new();
    public DiffSummary Summary { get; set; } = new();
}

public static class DiffEngine
{
    /// <summary>
    /// Compara dos textos línea por línea y produce una lista estructurada de diferencias.
    /// </summary>
    public static DiffResult Compare(string oldText, string newText)
    {
        oldText ??= string.Empty;
        newText ??= string.Empty;

        string[] oldLines = oldText.Replace("\r\n", "\n").Split('\n');
        string[] newLines = newText.Replace("\r\n", "\n").Split('\n');

        // Calcular Longest Common Subsequence (LCS)
        int m = oldLines.Length;
        int n = newLines.Length;

        int[,] lcs = new int[m + 1, n + 1];

        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                {
                    lcs[i + 1, j + 1] = lcs[i, j] + 1;
                }
                else
                {
                    lcs[i + 1, j + 1] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }
        }

        // Reconstruir lista de cambios desde el final hacia el inicio
        var rawDiff = new List<DiffLine>();
        int x = m;
        int y = n;

        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && string.Equals(oldLines[x - 1], newLines[y - 1], StringComparison.Ordinal))
            {
                rawDiff.Add(new DiffLine
                {
                    Type = DiffLineType.Unchanged,
                    OldLineNumber = x,
                    NewLineNumber = y,
                    Text = oldLines[x - 1]
                });
                x--;
                y--;
            }
            else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
            {
                rawDiff.Add(new DiffLine
                {
                    Type = DiffLineType.Inserted,
                    NewLineNumber = y,
                    Text = newLines[y - 1]
                });
                y--;
            }
            else if (x > 0 && (y == 0 || lcs[x, y - 1] < lcs[x - 1, y]))
            {
                rawDiff.Add(new DiffLine
                {
                    Type = DiffLineType.Deleted,
                    OldLineNumber = x,
                    Text = oldLines[x - 1]
                });
                x--;
            }
        }

        rawDiff.Reverse();

        // Optimización: Emparejar bloques consecutivos de Deleted e Inserted como Modified si corresponden a cambios en el mismo lugar
        var consolidated = ConsolidateModifiedLines(rawDiff);

        var summary = new DiffSummary
        {
            InsertedCount = consolidated.Count(l => l.Type == DiffLineType.Inserted),
            DeletedCount = consolidated.Count(l => l.Type == DiffLineType.Deleted),
            ModifiedCount = consolidated.Count(l => l.Type == DiffLineType.Modified),
            UnchangedCount = consolidated.Count(l => l.Type == DiffLineType.Unchanged)
        };

        return new DiffResult
        {
            Lines = consolidated,
            Summary = summary
        };
    }

    private static List<DiffLine> ConsolidateModifiedLines(List<DiffLine> input)
    {
        var result = new List<DiffLine>();
        int i = 0;

        while (i < input.Count)
        {
            // Detectar bloque contiguo de Deleted seguido de Inserted
            if (input[i].Type == DiffLineType.Deleted)
            {
                int delStart = i;
                while (i < input.Count && input[i].Type == DiffLineType.Deleted)
                {
                    i++;
                }
                int insStart = i;
                while (i < input.Count && input[i].Type == DiffLineType.Inserted)
                {
                    i++;
                }

                int delCount = insStart - delStart;
                int insCount = i - insStart;

                if (delCount > 0 && insCount > 0)
                {
                    // Emparejar los que se puedan como Modified
                    int min = Math.Min(delCount, insCount);
                    for (int k = 0; k < min; k++)
                    {
                        var delLine = input[delStart + k];
                        var insLine = input[insStart + k];

                        result.Add(new DiffLine
                        {
                            Type = DiffLineType.Modified,
                            OldLineNumber = delLine.OldLineNumber,
                            NewLineNumber = insLine.NewLineNumber,
                            Text = insLine.Text,
                            OldText = delLine.Text
                        });
                    }

                    // Sobrantes de deleted
                    for (int k = min; k < delCount; k++)
                    {
                        result.Add(input[delStart + k]);
                    }

                    // Sobrantes de inserted
                    for (int k = min; k < insCount; k++)
                    {
                        result.Add(input[insStart + k]);
                    }
                }
                else
                {
                    // Solo Deleted sin Inserted
                    for (int k = delStart; k < insStart; k++)
                    {
                        result.Add(input[k]);
                    }
                }
            }
            else
            {
                result.Add(input[i]);
                i++;
            }
        }

        return result;
    }
}
