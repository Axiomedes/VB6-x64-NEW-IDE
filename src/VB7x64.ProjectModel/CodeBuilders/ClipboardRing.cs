using System;
using System.Collections.Generic;

namespace VB7x64.ProjectModel.CodeBuilders;

public class ClipboardRing
{
    private static readonly ClipboardRing _instance = new();
    public static ClipboardRing Instance => _instance;

    private readonly List<string> _items = [];
    private const int MaxCapacity = 15;

    public event Action? HistoryChanged;

    public IReadOnlyList<string> Items
    {
        get
        {
            lock (_items)
            {
                return _items.ToArray();
            }
        }
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_items)
        {
            // Evitar duplicados idénticos consecutivos en la cima
            if (_items.Count > 0 && _items[0] == text)
                return;

            // Si ya existía más abajo, removerlo para moverlo a la cima
            _items.Remove(text);

            _items.Insert(0, text);

            if (_items.Count > MaxCapacity)
            {
                _items.RemoveAt(_items.Count - 1);
            }
        }

        HistoryChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_items)
        {
            _items.Clear();
        }
        HistoryChanged?.Invoke();
    }
}
