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
    public string Theme { get; set; } = "Classic";
    
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
}
