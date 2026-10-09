using System;
using System.IO;
using System.Text.Json;

namespace VB7x64.IDE.Settings;

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
    public double WindowHeight { get; set; } = 700;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public bool IsMaximized { get; set; } = false;

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
}
