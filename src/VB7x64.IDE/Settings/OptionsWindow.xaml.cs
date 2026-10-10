using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VB7x64.ProjectModel.Settings;

namespace VB7x64.IDE.Settings;

public partial class OptionsWindow : Window
{
    private readonly AppSettings _settings;
    private TextBox? _activeTextBox;
    private bool _isInitializing = true;

    private static readonly string[] PaletteColors =
    [
        "#000000", "#FFFFFF", "#1E1E1E", "#D4D4D4",
        "#0000FF", "#569CD6", "#008000", "#6A9955",
        "#A31515", "#CE9178", "#795E26", "#DCDCAA",
        "#2B91AF", "#4EC9B0", "#000080", "#4FC1FF",
        "#808080", "#858585", "#FF0000", "#00FF00",
        "#FFFF00", "#FF8000", "#800080", "#FF00FF"
    ];

    public OptionsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // 1. Fuentes de programación comunes
        var fonts = new List<string> { "Consolas", "Courier New", "Cascadia Code", "Lucida Console", "Segoe UI Mono" };
        foreach (var font in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f))
        {
            if (!fonts.Contains(font)) fonts.Add(font);
        }
        CmbFontFamily.ItemsSource = fonts;
        CmbFontFamily.SelectedItem = _settings.FontFamily;

        // 2. Tamaños de fuente
        CmbFontSize.ItemsSource = new List<double> { 10.0, 11.0, 12.0, 13.0, 14.0, 16.0, 18.0, 20.0 };
        CmbFontSize.SelectedItem = _settings.FontSize;

        // 3. Tamaños de tab
        CmbTabSize.ItemsSource = new List<int> { 2, 4, 8 };
        CmbTabSize.SelectedItem = _settings.TabSize;

        ChkLineNumbers.IsChecked = _settings.ShowLineNumbers;
        ChkCreateBak.IsChecked = _settings.CreateBackupBeforeSave;
        ChkCurrentLineHighlight.IsChecked = _settings.EnableCurrentLineHighlight;
        ChkBranchLines.IsChecked = _settings.EnableBranchLines;
        ChkEndOfLineDetails.IsChecked = _settings.EnableEndOfLineDetails;

        ChkEnableIntelliSense.IsChecked = _settings.EnableIntelliSense;
        ChkAutoListMembers.IsChecked = _settings.AutoListMembers;
        ChkAutoQuickInfo.IsChecked = _settings.AutoQuickInfo;
        ChkCompleteWordOnTabOrEnter.IsChecked = _settings.CompleteWordOnTabOrEnter;

        CmbProjectExplorerMode.SelectedIndex = _settings.ProjectExplorerMode == ProjectExplorerMode.Standard ? 0 : 1;

        // 4. Codificaciones comunes
        CmbEncoding.ItemsSource = new List<string> { "Windows-1252", "ISO-8859-1", "UTF-8", "Unicode (UTF-16)" };
        CmbEncoding.SelectedItem = _settings.DefaultEncoding;

        // 5. Cargar colores actuales del editor
        TxtBgColor.Text = _settings.EditorBackgroundColor;
        TxtFgColor.Text = _settings.EditorForegroundColor;
        TxtKeywordColor.Text = _settings.KeywordColor;
        TxtMethodColor.Text = _settings.MethodNameColor;
        TxtCommentColor.Text = _settings.CommentColor;
        TxtStringColor.Text = _settings.StringColor;
        TxtTypeColor.Text = _settings.TypeColor;
        TxtConstantColor.Text = _settings.ConstantColor;
        TxtNumberColor.Text = _settings.NumberColor;
        TxtLineNumbersColor.Text = _settings.LineNumbersColor;
        TxtCurrentLineColor.Text = _settings.CurrentLineHighlightColor;
        TxtBranchLinesColor.Text = _settings.BranchLinesColor;
        TxtEndOfLineDetailsColor.Text = _settings.EndOfLineDetailsColor;

        _activeTextBox = TxtBgColor;

        // 6. Construir paleta rápida
        BuildPalette();

        _isInitializing = false;
        UpdateSwatchesAndPreview();
    }

    private void BuildPalette()
    {
        PalettePanel.Children.Clear();
        foreach (string hex in PaletteColors)
        {
            var btn = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Background = TryGetBrush(hex, Brushes.Gray),
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
                ToolTip = hex,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            btn.Click += (_, _) =>
            {
                if (_activeTextBox != null)
                {
                    _activeTextBox.Text = hex;
                    _activeTextBox.Focus();
                    _activeTextBox.SelectAll();
                }
            };

            PalettePanel.Children.Add(btn);
        }
    }

    private void ColorField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            _activeTextBox = tb;
        }
    }

    private void ColorField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitializing)
        {
            UpdateSwatchesAndPreview();
        }
    }

    private void UpdateSwatchesAndPreview()
    {
        var bg = TryGetBrush(TxtBgColor.Text, Brushes.White);
        var fg = TryGetBrush(TxtFgColor.Text, Brushes.Black);
        var kw = TryGetBrush(TxtKeywordColor.Text, Brushes.Blue);
        var method = TryGetBrush(TxtMethodColor.Text, new SolidColorBrush(Color.FromRgb(121, 94, 38)));
        var comment = TryGetBrush(TxtCommentColor.Text, Brushes.Green);
        var str = TryGetBrush(TxtStringColor.Text, new SolidColorBrush(Color.FromRgb(163, 21, 21)));
        var type = TryGetBrush(TxtTypeColor.Text, new SolidColorBrush(Color.FromRgb(43, 145, 175)));
        var constant = TryGetBrush(TxtConstantColor.Text, new SolidColorBrush(Color.FromRgb(0, 0, 128)));
        var num = TryGetBrush(TxtNumberColor.Text, Brushes.Black);
        var lineNum = TryGetBrush(TxtLineNumbersColor.Text, Brushes.Gray);
        var curLine = TryGetBrush(TxtCurrentLineColor.Text, new SolidColorBrush(Color.FromRgb(240, 244, 250)));
        var branch = TryGetBrush(TxtBranchLinesColor.Text, new SolidColorBrush(Color.FromRgb(208, 208, 208)));
        var eol = TryGetBrush(TxtEndOfLineDetailsColor.Text, new SolidColorBrush(Color.FromRgb(138, 138, 138)));

        SwatchBg.Background = bg;
        SwatchFg.Background = fg;
        SwatchKeyword.Background = kw;
        SwatchMethod.Background = method;
        SwatchComment.Background = comment;
        SwatchString.Background = str;
        SwatchType.Background = type;
        SwatchConstant.Background = constant;
        SwatchNumber.Background = num;
        SwatchLineNumbers.Background = lineNum;
        SwatchCurrentLine.Background = curLine;
        SwatchBranchLines.Background = branch;
        SwatchEndOfLineDetails.Background = eol;

        // Actualizar vista previa en tiempo real
        PreviewBorder.Background = bg;
        PreviewTextBlock.Inlines.Clear();

        void AddLine(int line, Action addContent)
        {
            PreviewTextBlock.Inlines.Add(new Run($"{line,2}  ") { Foreground = lineNum });
            addContent();
            PreviewTextBlock.Inlines.Add(new LineBreak());
        }

        AddLine(1, () => PreviewTextBlock.Inlines.Add(new Run("' Ejemplo de código VB6") { Foreground = comment, FontStyle = FontStyles.Italic }));
        AddLine(2, () => PreviewTextBlock.Inlines.Add(new Run("Option Explicit") { Foreground = kw, FontWeight = FontWeights.Bold }));
        AddLine(3, () => { });
        AddLine(4, () =>
        {
            PreviewTextBlock.Inlines.Add(new Run("Public Sub ") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("CalcularTotal") { Foreground = method, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("(") { Foreground = fg });
            PreviewTextBlock.Inlines.Add(new Run("ByVal ") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("precio ") { Foreground = fg });
            PreviewTextBlock.Inlines.Add(new Run("As ") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("Currency") { Foreground = type });
            PreviewTextBlock.Inlines.Add(new Run(")") { Foreground = fg });
        });
        AddLine(5, () =>
        {
            PreviewTextBlock.Inlines.Add(new Run("    Dim ") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("mensaje ") { Foreground = fg });
            PreviewTextBlock.Inlines.Add(new Run("As ") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("String") { Foreground = type });
        });
        AddLine(6, () =>
        {
            PreviewTextBlock.Inlines.Add(new Run("    mensaje = ") { Foreground = fg });
            PreviewTextBlock.Inlines.Add(new Run("\"Total: $\"") { Foreground = str });
            PreviewTextBlock.Inlines.Add(new Run(" & (precio + ") { Foreground = fg });
            PreviewTextBlock.Inlines.Add(new Run("100") { Foreground = num });
            PreviewTextBlock.Inlines.Add(new Run(")") { Foreground = fg });
        });
        AddLine(7, () =>
        {
            PreviewTextBlock.Inlines.Add(new Run("    Call ") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("MsgBox") { Foreground = method, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("(mensaje, ") { Foreground = fg });
            PreviewTextBlock.Inlines.Add(new Run("vbInformation") { Foreground = constant });
            PreviewTextBlock.Inlines.Add(new Run(")") { Foreground = fg });
        });
        AddLine(8, () =>
        {
            PreviewTextBlock.Inlines.Add(new Run("End Sub") { Foreground = kw, FontWeight = FontWeights.Bold });
            PreviewTextBlock.Inlines.Add(new Run("  ' [Public Sub CalcularTotal]") { Foreground = eol, FontStyle = FontStyles.Italic });
        });
    }

    private static SolidColorBrush TryGetBrush(string hex, Brush fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return (SolidColorBrush)fallback;
            string cleanHex = hex.Trim();
            if (!cleanHex.StartsWith('#') && cleanHex.Length is 6 or 8)
            {
                cleanHex = "#" + cleanHex;
            }
            if (ColorConverter.ConvertFromString(cleanHex) is Color c)
            {
                return new SolidColorBrush(c);
            }
        }
        catch { }

        return (SolidColorBrush)fallback;
    }

    private void BtnThemeClassic_Click(object sender, RoutedEventArgs e)
    {
        SetThemeValues(
            bg: "#FFFFFF",
            fg: "#000000",
            kw: "#0000FF",
            method: "#795E26",
            comment: "#008000",
            str: "#A31515",
            type: "#2B91AF",
            constant: "#000080",
            num: "#000000",
            lineNum: "#2B91AF",
            curLine: "#F0F4FA",
            branch: "#D0D0D0",
            eol: "#8A8A8A"
        );
    }

    private void BtnThemeDark_Click(object sender, RoutedEventArgs e)
    {
        SetThemeValues(
            bg: "#1E1E1E",
            fg: "#D4D4D4",
            kw: "#569CD6",
            method: "#DCDCAA",
            comment: "#6A9955",
            str: "#CE9178",
            type: "#4EC9B0",
            constant: "#4FC1FF",
            num: "#B5CEA8",
            lineNum: "#858585",
            curLine: "#2A2D2E",
            branch: "#454545",
            eol: "#6A737D"
        );
    }

    private void BtnThemeContrast_Click(object sender, RoutedEventArgs e)
    {
        SetThemeValues(
            bg: "#000000",
            fg: "#FFFFFF",
            kw: "#00FFFF",
            method: "#FFD700",
            comment: "#00FF00",
            str: "#FF8080",
            type: "#00FF80",
            constant: "#FF00FF",
            num: "#FFFF80",
            lineNum: "#FFFF00",
            curLine: "#262626",
            branch: "#FFFF00",
            eol: "#00FFFF"
        );
    }

    private void SetThemeValues(string bg, string fg, string kw, string method, string comment, string str, string type, string constant, string num, string lineNum, string curLine, string branch, string eol)
    {
        _isInitializing = true;
        TxtBgColor.Text = bg;
        TxtFgColor.Text = fg;
        TxtKeywordColor.Text = kw;
        TxtMethodColor.Text = method;
        TxtCommentColor.Text = comment;
        TxtStringColor.Text = str;
        TxtTypeColor.Text = type;
        TxtConstantColor.Text = constant;
        TxtNumberColor.Text = num;
        TxtLineNumbersColor.Text = lineNum;
        TxtCurrentLineColor.Text = curLine;
        TxtBranchLinesColor.Text = branch;
        TxtEndOfLineDetailsColor.Text = eol;
        _isInitializing = false;

        UpdateSwatchesAndPreview();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CmbFontFamily.SelectedItem is string font) _settings.FontFamily = font;
        if (CmbFontSize.SelectedItem is double size) _settings.FontSize = size;
        if (CmbTabSize.SelectedItem is int tab) _settings.TabSize = tab;
        _settings.ShowLineNumbers = ChkLineNumbers.IsChecked == true;
        _settings.CreateBackupBeforeSave = ChkCreateBak.IsChecked == true;
        _settings.EnableCurrentLineHighlight = ChkCurrentLineHighlight.IsChecked == true;
        _settings.EnableBranchLines = ChkBranchLines.IsChecked == true;
        _settings.EnableEndOfLineDetails = ChkEndOfLineDetails.IsChecked == true;

        _settings.EnableIntelliSense = ChkEnableIntelliSense.IsChecked == true;
        _settings.AutoListMembers = ChkAutoListMembers.IsChecked == true;
        _settings.AutoQuickInfo = ChkAutoQuickInfo.IsChecked == true;
        _settings.CompleteWordOnTabOrEnter = ChkCompleteWordOnTabOrEnter.IsChecked == true;

        _settings.ProjectExplorerMode = CmbProjectExplorerMode.SelectedIndex == 0 ? ProjectExplorerMode.Standard : ProjectExplorerMode.Advanced;
        if (CmbEncoding.SelectedItem is string enc) _settings.DefaultEncoding = enc;

        // Guardar configuración de colores
        _settings.EditorBackgroundColor = TxtBgColor.Text.Trim();
        _settings.EditorForegroundColor = TxtFgColor.Text.Trim();
        _settings.KeywordColor = TxtKeywordColor.Text.Trim();
        _settings.MethodNameColor = TxtMethodColor.Text.Trim();
        _settings.CommentColor = TxtCommentColor.Text.Trim();
        _settings.StringColor = TxtStringColor.Text.Trim();
        _settings.TypeColor = TxtTypeColor.Text.Trim();
        _settings.ConstantColor = TxtConstantColor.Text.Trim();
        _settings.NumberColor = TxtNumberColor.Text.Trim();
        _settings.LineNumbersColor = TxtLineNumbersColor.Text.Trim();
        _settings.CurrentLineHighlightColor = TxtCurrentLineColor.Text.Trim();
        _settings.BranchLinesColor = TxtBranchLinesColor.Text.Trim();
        _settings.EndOfLineDetailsColor = TxtEndOfLineDetailsColor.Text.Trim();

        _settings.Save();
        DialogResult = true;
        Close();
    }
}
