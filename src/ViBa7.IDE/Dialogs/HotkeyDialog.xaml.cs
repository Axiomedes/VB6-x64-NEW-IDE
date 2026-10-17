using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ViBa7.ProjectModel.Designers;

namespace ViBa7.IDE.Dialogs;

public partial class HotkeyDialog : Window
{
    private readonly string _originalHeader;
    private List<HotkeyItem> _items = new();

    public string ResultingHeader { get; private set; } = string.Empty;
    public bool HasItems => _items.Count > 0;

    public HotkeyDialog(string formHeaderAndDesign, string formName = "Formulario")
    {
        InitializeComponent();

        _originalHeader = formHeaderAndDesign ?? string.Empty;
        TxtHeader.Text = $"Formulario: {formName}";

        _items = HotkeyEngine.ParseHotkeys(_originalHeader);
        RefreshSummaryAndGrid();
    }

    private void RefreshSummaryAndGrid()
    {
        int conflicts = _items.Count(i => i.HasConflict);
        int totalHotkeys = _items.Count(i => i.HotkeyChar.HasValue);
        var available = HotkeyEngine.GetAvailableLetters(_items);

        TxtConflictCount.Text = $"{conflicts} conflictos";
        TxtTotalHotkeys.Text = $"{totalHotkeys} con tecla rápida";
        TxtAvailableLetters.Text = $"Libres: {string.Join(", ", available.Take(12))}{(available.Count > 12 ? "..." : "")}";

        if (conflicts == 0)
        {
            BadgeConflicts.Visibility = Visibility.Collapsed;
        }
        else
        {
            BadgeConflicts.Visibility = Visibility.Visible;
        }

        DgHotkeys.ItemsSource = null;
        DgHotkeys.ItemsSource = _items;
    }

    private void BtnAutoResolve_Click(object sender, RoutedEventArgs e)
    {
        var available = HotkeyEngine.GetAvailableLetters(_items);
        var usedNow = new HashSet<char>(_items.Where(i => i.HotkeyChar.HasValue && !i.HasConflict).Select(i => i.HotkeyChar!.Value));

        // Para cada item con conflicto, buscar una letra libre que esté en su texto
        foreach (var item in _items.Where(i => i.HasConflict))
        {
            string cleanText = item.OriginalCaption.Replace("&", "");
            char? bestLetter = null;

            foreach (char c in cleanText)
            {
                char upper = char.ToUpperInvariant(c);
                if (char.IsLetter(upper) && !usedNow.Contains(upper))
                {
                    bestLetter = upper;
                    break;
                }
            }

            if (bestLetter.HasValue)
            {
                // Reubicar el '&' en la letra elegida
                int letterIdx = cleanText.IndexOf(bestLetter.Value, StringComparison.OrdinalIgnoreCase);
                if (letterIdx >= 0)
                {
                    item.NewCaption = cleanText.Substring(0, letterIdx) + "&" + cleanText.Substring(letterIdx);
                    item.HotkeyChar = bestLetter.Value;
                    item.HasConflict = false;
                    usedNow.Add(bestLetter.Value);
                }
            }
        }

        RefreshSummaryAndGrid();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var modifiedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _items)
        {
            if (!string.Equals(item.OriginalCaption, item.NewCaption, StringComparison.Ordinal))
            {
                modifiedMap[item.ControlName] = item.NewCaption;
            }
        }

        if (modifiedMap.Count == 0)
        {
            DialogResult = false;
            return;
        }

        ResultingHeader = HotkeyEngine.ApplyCaptions(_originalHeader, modifiedMap);
        DialogResult = true;
    }
}
