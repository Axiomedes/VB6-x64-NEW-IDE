using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ViBa7.ProjectModel.IntelliSense;

/// <summary>
/// Loads additional IntelliSense members from the JSON file generated from the VB6 type library.
/// </summary>
internal static class ExternalIntelliSenseLoader
{
    public static readonly Dictionary<string, ControlInfo> ControlMap = new(StringComparer.OrdinalIgnoreCase);

    static ExternalIntelliSenseLoader()
    {
        // Adjust the path as needed; assuming the IDE resources folder location.
        var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "VB6IntelliSense.json");
        if (!File.Exists(jsonPath))
            return;
        try
        {
            var json = File.ReadAllText(jsonPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, ControlInfo>>(json);
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    ControlMap[kvp.Key] = kvp.Value;
                }
            }
        }
        catch
        {
            // Silently ignore errors; IDE will continue with built‑in catalog.
        }
    }

    public class ControlInfo
    {
        public string Interface { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new();
    }
}
