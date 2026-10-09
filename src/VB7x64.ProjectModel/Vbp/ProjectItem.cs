using System;
using System.IO;

namespace VB7x64.ProjectModel.Vbp;

public class ProjectItem
{
    public int LineNumber { get; set; }
    public string RawLine { get; set; } = string.Empty;
    public ProjectItemType Type { get; set; } = ProjectItemType.Unknown;
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string AbsolutePath { get; set; } = string.Empty;
    public bool FileExists { get; set; } = true;

    public bool IsEditable => Type switch
    {
        ProjectItemType.Form => true,
        ProjectItemType.Module => true,
        ProjectItemType.Class => true,
        ProjectItemType.UserControl => true,
        ProjectItemType.PropertyPage => true,
        _ => false
    };

    public string DisplayIcon => Type switch
    {
        ProjectItemType.Form => "🗔",
        ProjectItemType.Module => "📜",
        ProjectItemType.Class => "🧩",
        ProjectItemType.UserControl => "🎛️",
        ProjectItemType.PropertyPage => "📑",
        ProjectItemType.Reference => "🔗",
        ProjectItemType.ActiveXControl => "📦",
        _ => "📄"
    };

    public string DisplayTitle => string.IsNullOrWhiteSpace(Name)
        ? (Path.GetFileName(RelativePath) is { Length: > 0 } fn ? fn : RawLine)
        : $"{Name} ({Path.GetFileName(RelativePath)})";
}
