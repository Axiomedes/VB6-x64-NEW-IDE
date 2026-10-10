using System;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Representa la descripción de fuente serializada en bloques BeginProperty Font ... EndProperty de VB6.
/// </summary>
public class FormFontDescription
{
    public string Name { get; set; } = "MS Sans Serif";
    public double Size { get; set; } = 8.25;
    public int Charset { get; set; } = 0;
    public int Weight { get; set; } = 400; // 400 = Regular, 700 = Bold
    public bool Underline { get; set; }
    public bool Italic { get; set; }
    public bool Strikethrough { get; set; }

    public bool IsBold
    {
        get => Weight >= 700;
        set => Weight = value ? 700 : 400;
    }

    public FormFontDescription Clone()
    {
        return new FormFontDescription
        {
            Name = Name,
            Size = Size,
            Charset = Charset,
            Weight = Weight,
            Underline = Underline,
            Italic = Italic,
            Strikethrough = Strikethrough
        };
    }
}
