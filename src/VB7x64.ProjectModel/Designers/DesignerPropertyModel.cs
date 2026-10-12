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
    ReadOnlyText,
    Picture
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

    private static readonly List<DesignerPropertyChoice> PictureAlignmentChoices = new()
    {
        new DesignerPropertyChoice("0 - Superior (Top)", "0"),
        new DesignerPropertyChoice("1 - Inferior (Bottom)", "1"),
        new DesignerPropertyChoice("2 - Izquierda (Left)", "2"),
        new DesignerPropertyChoice("3 - Derecha (Right)", "3"),
        new DesignerPropertyChoice("4 - Centro (Center)", "4")
    };

    private static readonly List<DesignerPropertyChoice> ButtonStyleChoices = new()
    {
        new DesignerPropertyChoice("0 - Standard", "0"),
        new DesignerPropertyChoice("1 - Graphical", "1")
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
                Name = "Picture",
                DisplayName = "Picture",
                Category = "Appearance",
                Description = "Devuelve o establece el gráfico que se va a mostrar como fondo del formulario.",
                Type = DesignerPropertyType.Picture,
                CurrentValue = Resources.FrxResourceFile.GetFormatDescription(form.PictureData, form.Picture),
                ValueSetter = val =>
                {
                    if (string.IsNullOrWhiteSpace(val) || val.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                    {
                        form.Picture = null;
                        form.PictureData = null;
                        onModelChanged();
                        return true;
                    }
                    if (val.StartsWith('(') && val.EndsWith(')'))
                    {
                        return true;
                    }
                    form.Picture = val;
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

    public static List<DesignerPropertyItem> GetPropertiesForControl(
        FormDesignerControl ctrl,
        FormDesignModel form,
        Action onModelChanged,
        Action<FormDesignerControl, string, string>? onControlRenamed = null,
        Func<string, bool>? confirmCreateControlArray = null,
        Action<string>? showWarning = null)
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
                    if (string.Equals(ctrl.Name, val, StringComparison.OrdinalIgnoreCase))
                    {
                        ctrl.Name = val;
                        onModelChanged();
                        return true;
                    }

                    // Buscar controles existentes con ese mismo nombre
                    var existingControls = form.GetAllControls()
                        .Where(c => c != ctrl && string.Equals(c.Name, val, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (existingControls.Count > 0)
                    {
                        if (!ctrl.Index.HasValue)
                        {
                            bool shouldCreate = confirmCreateControlArray?.Invoke(val) ?? false;
                            if (!shouldCreate)
                            {
                                return false;
                            }
                        }

                        var firstExisting = existingControls[0];
                        if (!firstExisting.Index.HasValue)
                        {
                            firstExisting.Index = 0;
                        }

                        var usedIndices = existingControls.Where(c => c.Index.HasValue).Select(c => c.Index!.Value).ToHashSet();
                        if (firstExisting.Index.HasValue) usedIndices.Add(firstExisting.Index.Value);

                        int nextIdx = 0;
                        while (usedIndices.Contains(nextIdx)) nextIdx++;

                        string oldName = ctrl.Name;
                        ctrl.Name = val;
                        if (!ctrl.Index.HasValue || usedIndices.Contains(ctrl.Index.Value))
                        {
                            ctrl.Index = nextIdx;
                        }
                        onControlRenamed?.Invoke(ctrl, oldName, val);
                        onModelChanged();
                        return true;
                    }

                    string prevName = ctrl.Name;
                    ctrl.Name = val;
                    onControlRenamed?.Invoke(ctrl, prevName, val);
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
                    val = val?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        var siblings = form.GetAllControls()
                            .Where(c => c != ctrl && string.Equals(c.Name, ctrl.Name, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        if (siblings.Count > 0)
                        {
                            showWarning?.Invoke($"El control '{ctrl.Name}' pertenece a una matriz de controles y debe tener un índice válido.");
                            return false;
                        }

                        ctrl.Index = null;
                        onModelChanged();
                        return true;
                    }

                    if (int.TryParse(val, out int idx) && idx >= 0)
                    {
                        var duplicate = form.GetAllControls()
                            .FirstOrDefault(c => c != ctrl &&
                                                 string.Equals(c.Name, ctrl.Name, StringComparison.OrdinalIgnoreCase) &&
                                                 c.Index == idx);

                        if (duplicate != null)
                        {
                            showWarning?.Invoke($"Ya existe un control '{ctrl.Name}' con el índice {idx} en la matriz.");
                            return false;
                        }

                        ctrl.Index = idx;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            }
        };

        // Si es un Timer, mostrar exclusivamente las propiedades soportadas por VB6 para temporizadores no visibles
        if (ctrl.IsTimer)
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "Interval",
                DisplayName = "Interval",
                Category = "Behavior",
                Description = "Devuelve o establece el número de milisegundos entre llamadas a un evento Timer (Tik). 0 desactiva el temporizador.",
                Type = DesignerPropertyType.IntegerRaw,
                CurrentValue = ctrl.Interval.ToString(),
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int iv) && iv >= 0 && iv <= 65535)
                    {
                        ctrl.Interval = iv;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            });

            list.Add(new DesignerPropertyItem
            {
                Name = "Enabled",
                DisplayName = "Enabled",
                Category = "Behavior",
                Description = "Determina si el temporizador se ejecuta y produce eventos Timer periódicamente.",
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
                Name = "Left",
                DisplayName = "Left",
                Category = "Position",
                Description = "Posición horizontal del icono del temporizador en diseño en Twips.",
                Type = DesignerPropertyType.IntegerTwips,
                CurrentValue = ctrl.Left.ToString(),
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int v)) { ctrl.Left = v; onModelChanged(); return true; }
                    return false;
                }
            });

            list.Add(new DesignerPropertyItem
            {
                Name = "Top",
                DisplayName = "Top",
                Category = "Position",
                Description = "Posición vertical del icono del temporizador en diseño en Twips.",
                Type = DesignerPropertyType.IntegerTwips,
                CurrentValue = ctrl.Top.ToString(),
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int v)) { ctrl.Top = v; onModelChanged(); return true; }
                    return false;
                }
            });

            list.Add(new DesignerPropertyItem
            {
                Name = "Tag",
                DisplayName = "Tag",
                Category = "General",
                Description = "Almacena cualquier dato adicional necesario para su programa.",
                Type = DesignerPropertyType.Text,
                CurrentValue = ctrl.CustomProperties.FirstOrDefault(p => p.Name.Equals("Tag", StringComparison.OrdinalIgnoreCase))?.Value ?? "",
                ValueSetter = val =>
                {
                    var p = ctrl.CustomProperties.FirstOrDefault(cp => cp.Name.Equals("Tag", StringComparison.OrdinalIgnoreCase));
                    if (p != null) p.Value = val;
                    else ctrl.CustomProperties.Add(new FormPropertyItem { Name = "Tag", Value = val });
                    onModelChanged();
                    return true;
                }
            });

            return list;
        }

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

        // Soporte de imágenes para PictureBox, Image, CommandButton, etc.
        bool isPictureBox = ctrl.TypeName.EndsWith("PictureBox", StringComparison.OrdinalIgnoreCase);
        bool isImageControl = ctrl.TypeName.EndsWith("Image", StringComparison.OrdinalIgnoreCase);
        bool isButton = ctrl.TypeName.EndsWith("Button", StringComparison.OrdinalIgnoreCase);

        if (isPictureBox || isImageControl || isButton || !string.IsNullOrEmpty(ctrl.Picture) || ctrl.PictureData != null)
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "Picture",
                DisplayName = "Picture",
                Category = "Appearance",
                Description = "Devuelve o establece el gráfico que se va a mostrar en el control.",
                Type = DesignerPropertyType.Picture,
                CurrentValue = Resources.FrxResourceFile.GetFormatDescription(ctrl.PictureData, ctrl.Picture),
                ValueSetter = val =>
                {
                    if (string.IsNullOrWhiteSpace(val) || val.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                    {
                        ctrl.Picture = null;
                        ctrl.PictureData = null;
                        onModelChanged();
                        return true;
                    }
                    if (val.StartsWith('(') && val.EndsWith(')'))
                    {
                        return true;
                    }
                    ctrl.Picture = val;
                    onModelChanged();
                    return true;
                }
            });
        }

        if (isPictureBox)
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "AutoSize",
                DisplayName = "AutoSize",
                Category = "Behavior",
                Description = "Determina si el control PictureBox se redimensiona automáticamente para ajustarse al tamaño de la imagen cargada.",
                Type = DesignerPropertyType.Boolean,
                CurrentValue = ctrl.AutoSize ? "True" : "False",
                Choices = BoolChoices,
                ValueSetter = val =>
                {
                    if (bool.TryParse(val, out bool b))
                    {
                        ctrl.AutoSize = b;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            });
        }

        if (isButton)
        {
            list.Add(new DesignerPropertyItem
            {
                Name = "Style",
                DisplayName = "Style",
                Category = "Appearance",
                Description = "Devuelve o establece si el botón se muestra en estilo estándar (0) o gráfico (1) para soportar iconos e imágenes.",
                Type = DesignerPropertyType.EnumChoice,
                CurrentValue = ctrl.Style.ToString(),
                Choices = ButtonStyleChoices,
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int s))
                    {
                        ctrl.Style = s;
                        onModelChanged();
                        return true;
                    }
                    return false;
                }
            });

            list.Add(new DesignerPropertyItem
            {
                Name = "PictureAlignment",
                DisplayName = "PictureAlignment",
                Category = "Appearance",
                Description = "Determina la alineación de la imagen en relación al texto del botón (0-Superior, 1-Inferior, 2-Izquierda, 3-Derecha, 4-Centro).",
                Type = DesignerPropertyType.EnumChoice,
                CurrentValue = ((int)ctrl.PictureAlignment).ToString(),
                Choices = PictureAlignmentChoices,
                ValueSetter = val =>
                {
                    if (int.TryParse(val, out int pa))
                    {
                        ctrl.PictureAlignment = (PictureAlignment)Math.Clamp(pa, 0, 4);
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
