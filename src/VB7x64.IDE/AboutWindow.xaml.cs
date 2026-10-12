using System;
using System.Windows;

namespace VB7x64.IDE;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        TxtAuthor.Text = $"Author: {AppVersion.Author}";
        TxtVersion.Text = $"Versión: {AppVersion.VersionString}";
        TxtLicenseHeader.Text = "Licencia: GPL-3.0";
        TxtLicenseDetail.Text = $"• Licencia: {AppVersion.License}";
        TxtPlatform.Text = $"• Plataforma: Windows {(Environment.Is64BitProcess ? "x64 (Nativo)" : "x86")}";
        TxtRuntime.Text = $"• Runtime: .NET {Environment.Version}";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
