using System;
using System.Text.RegularExpressions;
using System.Windows;
using ICSharpCode.AvalonEdit;

namespace ViBa7.IDE.Editor;

public partial class FindReplaceDialog : Window
{
    private readonly TextEditor _editor;

    public FindReplaceDialog(TextEditor editor)
    {
        InitializeComponent();
        _editor = editor;

        if (!string.IsNullOrEmpty(_editor.SelectedText))
        {
            TxtFind.Text = _editor.SelectedText;
        }

        Loaded += (_, _) =>
        {
            TxtFind.Focus();
            TxtFind.SelectAll();
        };

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    private Regex? BuildRegex()
    {
        string pattern = TxtFind.Text;
        if (string.IsNullOrEmpty(pattern)) return null;

        pattern = Regex.Escape(pattern);
        if (ChkWholeWord.IsChecked == true)
        {
            pattern = $@"\b{pattern}\b";
        }

        var options = RegexOptions.Multiline;
        if (ChkMatchCase.IsChecked != true)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(pattern, options);
    }

    private void BtnFindNext_Click(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;

        string text = _editor.Text;
        int startIndex = _editor.CaretOffset;
        if (startIndex >= text.Length) startIndex = 0;

        var match = regex.Match(text, startIndex);
        if (!match.Success)
        {
            // Búsqueda circular desde el inicio
            match = regex.Match(text, 0);
        }

        if (match.Success)
        {
            _editor.Select(match.Index, match.Length);
            _editor.ScrollToLine(_editor.Document.GetLineByOffset(match.Index).LineNumber);
        }
        else
        {
            MessageBox.Show($"No se encontró el texto '{TxtFind.Text}'.", "Buscar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnReplace_Click(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;

        if (_editor.SelectionLength > 0 && regex.IsMatch(_editor.SelectedText))
        {
            _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength, TxtReplace.Text);
        }

        BtnFindNext_Click(sender, e);
    }

    private void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;

        string currentText = _editor.Text;
        var matches = regex.Matches(currentText);
        if (matches.Count == 0)
        {
            MessageBox.Show($"No se encontraron coincidencias para '{TxtFind.Text}'.", "Reemplazar todo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using (_editor.Document.RunUpdate())
        {
            // Reemplazar de atrás hacia adelante para no invalidar offsets
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                _editor.Document.Replace(m.Index, m.Length, TxtReplace.Text);
            }
        }

        MessageBox.Show($"Se reemplazaron {matches.Count} coincidencias.", "Reemplazar todo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
