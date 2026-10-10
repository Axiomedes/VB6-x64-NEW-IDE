using System;
using System.Collections.Generic;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Representa una propiedad individual o bloque BeginProperty en la cabecera visual de un formulario VB6.
/// </summary>
public class FormPropertyItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? TrailingComment { get; set; }
    public bool IsBeginProperty { get; set; }
    public List<string> RawPropertyLines { get; set; } = new();

    public FormPropertyItem() { }

    public FormPropertyItem(string name, string value, string? trailingComment = null)
    {
        Name = name;
        Value = value;
        TrailingComment = trailingComment;
    }

    public FormPropertyItem Clone()
    {
        return new FormPropertyItem
        {
            Name = Name,
            Value = Value,
            TrailingComment = TrailingComment,
            IsBeginProperty = IsBeginProperty,
            RawPropertyLines = new List<string>(RawPropertyLines)
        };
    }
}
