using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ViBa7.ProjectModel.Vbp;

public class VB6ProjectGroup
{
    public string GroupFilePath { get; set; } = string.Empty;
    public string GroupDirectory => Path.GetDirectoryName(GroupFilePath) ?? string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? StartupProjectPath { get; set; }
    public List<VB6Project> Projects { get; } = [];

    public VB6Project? StartupProject => Projects.FirstOrDefault(p =>
        string.Equals(p.ProjectFilePath, StartupProjectPath, StringComparison.OrdinalIgnoreCase)) ?? Projects.FirstOrDefault();
}
