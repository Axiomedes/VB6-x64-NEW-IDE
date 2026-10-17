using System;
using System.IO;

namespace ViBa7.IDE.Logging;

public static class AppLogger
{
    private static readonly object _lock = new();
    private static readonly string _logDirectory;
    private static readonly string _logFilePath;

    public static event Action<string>? LogMessageReceived;

    static AppLogger()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(localAppData, "VB7x64", "logs");
        Directory.CreateDirectory(_logDirectory);
        _logFilePath = Path.Combine(_logDirectory, $"ide_{DateTime.Now:yyyyMMdd}.log");
    }

    public static string LogFilePath => _logFilePath;

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(message);
        var cur = ex;
        while (cur != null)
        {
            sb.AppendLine();
            sb.Append($" ---> {cur.GetType().FullName}: {cur.Message}");
            if (!string.IsNullOrWhiteSpace(cur.StackTrace))
            {
                sb.AppendLine();
                sb.Append(cur.StackTrace);
            }
            cur = cur.InnerException;
        }
        Log("ERROR", sb.ToString());
    }

    private static void Log(string level, string message)
    {
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
            catch
            {
                // Fallback silencioso si no se puede escribir en disco
            }
        }

        try
        {
            LogMessageReceived?.Invoke(entry);
        }
        catch
        {
            // Ignorar errores de suscriptores
        }
    }
}
