using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VB7x64.ProjectModel.Settings;

namespace VB7x64.IDE.Settings;

public partial class OptionsWindow : Window
{
    private readonly AppSettings _settings;

    public OptionsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // Fuentes de programación comunes
        var fonts = new List<string> { "Consolas", "Courier New", "Cascadia Code", "Lucida Console", "Segoe UI Mono" };
        foreach (var font in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f))
        {
            if (!fonts.Contains(font)) fonts.Add(font);
        }
        CmbFontFamily.ItemsSource = fonts;
        CmbFontFamily.SelectedItem = _settings.FontFamily;

        // Tamaños de fuente
        CmbFontSize.ItemsSource = new List<double> { 10.0, 11.0, 12.0, 13.0, 14.0, 16.0, 18.0, 20.0 };
        CmbFontSize.SelectedItem = _settings.FontSize;

        // Tamaños de tab
        CmbTabSize.ItemsSource = new List<int> { 2, 4, 8 };
        CmbTabSize.SelectedItem = _settings.TabSize;

        ChkLineNumbers.IsChecked = _settings.ShowLineNumbers;
        ChkCreateBak.IsChecked = _settings.CreateBackupBeforeSave;

        // Codificaciones comunes
        CmbEncoding.ItemsSource = new List<string> { "Windows-1252", "ISO-8859-1", "UTF-8", "Unicode (UTF-16)" };
        CmbEncoding.SelectedItem = _settings.DefaultEncoding;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CmbFontFamily.SelectedItem is string font) _settings.FontFamily = font;
        if (CmbFontSize.SelectedItem is double size) _settings.FontSize = size;
        if (CmbTabSize.SelectedItem is int tab) _settings.TabSize = tab;
        _settings.ShowLineNumbers = ChkLineNumbers.IsChecked == true;
        _settings.CreateBackupBeforeSave = ChkCreateBak.IsChecked == true;
        if (CmbEncoding.SelectedItem is string enc) _settings.DefaultEncoding = enc;

        _settings.Save();
        DialogResult = true;
        Close();
    }
}
