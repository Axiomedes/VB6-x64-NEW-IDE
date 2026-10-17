using System;

namespace ViBa7.ProjectModel.Designers;

public class FormControlItem
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty; // ej: VB.CommandButton, VB.TextBox
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public bool HasExplicitTabIndex { get; set; }

    public string DisplayType => TypeName.StartsWith("VB.", StringComparison.OrdinalIgnoreCase) 
        ? TypeName.Substring(3) 
        : TypeName;

    public string DisplayCoordinates => $"Top: {Top}, Left: {Left}";
}
