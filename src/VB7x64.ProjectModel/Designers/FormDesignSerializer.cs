using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Serializador de modelos de diseño de formularios VB6 (FormDesignModel) a sintaxis .frm clásica.
/// Preserva sangrías estándar de VB6, alineación de propiedades y atributos de módulo.
/// </summary>
public static class FormDesignSerializer
{
    private const string PropertyPad = "                "; // 16 espacios

    /// <summary>
    /// Serializa el modelo de diseño a texto completo de cabecera visual de formulario.
    /// </summary>
    public static string Serialize(FormDesignModel model)
    {
        if (model == null) return string.Empty;

        var sb = new StringBuilder();

        // 1. Cabecera de versión
        if (!string.IsNullOrWhiteSpace(model.VersionHeader))
        {
            sb.AppendLine(model.VersionHeader);
        }
        else
        {
            sb.AppendLine("VERSION 5.00");
        }

        // 2. Inicio del formulario raíz
        sb.AppendLine($"Begin {model.RootType} {model.Name} ");

        // 3. Propiedades del formulario (sangría 3 espacios)
        SerializeFormProperties(sb, model, indentLevel: 1);

        // 4. Controles hijos de primer nivel
        foreach (var child in model.Children)
        {
            SerializeControl(sb, child, indentLevel: 1);
        }

        // 5. Cierre del formulario raíz
        sb.AppendLine("End");

        // 6. Atributos de módulo posteriores (Attribute VB_*)
        if (model.Attributes.Count > 0)
        {
            foreach (var attr in model.Attributes)
            {
                // Si el nombre del formulario cambió, sincronizar Attribute VB_Name
                if (attr.StartsWith("Attribute VB_Name =", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Attribute VB_Name = \"{model.Name}\"");
                }
                else
                {
                    sb.AppendLine(attr);
                }
            }
        }
        else
        {
            // Atributos por defecto si no estaban presentes
            sb.AppendLine($"Attribute VB_Name = \"{model.Name}\"");
            sb.AppendLine("Attribute VB_GlobalNameSpace = False");
            sb.AppendLine("Attribute VB_Creatable = False");
            sb.AppendLine("Attribute VB_PredeclaredId = True");
            sb.AppendLine("Attribute VB_Exposed = False");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serializa un único control a sintaxis VB6.
    /// </summary>
    public static string SerializeControl(FormDesignerControl ctrl, int indentLevel = 0)
    {
        if (ctrl == null) return string.Empty;
        var sb = new StringBuilder();
        SerializeControl(sb, ctrl, indentLevel);
        return sb.ToString();
    }

    /// <summary>
    /// Serializa una lista de controles (por ejemplo para el portapapeles).
    /// </summary>
    public static string SerializeControls(IEnumerable<FormDesignerControl> controls)
    {
        if (controls == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ctrl in controls)
        {
            SerializeControl(sb, ctrl, 0);
        }
        return sb.ToString();
    }

    private static void SerializeFormProperties(StringBuilder sb, FormDesignModel model, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 3);

        // Mapeo de propiedades prioritarias en orden estándar de VB6
        var writtenProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void WriteProp(string name, string value, string? comment = null)
        {
            writtenProps.Add(name);
            string formattedComment = string.IsNullOrEmpty(comment) ? string.Empty : $"  {comment}";
            string propPadded = name.Length < 16 ? name.PadRight(16) : name + " ";
            sb.AppendLine($"{indent}{propPadded}=   {value}{formattedComment}");
        }

        if (!string.IsNullOrEmpty(model.Caption)) WriteProp("Caption", $"\"{model.Caption}\"");
        WriteProp("ClientHeight", model.ClientHeight.ToString(CultureInfo.InvariantCulture));
        WriteProp("ClientLeft", model.ClientLeft.ToString(CultureInfo.InvariantCulture));
        WriteProp("ClientTop", model.ClientTop.ToString(CultureInfo.InvariantCulture));
        WriteProp("ClientWidth", model.ClientWidth.ToString(CultureInfo.InvariantCulture));

        if (model.ScaleHeight.HasValue) WriteProp("ScaleHeight", model.ScaleHeight.Value.ToString(CultureInfo.InvariantCulture));
        if (model.ScaleWidth.HasValue) WriteProp("ScaleWidth", model.ScaleWidth.Value.ToString(CultureInfo.InvariantCulture));
        if (model.ScaleMode.HasValue) WriteProp("ScaleMode", model.ScaleMode.Value.ToString(CultureInfo.InvariantCulture));
        if (model.StartUpPosition.HasValue) WriteProp("StartUpPosition", model.StartUpPosition.Value.ToString(CultureInfo.InvariantCulture), "'Windows Default");
        if (model.BorderStyle.HasValue) WriteProp("BorderStyle", model.BorderStyle.Value.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(model.BackColorHex)) WriteProp("BackColor", model.BackColorHex);
        if (!string.IsNullOrEmpty(model.ForeColorHex)) WriteProp("ForeColor", model.ForeColorHex);

        // Bloque Font
        if (model.Font != null)
        {
            SerializeFont(sb, model.Font, indentLevel);
            writtenProps.Add("Font");
        }

        // Propiedades adicionales existentes en CustomProperties
        foreach (var prop in model.CustomProperties)
        {
            if (writtenProps.Contains(prop.Name)) continue;

            if (prop.IsBeginProperty)
            {
                foreach (var line in prop.RawPropertyLines)
                {
                    sb.AppendLine(line);
                }
            }
            else
            {
                WriteProp(prop.Name, prop.Value, prop.TrailingComment);
            }
        }
    }

    private static void SerializeControl(StringBuilder sb, FormDesignerControl ctrl, int indentLevel)
    {
        if (ctrl.IsMenu)
        {
            SerializeMenuControl(sb, ctrl, indentLevel);
            return;
        }

        string indent = new string(' ', indentLevel * 3);
        string propIndent = new string(' ', (indentLevel + 1) * 3);

        // Begin ControlType ControlName
        sb.AppendLine($"{indent}Begin {ctrl.TypeName} {ctrl.Name} ");

        var writtenProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void WriteProp(string name, string value, string? comment = null)
        {
            writtenProps.Add(name);
            string formattedComment = string.IsNullOrEmpty(comment) ? string.Empty : $"  {comment}";
            string propPadded = name.Length < 16 ? name.PadRight(16) : name + " ";
            sb.AppendLine($"{propIndent}{propPadded}=   {value}{formattedComment}");
        }

        // Propiedades canónicas
        if (ctrl.Index.HasValue) WriteProp("Index", ctrl.Index.Value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(ctrl.Caption)) WriteProp("Caption", $"\"{ctrl.Caption}\"");
        if (!string.IsNullOrEmpty(ctrl.Text)) WriteProp("Text", $"\"{ctrl.Text}\"");

        WriteProp("Height", ctrl.Height.ToString(CultureInfo.InvariantCulture));
        WriteProp("Left", ctrl.Left.ToString(CultureInfo.InvariantCulture));

        if (ctrl.TabIndex.HasValue) WriteProp("TabIndex", ctrl.TabIndex.Value.ToString(CultureInfo.InvariantCulture));
        if (ctrl.TabStop.HasValue) WriteProp("TabStop", ctrl.TabStop.Value ? "0  'False" : "-1  'True");
        if (!ctrl.Enabled) WriteProp("Enabled", "0  'False");
        if (!ctrl.Visible) WriteProp("Visible", "0  'False");

        WriteProp("Top", ctrl.Top.ToString(CultureInfo.InvariantCulture));
        WriteProp("Width", ctrl.Width.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(ctrl.BackColorHex)) WriteProp("BackColor", ctrl.BackColorHex);
        if (!string.IsNullOrEmpty(ctrl.ForeColorHex)) WriteProp("ForeColor", ctrl.ForeColorHex);
        if (ctrl.BorderStyle.HasValue) WriteProp("BorderStyle", ctrl.BorderStyle.Value.ToString(CultureInfo.InvariantCulture));
        if (ctrl.Alignment.HasValue) WriteProp("Alignment", ctrl.Alignment.Value.ToString(CultureInfo.InvariantCulture));
        if (ctrl.MultiLine.HasValue && ctrl.MultiLine.Value) WriteProp("MultiLine", "-1  'True");
        if (ctrl.ScrollBars.HasValue && ctrl.ScrollBars.Value > 0) WriteProp("ScrollBars", ctrl.ScrollBars.Value.ToString(CultureInfo.InvariantCulture));

        // Font si aplica
        if (ctrl.Font != null)
        {
            SerializeFont(sb, ctrl.Font, indentLevel + 1);
            writtenProps.Add("Font");
        }

        // Propiedades conservadas en CustomProperties que no hayan sido escritas
        foreach (var prop in ctrl.CustomProperties)
        {
            if (writtenProps.Contains(prop.Name)) continue;

            if (prop.IsBeginProperty)
            {
                foreach (var line in prop.RawPropertyLines)
                {
                    sb.AppendLine(line);
                }
            }
            else
            {
                WriteProp(prop.Name, prop.Value, prop.TrailingComment);
            }
        }

        // Controles hijos (si es contenedor, ej. Frame, PictureBox o Menú)
        foreach (var child in ctrl.Children)
        {
            SerializeControl(sb, child, indentLevel + 1);
        }

        // End del control
        sb.AppendLine($"{indent}End");
    }

    private static void SerializeFont(StringBuilder sb, FormFontDescription font, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 3);
        string propIndent = new string(' ', (indentLevel + 1) * 3);

        sb.AppendLine($"{indent}BeginProperty Font ");
        sb.AppendLine($"{propIndent}{"Name".PadRight(16)}=   \"{font.Name}\"");
        sb.AppendLine($"{propIndent}{"Size".PadRight(16)}=   {font.Size.ToString("0.##", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"{propIndent}{"Charset".PadRight(16)}=   {font.Charset}");
        sb.AppendLine($"{propIndent}{"Weight".PadRight(16)}=   {font.Weight}");
        sb.AppendLine($"{propIndent}{"Underline".PadRight(16)}=   {(font.Underline ? "-1  'True" : "0   'False")}");
        sb.AppendLine($"{propIndent}{"Italic".PadRight(16)}=   {(font.Italic ? "-1  'True" : "0   'False")}");
        sb.AppendLine($"{propIndent}{"Strikethrough".PadRight(16)}=   {(font.Strikethrough ? "-1  'True" : "0   'False")}");
        sb.AppendLine($"{indent}EndProperty");
    }

    private static void SerializeMenuControl(StringBuilder sb, FormDesignerControl ctrl, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 3);
        string propIndent = new string(' ', (indentLevel + 1) * 3);

        sb.AppendLine($"{indent}Begin VB.Menu {ctrl.Name} ");

        var writtenProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void WriteProp(string name, string value, string? comment = null)
        {
            writtenProps.Add(name);
            string formattedComment = string.IsNullOrEmpty(comment) ? string.Empty : $"  {comment}";
            string propPadded = name.Length < 16 ? name.PadRight(16) : name + " ";
            sb.AppendLine($"{propIndent}{propPadded}=   {value}{formattedComment}");
        }

        if (!string.IsNullOrEmpty(ctrl.Caption)) WriteProp("Caption", $"\"{ctrl.Caption}\"");
        if (ctrl.Index.HasValue) WriteProp("Index", ctrl.Index.Value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(ctrl.Shortcut)) WriteProp("Shortcut", ctrl.Shortcut);
        if (ctrl.Checked) WriteProp("Checked", "-1  'True");
        if (!ctrl.Enabled) WriteProp("Enabled", "0  'False");
        if (!ctrl.Visible) WriteProp("Visible", "0  'False");
        if (ctrl.WindowList) WriteProp("WindowList", "-1  'True");
        if (ctrl.NegotiatePosition > 0) WriteProp("NegotiatePosition", ctrl.NegotiatePosition.ToString(CultureInfo.InvariantCulture));
        if (ctrl.HelpContextID > 0) WriteProp("HelpContextID", ctrl.HelpContextID.ToString(CultureInfo.InvariantCulture));

        // Propiedades conservadas en CustomProperties que no hayan sido escritas
        foreach (var prop in ctrl.CustomProperties)
        {
            if (writtenProps.Contains(prop.Name)) continue;
            if (prop.IsBeginProperty)
            {
                foreach (var line in prop.RawPropertyLines)
                {
                    sb.AppendLine(line);
                }
            }
            else
            {
                WriteProp(prop.Name, prop.Value, prop.TrailingComment);
            }
        }

        // Submenús anidados
        foreach (var child in ctrl.Children)
        {
            SerializeControl(sb, child, indentLevel + 1);
        }

        sb.AppendLine($"{indent}End");
    }
}
