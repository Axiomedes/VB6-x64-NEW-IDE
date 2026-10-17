using System;
using System.Windows;

namespace ViBa7.IDE;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        TxtAuthor.Text = $"Author: {AppVersion.Author}";
        TxtVersion.Text = $"Versión: {AppVersion.VersionString}";
        TxtLicenseHeader.Text = "Licencia: GPL-3.0";
        TxtLicenseDetail.Text = $"• Licencia: {AppVersion.License}";
        TxtPlatform.Text = $"• Plataforma: Windows {(Environment.Is64BitProcess ? "x64" : "x86 (Nativo 32-bit / WOW64)")}";
        TxtRuntime.Text = $"• Runtime: .NET {Environment.Version}";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
