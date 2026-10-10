using System;
using System.Collections.Generic;
using System.Linq;

namespace VB7x64.ProjectModel.Designers;

public enum DesignerPropertyType
{
    Text,
    IntegerTwips,
    IntegerPixels,
    IntegerRaw,
    Boolean,
    ColorHex,
    EnumChoice,
    FontDescription,
    ReadOnlyText
}

public record DesignerPropertyChoice(string DisplayText, string RawValue);

/// <summary>
/// Representa un elemento de propiedad individual listo para ser inspeccionado y editado en la Ventana de Propiedades.
/// </summary>
public class DesignerPropertyItem
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public string Description { get; init; } = string.Empty;
    public DesignerPropertyType Type { get; init; } = DesignerPropertyType.Text;
    public string CurrentValue { get; set; } = string.Empty;
    public List<DesignerPropertyChoice> Choices { get; init; } = new();
    public bool IsReadOnly { get; init; }
    public Func<string, bool>? ValueSetter { get; init; }

    public bool SetValue(string newValue)
    {
        if (IsReadOnly || ValueSetter == null) return false;
        if (ValueSetter(newValue))
        {
            CurrentValue = newValue;
            return true;
        }
        return false;
    }
}

/// <summary>
/// Fábrica que genera descriptores de propiedades para formularios y controles VB6.
/// </summary>
public static class DesignerPropertyFactory
{
    private static readonly List<DesignerPropertyChoice> BoolChoices = new()
    {
        new DesignerPropertyChoice("True", "True"),
        new DesignerPropertyChoice("False", "False")
    };

    private static readonly List<DesignerPropertyChoice> FormBorderChoices = new()
    {
        new DesignerPropertyChoice("0 - None", "0"),
        new DesignerPropertyChoice("1 - Fixed Single", "1"),
        new DesignerPropertyChoice("2 - Sizable", "2"),
        new DesignerPropertyChoice("3 - Fixed Dialog", "3"),
        new DesignerPropertyChoice("4 - Fixed ToolWindow", "4"),
        new DesignerPropertyChoice("5 - Sizable ToolWindow", "5")
    };

    private static readonly List<DesignerPropertyChoice> StartUpChoices = new()
    {
        new DesignerPropertyChoice("0 - Manual", "0"),
        new DesignerPropertyChoice("1 - CenterOwner", "1"),
        new DesignerPropertyChoice("2 - CenterScreen", "2"),
        new DesignerPropertyChoice("3 - Windows Default", "3")
    };

    private static readonly List<DesignerPropertyChoice> AlignmentChoices = new()
    {
        new DesignerPropertyChoice("0 - Left Justify", "0"),
        new DesignerPropertyChoice("1 - Right Justify", "1"),
        new DesignerPropertyChoice("2 - Center", "2")
    };

    public static List<DesignerPropertyItem> GetPropertiesForForm(FormDesignModel form, Action onModelChanged)
    {
        var list = new List<DesignerPropertyItem>
        {
            new DesignerPropertyItem
            {
                Name = "Name",
                DisplayName = "(Name)",
                Category = "General",
                Description = "Devuelve o establece el nombre utilizado en el código para identificar el formulario.",
                Type = DesignerPropertyType.Text,
                CurrentValue = form.Name,
                ValueSetter = val =>
                {
                    val = val.Trim();
                    if (string.IsNullOrWhiteSpace(val)) return false;
                    form.Name = val;
                    onModelChanged();
                    return true;
                }
            },
            new DesignerPropertyItem
            {
                Name = "Caption",
                DisplayName = "Caption",
                Category = "Appearance",
                Description = "Devuelve o establece el texto que se muestra en la barra de título del formulario.",
                Type = DesignerPropertyType.Text,
                CurrentValue = form.Caption,
                ValueSetter = val =>
                {
                    form.Caption = val;
                    onModelChanged();
                    return true;
                }
            },
            new DesignerPropertyItem
            {
                Name = "BackColor",
                DisplayName = "BackColor",
                Category = "Appearance",
                Description = "Devuelve o establece el color de fondo del formulario.",
                Type = DesignerPropertyType.ColorHex,
                CurrentValue = form.BackColorHex ?? "&H8000000F&",
                ValueSetter = val =>
                {
                    form.BackColorHex = val;
                    onModelChanged();
                    return true;
                }
            },
            new DesignerPropertyItem
            {
                Name = "ForeColor",
                DisplayName = "ForeColor",
                Category = "Appearance",
                Description = "Devuelve o establece el color de primer plano para texto y gráficos en el formulario.",
                Type = DesignerPropertyType.ColorHex,
                CurrentValue = form.ForeColorHex ?? "&H80000012&",
                ValueSetter = val =>
                {
                    form.ForeColorHex = val;
                    onModelChanged();
                    return true;
                }
            },
            new DesignerPropertyItem
            {
                Name = "BorderStyle",
                DisplayName = "BorderStyle",
                Category = "Appearance",
                Description = "Devuelve o establece el estilo del borde del formulario (redimensionable, fijo, diálogo, etc.).",
                Type = DesignerPropertyType.EnumChoice,
                CurrentValue = form.BorderStyle?.ToString() ?? "2",
                Choices = FormBorderChoices,
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int b))
                    {
                        form.BorderStyle = b;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            },
            new DesignerPropertyItem
            {
                Name = "StartUpPosition",
                DisplayName = "StartUpPosition",
                Category = "Position",
                Description = "Devuelve o establece la posición en pantalla en la que aparecerá el formulario al cargarse.",
                Type = DesignerPropertyType.EnumChoice,
                CurrentValue = form.StartUpPosition?.ToString() ?? "3",
                Choices = StartUpChoices,
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int sp))
                    {
                        form.StartUpPosition = sp;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            },
            new DesignerPropertyItem
            {
                Name = "ClientWidth",
                DisplayName = "ClientWidth",
                Category = "Position",
                Description = "Devuelve o establece el ancho del área cliente del formulario en Twips.",
                Type = DesignerPropertyType.IntegerTwips,
                CurrentValue = form.ClientWidth.ToString(),
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int w) && w > 0)
                    {
                        form.ClientWidth = w;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            },
            new DesignerPropertyItem
            {
                Name = "ClientHeight",
                DisplayName = "ClientHeight",
                Category = "Position",
                Description = "Devuelve o establece el alto del área cliente del formulario en Twips.",
                Type = DesignerPropertyType.IntegerTwips,
                CurrentValue = form.ClientHeight.ToString(),
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int h) && h > 0)
                    {
                        form.ClientHeight = h;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            },
            new DesignerPropertyItem
            {
                Name = "Font",
                DisplayName = "Font",
                Category = "Font",
                Description = "Devuelve o establece la tipografía predeterminada para el formulario.",
                Type = DesignerPropertyType.FontDescription,
                CurrentValue = form.Font?.Name != null ? $"{form.Font.Name}, {form.Font.Size}pt" : "MS Sans Serif, 8pt",
                IsReadOnly = true
            }
        };

        return list;
    }

    public static List<DesignerPropertyItem> GetPropertiesForControl(FormDesignerControl ctrl, FormDesignModel form, Action onModelChanged)
    {
        var list = new List<DesignerPropertyItem>
        {
            new DesignerPropertyItem
            {
                Name = "Name",
                DisplayName = "(Name)",
                Category = "General",
                Description = "Devuelve o establece el nombre utilizado en el código para identificar el control.",
                Type = DesignerPropertyType.Text,
                CurrentValue = ctrl.Name,
                ValueSetter = val =>
                {
                    val = val.Trim();
                    if (string.IsNullOrWhiteSpace(val)) return false;
                    // Evitar nombres repetidos
                    var existing = form.FindControl(val);
                    if (existing != null && existing != ctrl) return false;
                    ctrl.Name = val;
                    onModelChanged();
                    return true;
                }
            },
            new DesignerPropertyItem
            {
                Name = "Index",
                DisplayName = "Index",
                Category = "General",
                Description = "Devuelve o establece el número que identifica a un control en una matriz de controles.",
                Type = DesignerPropertyType.IntegerRaw,
                CurrentValue = ctrl.Index?.ToString() ?? "",
                ValueSetter = val =>
                {
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        ctrl.Index = null;
                        onModelChanged();
                        return true;
                    }
                    if (int.TryParse(val, out int idx) && idx >= 0)
                    {
                        ctrl.Index = idx;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            }
        };

        // Caption o Text según el tipo de control
        var toolboxInfo = ControlToolboxRegistry.Find(ctrl.TypeName);
        if (toolboxInfo?.HasCaption == true || !string.IsNullOrEmpty(ctrl.Caption) ||
            ctrl.TypeName.EndsWith("Button", StringComparison.OrdinalIgnoreCase) ||
            ctrl.TypeName.EndsWith("Label", StringComparison.OrdinalIgnoreCase) ||
            ctrl.TypeName.EndsWith("Frame", StringComparison.OrdinalIgnoreCase) ||
            ctrl.TypeName.EndsWith("CheckBox", StringComparison.OrdinalIgnoreCase) ||
            ctrl.TypeName.EndsWith("OptionButton", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "Caption",
                DisplayName = "Caption",
                Category = "Appearance",
                Description = "Devuelve o establece el texto que se muestra en el control.",
                Type = DesignerPropertyType.Text,
                CurrentValue = ctrl.Caption,
                ValueSetter = val =>
                {
                    ctrl.Caption = val;
                    onModelChanged();
                    return true;
                }
            });
        }

        if (toolboxInfo?.HasText == true || !string.IsNullOrEmpty(ctrl.Text) ||
            ctrl.TypeName.EndsWith("TextBox", StringComparison.OrdinalIgnoreCase) ||
            ctrl.TypeName.EndsWith("ComboBox", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "Text",
                DisplayName = "Text",
                Category = "Appearance",
                Description = "Devuelve o establece el texto contenido en el control.",
                Type = DesignerPropertyType.Text,
                CurrentValue = ctrl.Text,
                ValueSetter = val =>
                {
                    ctrl.Text = val;
                    onModelChanged();
                    return true;
                }
            });
        }

        // Posicionamiento y dimensiones en Twips
        list.Add(new DesignerPropertyItem
        {
            Name = "Left",
            DisplayName = "Left",
            Category = "Position",
            Description = "Devuelve o establece la distancia entre el borde izquierdo del control y el contenedor en Twips.",
            Type = DesignerPropertyType.IntegerTwips,
            CurrentValue = ctrl.Left.ToString(),
            ValueSetter = val =>
            {
                if (int.TryParse(val, out int v))
                {
                    ctrl.Left = v;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "Top",
            DisplayName = "Top",
            Category = "Position",
            Description = "Devuelve o establece la distancia entre el borde superior del control y el contenedor en Twips.",
            Type = DesignerPropertyType.IntegerTwips,
            CurrentValue = ctrl.Top.ToString(),
            ValueSetter = val =>
            {
                if (int.TryParse(val, out int v))
                {
                    ctrl.Top = v;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "Width",
            DisplayName = "Width",
            Category = "Position",
            Description = "Devuelve o establece el ancho del control en Twips.",
            Type = DesignerPropertyType.IntegerTwips,
            CurrentValue = ctrl.Width.ToString(),
            ValueSetter = val =>
            {
                if (int.TryParse(val, out int v) && v >= 0)
                {
                    ctrl.Width = v;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "Height",
            DisplayName = "Height",
            Category = "Position",
            Description = "Devuelve o establece la altura del control en Twips.",
            Type = DesignerPropertyType.IntegerTwips,
            CurrentValue = ctrl.Height.ToString(),
            ValueSetter = val =>
            {
                if (int.TryParse(val, out int v) && v >= 0)
                {
                    ctrl.Height = v;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        // Comportamiento
        list.Add(new DesignerPropertyItem
        {
            Name = "Enabled",
            DisplayName = "Enabled",
            Category = "Behavior",
            Description = "Determina si el control puede responder a eventos generados por el usuario.",
            Type = DesignerPropertyType.Boolean,
            CurrentValue = ctrl.Enabled ? "True" : "False",
            Choices = BoolChoices,
            ValueSetter = val =>
            {
                if (bool.TryParse(val, out bool b))
                {
                    ctrl.Enabled = b;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "Visible",
            DisplayName = "Visible",
            Category = "Behavior",
            Description = "Determina si el control está visible u oculto.",
            Type = DesignerPropertyType.Boolean,
            CurrentValue = ctrl.Visible ? "True" : "False",
            Choices = BoolChoices,
            ValueSetter = val =>
            {
                if (bool.TryParse(val, out bool b))
                {
                    ctrl.Visible = b;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "TabIndex",
            DisplayName = "TabIndex",
            Category = "Behavior",
            Description = "Devuelve o establece el orden de tabulación del control dentro de su formulario principal.",
            Type = DesignerPropertyType.IntegerRaw,
            CurrentValue = ctrl.TabIndex?.ToString() ?? "",
            ValueSetter = val =>
            {
                if (string.IsNullOrWhiteSpace(val))
                {
                    ctrl.TabIndex = null;
                    onModelChanged();
                    return true;
                }
                if (int.TryParse(val, out int t) && t >= 0)
                {
                    ctrl.TabIndex = t;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "TabStop",
            DisplayName = "TabStop",
            Category = "Behavior",
            Description = "Determina si el usuario puede utilizar la tecla TAB para pasar el foco al control.",
            Type = DesignerPropertyType.Boolean,
            CurrentValue = (ctrl.TabStop ?? true) ? "True" : "False",
            Choices = BoolChoices,
            ValueSetter = val =>
            {
                if (bool.TryParse(val, out bool b))
                {
                    ctrl.TabStop = b;
                    onModelChanged();
                    return true;
                }
                return false;
            }
        });

        // Colores
        list.Add(new DesignerPropertyItem
        {
            Name = "BackColor",
            DisplayName = "BackColor",
            Category = "Appearance",
            Description = "Devuelve o establece el color de fondo utilizado para mostrar texto y gráficos en el control.",
            Type = DesignerPropertyType.ColorHex,
            CurrentValue = ctrl.BackColorHex ?? "&H8000000F&",
            ValueSetter = val =>
            {
                ctrl.BackColorHex = val;
                onModelChanged();
                return true;
            }
        });

        list.Add(new DesignerPropertyItem
        {
            Name = "ForeColor",
            DisplayName = "ForeColor",
            Category = "Appearance",
            Description = "Devuelve o establece el color de primer plano utilizado para mostrar texto y gráficos en el control.",
            Type = DesignerPropertyType.ColorHex,
            CurrentValue = ctrl.ForeColorHex ?? "&H80000012&",
            ValueSetter = val =>
            {
                ctrl.ForeColorHex = val;
                onModelChanged();
                return true;
            }
        });

        // Alineación (si aplica)
        if (ctrl.TypeName.EndsWith("Label", StringComparison.OrdinalIgnoreCase) ||
            ctrl.TypeName.EndsWith("TextBox", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "Alignment",
                DisplayName = "Alignment",
                Category = "Appearance",
                Description = "Determina la alineación del texto dentro del control (Izquierda, Derecha o Centro).",
                Type = DesignerPropertyType.EnumChoice,
                CurrentValue = ctrl.Alignment?.ToString() ?? "0",
                Choices = AlignmentChoices,
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int a))
                    {
                        ctrl.Alignment = a;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            });
        }

        // MultiLine para TextBox
        if (ctrl.TypeName.EndsWith("TextBox", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "MultiLine",
                DisplayName = "MultiLine",
                Category = "Behavior",
                Description = "Indica si un control TextBox puede aceptar y mostrar múltiples líneas de texto.",
                Type = DesignerPropertyType.Boolean,
                CurrentValue = (ctrl.MultiLine ?? false) ? "True" : "False",
                Choices = BoolChoices,
                ValueSetter = val =>
                {
                    if (bool.TryParse(val, out bool b))
                    {
                        ctrl.MultiLine = b;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            });
        }

        return list;
    }
}
