using System.Windows;

namespace VB7x64.IDE.Editor;

public partial class InputPromptDialog : Window
{
    public string InputValue { get; private set; } = string.Empty;

    public InputPromptDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        LblPrompt.Text = prompt;
        TxtInput.Text = defaultValue;

        Loaded += (_, _) =>
        {
            TxtInput.Focus();
            TxtInput.SelectAll();
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        InputValue = TxtInput.Text.Trim();
        DialogResult = true;
    }
}
