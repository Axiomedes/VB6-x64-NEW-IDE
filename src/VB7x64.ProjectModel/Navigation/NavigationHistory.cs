using System;
using System.Collections.Generic;
using System.IO;

namespace VB7x64.ProjectModel.Navigation;

public record NavigationPoint(
    string FilePath,
    int LineNumber,
    int ColumnNumber = 1
);

public class NavigationHistory
{
    private static readonly Lazy<NavigationHistory> _instance = new(() => new NavigationHistory());
    public static NavigationHistory Instance => _instance.Value;

    private readonly List<NavigationPoint> _backStack = [];
    private readonly List<NavigationPoint> _forwardStack = [];
    private const int MaxHistory = 50;

    public event Action? HistoryChanged;

    public bool CanNavigateBack => _backStack.Count > 0;
    public bool CanNavigateForward => _forwardStack.Count > 0;

    private string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    /// <summary>
    /// Registra un punto de navegación visitado.
    /// </summary>
    public void RecordNavigation(string filePath, int line, int column = 1)
    {
        if (string.IsNullOrWhiteSpace(filePath) || line <= 0) return;
        string normPath = Normalize(filePath);
        var point = new NavigationPoint(normPath, line, Math.Max(1, column));

        // Evitar duplicar si es la misma ubicación o líneas adyacentes inmediatas en el mismo archivo
        if (_backStack.Count > 0)
        {
            var last = _backStack[^1];
            if (string.Equals(last.FilePath, normPath, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(last.LineNumber - line) <= 2)
            {
                return;
            }
        }

        _backStack.Add(point);
        if (_backStack.Count > MaxHistory)
        {
            _backStack.RemoveAt(0);
        }

        _forwardStack.Clear();
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// Retrocede a la posición anterior, guardando la ubicación actual en el historial de avance.
    /// </summary>
    public NavigationPoint? NavigateBackward(string currentFilePath, int currentLine, int currentCol = 1)
    {
        if (_backStack.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(currentFilePath) && currentLine > 0)
        {
            _forwardStack.Add(new NavigationPoint(Normalize(currentFilePath), currentLine, currentCol));
        }

        var target = _backStack[^1];
        _backStack.RemoveAt(_backStack.Count - 1);

        HistoryChanged?.Invoke();
        return target;
    }

    /// <summary>
    /// Avanza a la posición siguiente, guardando la ubicación actual en el historial de retroceso.
    /// </summary>
    public NavigationPoint? NavigateForward(string currentFilePath, int currentLine, int currentCol = 1)
    {
        if (_forwardStack.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(currentFilePath) && currentLine > 0)
        {
            _backStack.Add(new NavigationPoint(Normalize(currentFilePath), currentLine, currentCol));
        }

        var target = _forwardStack[^1];
        _forwardStack.RemoveAt(_forwardStack.Count - 1);

        HistoryChanged?.Invoke();
        return target;
    }

    public void Clear()
    {
        _backStack.Clear();
        _forwardStack.Clear();
        HistoryChanged?.Invoke();
    }
}
