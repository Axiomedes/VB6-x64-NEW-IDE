using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VB7x64.ProjectModel.Settings;

public class AppSettings
{
    private static readonly string SettingsFilePath;

    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 13.0;
    public int TabSize { get; set; } = 4;
    public bool ShowLineNumbers { get; set; } = true;
    public bool CreateBackupBeforeSave { get; set; } = true;
    public string DefaultEncoding { get; set; } = "Windows-1252";
    public string Theme { get; set; } = "Clásico VB6";

    // Colores personalizables del editor de código
    public string EditorBackgroundColor { get; set; } = "#FFFFFF";
    public string EditorForegroundColor { get; set; } = "#000000";
    public string LineNumbersColor { get; set; } = "#2B91AF";
    public string KeywordColor { get; set; } = "#0000FF";
    public string MethodNameColor { get; set; } = "#795E26";
    public string CommentColor { get; set; } = "#008000";
    public string StringColor { get; set; } = "#A31515";
    public string NumberColor { get; set; } = "#000000";
    public string TypeColor { get; set; } = "#2B91AF";
    public string ConstantColor { get; set; } = "#000080";
    
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public bool IsMaximized { get; set; } = false;

    public double ProjectExplorerWidth { get; set; } = 260.0;
    public bool ShowProjectExplorer { get; set; } = true;
    public bool ShowOutputPanel { get; set; } = false;
    public double OutputPanelHeight { get; set; } = 140.0;

    public List<string> RecentProjects { get; set; } = [];
    public List<string> RecentFiles { get; set; } = [];

    static AppSettings()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string configDir = Path.Combine(localAppData, "VB7x64", "config");
        Directory.CreateDirectory(configDir);
        SettingsFilePath = Path.Combine(configDir, "settings.json");
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // En caso de error al deserializar, retornar opciones por defecto
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silencioso ante fallos de persistencia
        }
    }

    public void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentProjects.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentProjects.Insert(0, path);
        if (RecentProjects.Count > 10) RecentProjects.RemoveRange(10, RecentProjects.Count - 10);
    }

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 10) RecentFiles.RemoveRange(10, RecentFiles.Count - 10);
    }

    public void ApplyThemePreset(string presetName)
    {
        Theme = presetName;
        switch (presetName.Trim().ToLowerInvariant())
        {
            case "dark":
            case "oscuro":
            case "oscuro moderno":
                Theme = "Oscuro Moderno";
                EditorBackgroundColor = "#1E1E1E";
                EditorForegroundColor = "#D4D4D4";
                LineNumbersColor = "#858585";
                KeywordColor = "#569CD6";
                MethodNameColor = "#DCDCAA";
                CommentColor = "#6A9955";
                StringColor = "#CE9178";
                NumberColor = "#B5CEA8";
                TypeColor = "#4EC9B0";
                ConstantColor = "#4FC1FF";
                break;

            case "contrast":
            case "alto contraste":
                Theme = "Alto Contraste";
                EditorBackgroundColor = "#000000";
                EditorForegroundColor = "#FFFFFF";
                LineNumbersColor = "#FFFF00";
                KeywordColor = "#00FFFF";
                MethodNameColor = "#FFD700";
                CommentColor = "#00FF00";
                StringColor = "#FF8080";
                NumberColor = "#FFFF80";
                TypeColor = "#00FF80";
                ConstantColor = "#FF00FF";
                break;

            default:
                Theme = "Clásico VB6";
                EditorBackgroundColor = "#FFFFFF";
                EditorForegroundColor = "#000000";
                LineNumbersColor = "#2B91AF";
                KeywordColor = "#0000FF";
                MethodNameColor = "#795E26";
                CommentColor = "#008000";
                StringColor = "#A31515";
                NumberColor = "#000000";
                TypeColor = "#2B91AF";
                ConstantColor = "#000080";
                break;
        }
    }
}
