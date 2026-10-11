using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VB7x64.ProjectModel.Designers;
using VB7x64.ProjectModel.Documents;

namespace VB7x64.ProjectModel.Vbp;

public class VB6Project
{
    public string ProjectFilePath { get; set; } = string.Empty;
    public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath) ?? string.Empty;

    public string Name { get; set; } = string.Empty;
    public string ProjectType { get; set; } = "Exe";
    public string Startup { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ExeName32 { get; set; } = string.Empty;

    public List<ProjectItem> Items { get; } = [];
    public List<ProjectReference> References { get; } = [];
    public List<string> RawLines { get; } = [];
    public Dictionary<string, string> Settings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int MajorVer
    {
        get => int.TryParse(Settings.TryGetValue("MajorVer", out var v) ? v : "1", out var n) ? n : 1;
        set => Settings["MajorVer"] = value.ToString();
    }

    public int MinorVer
    {
        get => int.TryParse(Settings.TryGetValue("MinorVer", out var v) ? v : "0", out var n) ? n : 0;
        set => Settings["MinorVer"] = value.ToString();
    }

    public int RevisionVer
    {
        get => int.TryParse(Settings.TryGetValue("RevisionVer", out var v) ? v : "0", out var n) ? n : 0;
        set => Settings["RevisionVer"] = value.ToString();
    }

    public bool AutoIncrementVer
    {
        get => Settings.TryGetValue("AutoIncrementVer", out var v) && (v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
        set => Settings["AutoIncrementVer"] = value ? "1" : "0";
    }

    public string CompanyName
    {
        get => Settings.TryGetValue("CompanyName", out var v) ? v : string.Empty;
        set => Settings["CompanyName"] = value;
    }

    public string FileDescription
    {
        get => Settings.TryGetValue("FileDescription", out var v) ? v : Title;
        set => Settings["FileDescription"] = value;
    }

    public string LegalCopyright
    {
        get => Settings.TryGetValue("LegalCopyright", out var v) ? v : string.Empty;
        set => Settings["LegalCopyright"] = value;
    }

    public IEnumerable<ProjectItem> Forms => Items.FindAll(i => i.Type == ProjectItemType.Form);
    public IEnumerable<ProjectItem> Modules => Items.FindAll(i => i.Type == ProjectItemType.Module);
    public IEnumerable<ProjectItem> Classes => Items.FindAll(i => i.Type == ProjectItemType.Class);
    public IEnumerable<ProjectItem> UserControls => Items.FindAll(i => i.Type == ProjectItemType.UserControl);
    public IEnumerable<ProjectItem> PropertyPages => Items.FindAll(i => i.Type == ProjectItemType.PropertyPage);
    public IEnumerable<ProjectItem> ActiveXControls => Items.FindAll(i => i.Type == ProjectItemType.ActiveXControl);

    /// <summary>
    /// Bibliotecas OCX / TypeLibs cargadas e integradas en este proyecto.
    /// </summary>
    public List<VB7x64.ProjectModel.ActiveX.OcxLibraryInfo> LoadedOcxLibraries { get; } = [];

    /// <summary>
    /// Registra un control ActiveX en el proyecto.
    /// </summary>
    public void AddActiveXControl(VB7x64.ProjectModel.ActiveX.OcxLibraryInfo libInfo, string relativeOrAbsolutePath)
    {
        ArgumentNullException.ThrowIfNull(libInfo);

        if (!LoadedOcxLibraries.Any(l => string.Equals(l.Guid, libInfo.Guid, StringComparison.OrdinalIgnoreCase) || string.Equals(l.FilePath, libInfo.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            LoadedOcxLibraries.Add(libInfo);
        }

        string fileName = Path.GetFileName(relativeOrAbsolutePath);
        if (!Items.Any(i => i.Type == ProjectItemType.ActiveXControl && (string.Equals(i.AbsolutePath, libInfo.FilePath, StringComparison.OrdinalIgnoreCase) || string.Equals(i.Name, libInfo.LibraryName, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFileName(i.RelativePath), fileName, StringComparison.OrdinalIgnoreCase))))
        {
            string guidFormatted = libInfo.Guid;
            if (!guidFormatted.StartsWith('{')) guidFormatted = "{" + guidFormatted + "}";
            string rawLine = $"Object={guidFormatted}#{libInfo.MajorVersion}.{libInfo.MinorVersion}#0; {fileName}";

            Items.Add(new ProjectItem
            {
                Type = ProjectItemType.ActiveXControl,
                Name = libInfo.LibraryName,
                RelativePath = relativeOrAbsolutePath,
                AbsolutePath = libInfo.FilePath,
                RawLine = rawLine,
                FileExists = true
            });

            RawLines.Add(rawLine);
        }
    }

    private readonly Dictionary<string, FormDesignModel> _cachedUserControlModels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Busca un ProjectItem de tipo UserControl por su nombre, tipo o nombre de archivo.
    /// </summary>
    public ProjectItem? FindUserControl(string typeOrName)
    {
        if (string.IsNullOrWhiteSpace(typeOrName)) return null;

        string cleanName = typeOrName.Trim();
        if (cleanName.StartsWith($"{Name}.", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName[(Name.Length + 1)..];
        }

        return UserControls.FirstOrDefault(u =>
            string.Equals(u.Name, cleanName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(u.RelativePath), cleanName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(u.RelativePath), cleanName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"{Name}.{u.Name}", typeOrName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Obtiene y parsea el modelo de diseño (FormDesignModel) de un UserControl del proyecto.
    /// </summary>
    public FormDesignModel? GetUserControlDesignModel(string typeOrName)
    {
        var item = FindUserControl(typeOrName);
        if (item == null) return null;

        string key = item.AbsolutePath ?? item.Name;
        if (_cachedUserControlModels.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!string.IsNullOrEmpty(item.AbsolutePath) && File.Exists(item.AbsolutePath))
        {
            try
            {
                var doc = SafeFileStorage.LoadDocument(item.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(doc.Header))
                {
                    var model = FormDesignParser.Parse(doc.Header);
                    _cachedUserControlModels[key] = model;
                    return model;
                }
            }
            catch
            {
                // Fallback silencioso si no se puede leer
            }
        }

        return null;
    }

    /// <summary>
    /// Invalida la caché del modelo visual de un UserControl (por ejemplo cuando se guardan cambios en el editor).
    /// </summary>
    public void InvalidateUserControlCache(string? keyOrPath = null)
    {
        if (string.IsNullOrEmpty(keyOrPath))
        {
            _cachedUserControlModels.Clear();
        }
        else
        {
            _cachedUserControlModels.Remove(keyOrPath);
        }
    }
}

