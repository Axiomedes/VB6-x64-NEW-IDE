using System.Windows;

namespace ViBa7.IDE.Editor;

public partial class GoToLineDialog : Window
{
    private readonly int _maxLines;
    public int SelectedLine { get; private set; }

    public GoToLineDialog(int currentLine, int maxLines)
    {
        InitializeComponent();
        _maxLines = maxLines;
        LblPrompt.Text = $"Número de línea (1 - {maxLines}):";
        TxtLineNumber.Text = currentLine.ToString();
        TxtLineNumber.SelectAll();
        Loaded += (_, _) => TxtLineNumber.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtLineNumber.Text, out int line) && line >= 1 && line <= _maxLines)
        {
            SelectedLine = line;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show($"Por favor ingresa un número de línea válido entre 1 y {_maxLines}.", "Ir a la línea", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtLineNumber.Focus();
            TxtLineNumber.SelectAll();
        }
    }
}
