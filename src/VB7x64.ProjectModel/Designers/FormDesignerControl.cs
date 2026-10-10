using System;
using System.Collections.Generic;
using System.Linq;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Representa un control individual o contenedor dentro del formulario VB6 en modo diseño.
/// </summary>
public class FormDesignerControl
{
    public const double TwipsPerPixel = 15.0;

    public string TypeName { get; set; } = "VB.CommandButton";
    public string Name { get; set; } = "Command1";
    public int? Index { get; set; }

    // Geometría en Twips (unidades nativas de VB6)
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; } = 1500;
    public int Height { get; set; } = 495;

    // Propiedades comunes
    public string Caption { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int? TabIndex { get; set; }
    public bool? TabStop { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;

    // Apariencia
    public string? BackColorHex { get; set; }
    public string? ForeColorHex { get; set; }
    public int? BorderStyle { get; set; }
    public int? Alignment { get; set; }
    public bool? MultiLine { get; set; }
    public int? ScrollBars { get; set; }
    public FormFontDescription? Font { get; set; }

    // Jerarquía de contenedores (para Frame, PictureBox, etc.)
    public FormDesignerControl? ParentControl { get; set; }
    public List<FormDesignerControl> Children { get; set; } = new();

    // Colección de todas las propiedades originales y no mapeadas (para round-trip sin pérdidas)
    public List<FormPropertyItem> CustomProperties { get; set; } = new();

    #region Conversión Twips ↔ Píxeles
    public double LeftPixels
    {
        get => Left / TwipsPerPixel;
        set => Left = (int)Math.Round(value * TwipsPerPixel);
    }

    public double TopPixels
    {
        get => Top / TwipsPerPixel;
        set => Top = (int)Math.Round(value * TwipsPerPixel);
    }

    public double WidthPixels
    {
        get => Math.Max(1, Width / TwipsPerPixel);
        set => Width = Math.Max(15, (int)Math.Round(value * TwipsPerPixel));
    }

    public double HeightPixels
    {
        get => Math.Max(1, Height / TwipsPerPixel);
        set => Height = Math.Max(15, (int)Math.Round(value * TwipsPerPixel));
    }

    public void SetBoundsPixels(double left, double top, double width, double height)
    {
        LeftPixels = left;
        TopPixels = top;
        WidthPixels = width;
        HeightPixels = height;
    }

    public void SetPositionPixels(double left, double top)
    {
        LeftPixels = left;
        TopPixels = top;
    }

    public void SetSizePixels(double width, double height)
    {
        WidthPixels = width;
        HeightPixels = height;
    }
    #endregion

    #region Propiedades de Diagnóstico y UI
    public string DisplayType => TypeName.StartsWith("VB.", StringComparison.OrdinalIgnoreCase)
        ? TypeName.Substring(3)
        : TypeName;

    public string DisplayNameWithIndex => Index.HasValue ? $"{Name}({Index.Value})" : Name;

    public bool IsContainer =>
        TypeName.Equals("VB.Frame", StringComparison.OrdinalIgnoreCase) ||
        TypeName.Equals("VB.PictureBox", StringComparison.OrdinalIgnoreCase);

    public bool IsMenu =>
        TypeName.Equals("VB.Menu", StringComparison.OrdinalIgnoreCase);

    public bool IsTimer =>
        TypeName.Equals("VB.Timer", StringComparison.OrdinalIgnoreCase);
    #endregion

    /// <summary>
    /// Clona el control y su jerarquía de hijos.
    /// </summary>
    public FormDesignerControl Clone(FormDesignerControl? newParent = null)
    {
        var clone = new FormDesignerControl
        {
            TypeName = TypeName,
            Name = Name,
            Index = Index,
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height,
            Caption = Caption,
            Text = Text,
            TabIndex = TabIndex,
            TabStop = TabStop,
            Enabled = Enabled,
            Visible = Visible,
            BackColorHex = BackColorHex,
            ForeColorHex = ForeColorHex,
            BorderStyle = BorderStyle,
            Alignment = Alignment,
            MultiLine = MultiLine,
            ScrollBars = ScrollBars,
            Font = Font?.Clone(),
            ParentControl = newParent,
            CustomProperties = CustomProperties.Select(p => p.Clone()).ToList()
        };

        foreach (var child in Children)
        {
            clone.Children.Add(child.Clone(clone));
        }

        return clone;
    }
}
