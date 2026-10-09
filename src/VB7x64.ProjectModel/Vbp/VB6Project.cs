using System;
using System.Collections.Generic;
using System.IO;

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

    public IEnumerable<ProjectItem> Forms => Items.FindAll(i => i.Type == ProjectItemType.Form);
    public IEnumerable<ProjectItem> Modules => Items.FindAll(i => i.Type == ProjectItemType.Module);
    public IEnumerable<ProjectItem> Classes => Items.FindAll(i => i.Type == ProjectItemType.Class);
    public IEnumerable<ProjectItem> UserControls => Items.FindAll(i => i.Type == ProjectItemType.UserControl);
    public IEnumerable<ProjectItem> PropertyPages => Items.FindAll(i => i.Type == ProjectItemType.PropertyPage);
}
