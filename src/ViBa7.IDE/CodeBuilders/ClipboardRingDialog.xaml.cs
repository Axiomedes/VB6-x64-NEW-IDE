using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ViBa7.ProjectModel.CodeBuilders;

namespace ViBa7.IDE.CodeBuilders;

public partial class ClipboardRingDialog : Window
{
    public string SelectedText { get; private set; } = "";

    public ClipboardRingDialog()
    {
        InitializeComponent();
        LoadItems();
    }

    private void LoadItems()
    {
        LstClipboardItems.Items.Clear();
        var items = ClipboardRing.Instance.Items;

        if (items.Count == 0)
        {
            LstClipboardItems.Items.Add(new ListBoxItem
            {
                Content = "(El portapapeles está vacío)",
                IsEnabled = false,
                FontStyle = FontStyles.Italic
            });
            TxtPreview.Text = "";
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            string item = items[i];
            string firstLine = item.Replace("\r", "").Split('\n').FirstOrDefault()?.Trim() ?? "";
            if (firstLine.Length > 35) firstLine = firstLine[..32] + "...";
            if (string.IsNullOrWhiteSpace(firstLine)) firstLine = $"(Fragmento #{i + 1})";

            var lbi = new ListBoxItem
            {
                Content = $"{i + 1}. {firstLine}",
                Tag = item,
                ToolTip = item
            };
            LstClipboardItems.Items.Add(lbi);
        }

        LstClipboardItems.SelectedIndex = 0;
    }

    private void LstClipboardItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstClipboardItems.SelectedItem is ListBoxItem { Tag: string text })
        {
            TxtPreview.Text = text;
        }
        else
        {
            TxtPreview.Text = "";
        }
    }

    private void LstClipboardItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstClipboardItems.SelectedItem is ListBoxItem { Tag: string text })
        {
            SelectedText = text;
            DialogResult = true;
            Close();
        }
    }

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        if (LstClipboardItems.SelectedItem is ListBoxItem { Tag: string text })
        {
            SelectedText = text;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Selecciona un fragmento para pegar.", "Portapapeles Múltiple", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ClipboardRing.Instance.Clear();
        LoadItems();
    }
}
