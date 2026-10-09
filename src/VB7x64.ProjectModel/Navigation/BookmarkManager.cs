using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VB7x64.ProjectModel.Navigation;

public class BookmarkManager
{
    private static readonly Lazy<BookmarkManager> _instance = new(() => new BookmarkManager());
    public static BookmarkManager Instance => _instance.Value;

    private readonly object _lock = new();
    private readonly Dictionary<string, SortedSet<int>> _bookmarks = new(StringComparer.OrdinalIgnoreCase);

    public event Action? BookmarksChanged;

    private string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    /// <summary>
    /// Alterna el estado del marcador en una línea de un archivo. Retorna true si se agregó, false si se eliminó.
    /// </summary>
    public bool ToggleBookmark(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(filePath) || line <= 0) return false;
        string key = Normalize(filePath);
        bool added;

        lock (_lock)
        {
            if (!_bookmarks.TryGetValue(key, out var set))
            {
                set = [];
                _bookmarks[key] = set;
            }

            if (set.Contains(line))
            {
                set.Remove(line);
                if (set.Count == 0) _bookmarks.Remove(key);
                added = false;
            }
            else
            {
                set.Add(line);
                added = true;
            }
        }

        BookmarksChanged?.Invoke();
        return added;
    }

    public bool HasBookmark(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(filePath) || line <= 0) return false;
        string key = Normalize(filePath);

        lock (_lock)
        {
            return _bookmarks.TryGetValue(key, out var set) && set.Contains(line);
        }
    }

    public IReadOnlyCollection<int> GetBookmarks(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return Array.Empty<int>();
        string key = Normalize(filePath);

        lock (_lock)
        {
            if (_bookmarks.TryGetValue(key, out var set))
            {
                return set.ToArray();
            }
        }
        return Array.Empty<int>();
    }

    public int TotalBookmarkCount
    {
        get
        {
            lock (_lock)
            {
                return _bookmarks.Values.Sum(s => s.Count);
            }
        }
    }

    public (string FilePath, int Line)? GetNextBookmark(string currentFilePath, int currentLine)
    {
        if (string.IsNullOrWhiteSpace(currentFilePath)) return null;
        string currentKey = Normalize(currentFilePath);

        lock (_lock)
        {
            if (_bookmarks.Count == 0) return null;

            // 1. Buscar en el archivo actual después de la línea indicada
            if (_bookmarks.TryGetValue(currentKey, out var currentSet))
            {
                foreach (int line in currentSet)
                {
                    if (line > currentLine)
                    {
                        return (currentKey, line);
                    }
                }
            }

            // 2. Si no hay más en el archivo actual, buscar en los siguientes archivos ordenados
            var files = _bookmarks.Keys.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            int currentIndex = files.IndexOf(currentKey);

            for (int step = 1; step <= files.Count; step++)
            {
                int nextIndex = (currentIndex + step + files.Count) % files.Count;
                string nextFile = files[nextIndex];
                var nextSet = _bookmarks[nextFile];
                if (nextSet.Count > 0)
                {
                    // Si dimos la vuelta al archivo actual
                    if (string.Equals(nextFile, currentKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return (currentKey, nextSet.First());
                    }
                    return (nextFile, nextSet.First());
                }
            }
        }

        return null;
    }

    public (string FilePath, int Line)? GetPreviousBookmark(string currentFilePath, int currentLine)
    {
        if (string.IsNullOrWhiteSpace(currentFilePath)) return null;
        string currentKey = Normalize(currentFilePath);

        lock (_lock)
        {
            if (_bookmarks.Count == 0) return null;

            // 1. Buscar en el archivo actual antes de la línea indicada (la mayor línea < currentLine)
            if (_bookmarks.TryGetValue(currentKey, out var currentSet))
            {
                var prevLines = currentSet.Where(l => l < currentLine).ToList();
                if (prevLines.Count > 0)
                {
                    return (currentKey, prevLines.Last());
                }
            }

            // 2. Si no hay más en el archivo actual antes, buscar en archivos anteriores
            var files = _bookmarks.Keys.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            int currentIndex = files.IndexOf(currentKey);
            if (currentIndex < 0) currentIndex = 0;

            for (int step = 1; step <= files.Count; step++)
            {
                int prevIndex = (currentIndex - step + files.Count) % files.Count;
                string prevFile = files[prevIndex];
                var prevSet = _bookmarks[prevFile];
                if (prevSet.Count > 0)
                {
                    if (string.Equals(prevFile, currentKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return (currentKey, prevSet.Last());
                    }
                    return (prevFile, prevSet.Last());
                }
            }
        }

        return null;
    }

    public void ClearAllBookmarks()
    {
        lock (_lock)
        {
            _bookmarks.Clear();
        }
        BookmarksChanged?.Invoke();
    }

    public void ClearBookmarksForFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        string key = Normalize(filePath);
        lock (_lock)
        {
            _bookmarks.Remove(key);
        }
        BookmarksChanged?.Invoke();
    }
}
