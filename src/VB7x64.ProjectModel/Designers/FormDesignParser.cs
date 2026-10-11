using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Parser de cabeceras visuales de formularios VB6 (.frm, .ctl, .pag).
/// Reconstruye el modelo jerárquico completo en memoria preservando cada propiedad original.
/// </summary>
public static class FormDesignParser
{
    private static readonly Regex BeginBlockRegex = new(
        @"^\s*Begin\s+([^\s]+)\s+([^\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndBlockRegex = new(
        @"^\s*End\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BeginPropertyRegex = new(
        @"^\s*BeginProperty\s+([^\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndPropertyRegex = new(
        @"^\s*EndProperty\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PropertyAssignmentRegex = new(
        @"^\s*([a-zA-Z0-9_]+)\s*=\s*(.*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parsea un fragmento de controles (por ejemplo pegados desde el portapapeles) y retorna la lista de controles.
    /// </summary>
    public static List<FormDesignerControl> ParseControlSnippet(string snippetText)
    {
        if (string.IsNullOrWhiteSpace(snippetText)) return new List<FormDesignerControl>();

        string trimmed = snippetText.TrimStart();
        string wrappedText;
        if (trimmed.StartsWith("VERSION ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Begin VB.Form", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Begin VB.UserControl", StringComparison.OrdinalIgnoreCase))
        {
            wrappedText = snippetText;
        }
        else
        {
            wrappedText = $"VERSION 5.00\r\nBegin VB.Form DummyHost\r\n{snippetText}\r\nEnd\r\n";
        }

        var model = Parse(wrappedText);
        return model.Children;
    }

    /// <summary>
    /// Parsea el texto de la cabecera visual de un formulario VB6 y retorna el modelo de diseño.
    /// </summary>
    public static FormDesignModel Parse(string headerText)
    {
        var model = new FormDesignModel();
        if (string.IsNullOrWhiteSpace(headerText))
        {
            return model;
        }

        string[] lines = headerText.Replace("\r\n", "\n").Split('\n');
        int lineIdx = 0;

        // 1. Extraer versión inicial si existe (ej. VERSION 5.00)
        while (lineIdx < lines.Length)
        {
            string trimmed = lines[lineIdx].Trim();
            if (trimmed.StartsWith("VERSION ", StringComparison.OrdinalIgnoreCase))
            {
                model.VersionHeader = trimmed;
                lineIdx++;
                break;
            }

            if (trimmed.StartsWith("Begin ", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            lineIdx++;
        }

        // Pila de control jerárquico
        var controlStack = new Stack<FormDesignerControl>();
        bool insideRootForm = false;
        bool rootFormEnded = false;

        // Estado de BeginProperty
        bool insideBeginProperty = false;
        string? currentPropertyName = null;
        var currentPropertyLines = new List<string>();

        for (; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Después del 'End' del formulario raíz, recopilar atributos de módulo (Attribute VB_*)
            if (rootFormEnded)
            {
                if (trimmed.StartsWith("Attribute ", StringComparison.OrdinalIgnoreCase))
                {
                    model.Attributes.Add(line.TrimEnd());
                }
                continue;
            }

            // Manejo de bloques BeginProperty ... EndProperty
            if (insideBeginProperty)
            {
                if (EndPropertyRegex.IsMatch(trimmed))
                {
                    insideBeginProperty = false;
                    currentPropertyLines.Add(line);

                    // Procesar la propiedad completada (especialmente Font)
                    if (string.Equals(currentPropertyName, "Font", StringComparison.OrdinalIgnoreCase))
                    {
                        var fontDesc = ParseFontBlock(currentPropertyLines);
                        if (controlStack.Count > 0)
                        {
                            controlStack.Peek().Font = fontDesc;
                        }
                        else if (insideRootForm)
                        {
                            model.Font = fontDesc;
                        }
                    }

                    var propItem = new FormPropertyItem
                    {
                        Name = currentPropertyName ?? "Property",
                        IsBeginProperty = true,
                        RawPropertyLines = new List<string>(currentPropertyLines)
                    };

                    if (controlStack.Count > 0)
                    {
                        controlStack.Peek().CustomProperties.Add(propItem);
                    }
                    else if (insideRootForm)
                    {
                        model.CustomProperties.Add(propItem);
                    }

                    currentPropertyName = null;
                    currentPropertyLines.Clear();
                    continue;
                }

                currentPropertyLines.Add(line);
                continue;
            }

            var beginPropertyMatch = BeginPropertyRegex.Match(trimmed);
            if (beginPropertyMatch.Success)
            {
                insideBeginProperty = true;
                currentPropertyName = beginPropertyMatch.Groups[1].Value.Trim();
                currentPropertyLines.Clear();
                currentPropertyLines.Add(line);
                continue;
            }

            // Comprobación de Begin de control o formulario
            var beginMatch = BeginBlockRegex.Match(trimmed);
            if (beginMatch.Success)
            {
                string typeName = beginMatch.Groups[1].Value.Trim();
                string ctrlName = beginMatch.Groups[2].Value.Trim();

                if (!insideRootForm)
                {
                    // Formulario raíz
                    insideRootForm = true;
                    model.RootType = typeName;
                    model.Name = ctrlName;
                    continue;
                }

                // Control hijo (de primer nivel o anidado en un contenedor existente)
                var newCtrl = new FormDesignerControl
                {
                    TypeName = typeName,
                    Name = ctrlName
                };

                if (controlStack.Count > 0)
                {
                    // Control anidado dentro de un contenedor (ej. Frame, PictureBox)
                    var parent = controlStack.Peek();
                    newCtrl.ParentControl = parent;
                    parent.Children.Add(newCtrl);
                }
                else
                {
                    // Control de primer nivel en el formulario
                    model.Children.Add(newCtrl);
                }

                controlStack.Push(newCtrl);
                continue;
            }

            // Comprobación de End de control o formulario
            if (EndBlockRegex.IsMatch(trimmed))
            {
                if (controlStack.Count > 0)
                {
                    controlStack.Pop();
                }
                else if (insideRootForm)
                {
                    insideRootForm = false;
                    rootFormEnded = true;
                }
                continue;
            }

            // Asignación de propiedad: Property = Value ['Comment]
            var propMatch = PropertyAssignmentRegex.Match(trimmed);
            if (propMatch.Success)
            {
                string propName = propMatch.Groups[1].Value.Trim();
                string rhs = propMatch.Groups[2].Value;

                var (valStr, commentStr) = SplitPropertyAndComment(rhs);
                string cleanVal = Unquote(valStr);

                var propItem = new FormPropertyItem(propName, valStr, commentStr);

                if (controlStack.Count > 0)
                {
                    var activeCtrl = controlStack.Peek();
                    ApplyControlProperty(activeCtrl, propName, cleanVal, valStr);
                    activeCtrl.CustomProperties.Add(propItem);
                }
                else if (insideRootForm)
                {
                    ApplyFormProperty(model, propName, cleanVal, valStr);
                    model.CustomProperties.Add(propItem);
                }
            }
        }

        return model;
    }

    private static void ApplyFormProperty(FormDesignModel model, string name, string cleanVal, string rawVal)
    {
        switch (name.ToUpperInvariant())
        {
            case "CAPTION":
                model.Caption = cleanVal;
                break;
            case "CLIENTWIDTH":
                if (int.TryParse(cleanVal, out int cw)) model.ClientWidth = cw;
                break;
            case "CLIENTHEIGHT":
                if (int.TryParse(cleanVal, out int ch)) model.ClientHeight = ch;
                break;
            case "CLIENTLEFT":
                if (int.TryParse(cleanVal, out int cl)) model.ClientLeft = cl;
                break;
            case "CLIENTTOP":
                if (int.TryParse(cleanVal, out int ct)) model.ClientTop = ct;
                break;
            case "SCALEWIDTH":
                if (int.TryParse(cleanVal, out int sw)) model.ScaleWidth = sw;
                break;
            case "SCALEHEIGHT":
                if (int.TryParse(cleanVal, out int sh)) model.ScaleHeight = sh;
                break;
            case "SCALEMODE":
                if (int.TryParse(cleanVal, out int sm)) model.ScaleMode = sm;
                break;
            case "STARTUPPOSITION":
                if (int.TryParse(cleanVal, out int sup)) model.StartUpPosition = sup;
                break;
            case "BORDERSTYLE":
                if (int.TryParse(cleanVal, out int bs)) model.BorderStyle = bs;
                break;
            case "BACKCOLOR":
                model.BackColorHex = rawVal.Trim();
                break;
            case "FORECOLOR":
                model.ForeColorHex = rawVal.Trim();
                break;
        }
    }

    private static void ApplyControlProperty(FormDesignerControl ctrl, string name, string cleanVal, string rawVal)
    {
        switch (name.ToUpperInvariant())
        {
            case "CAPTION":
                ctrl.Caption = cleanVal;
                break;
            case "TEXT":
                ctrl.Text = cleanVal;
                break;
            case "LEFT":
                if (int.TryParse(cleanVal, out int left)) ctrl.Left = left;
                break;
            case "TOP":
                if (int.TryParse(cleanVal, out int top)) ctrl.Top = top;
                break;
            case "WIDTH":
                if (int.TryParse(cleanVal, out int width)) ctrl.Width = width;
                break;
            case "HEIGHT":
                if (int.TryParse(cleanVal, out int height)) ctrl.Height = height;
                break;
            case "TABINDEX":
                if (int.TryParse(cleanVal, out int tidx)) ctrl.TabIndex = tidx;
                break;
            case "TABSTOP":
                ctrl.TabStop = !cleanVal.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                               !cleanVal.Equals("False", StringComparison.OrdinalIgnoreCase);
                break;
            case "ENABLED":
                ctrl.Enabled = !cleanVal.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                               !cleanVal.Equals("False", StringComparison.OrdinalIgnoreCase);
                break;
            case "VISIBLE":
                ctrl.Visible = !cleanVal.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                               !cleanVal.Equals("False", StringComparison.OrdinalIgnoreCase);
                break;
            case "CHECKED":
                ctrl.Checked = cleanVal.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                               cleanVal.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                               cleanVal.Equals("True", StringComparison.OrdinalIgnoreCase);
                break;
            case "SHORTCUT":
                ctrl.Shortcut = cleanVal;
                break;
            case "WINDOWLIST":
                ctrl.WindowList = cleanVal.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                                  cleanVal.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                  cleanVal.Equals("True", StringComparison.OrdinalIgnoreCase);
                break;
            case "NEGOTIATEPOSITION":
                if (int.TryParse(cleanVal, out int np)) ctrl.NegotiatePosition = np;
                break;
            case "HELPCONTEXTID":
                if (int.TryParse(cleanVal, out int hcid)) ctrl.HelpContextID = hcid;
                break;
            case "INDEX":
                if (int.TryParse(cleanVal, out int idx)) ctrl.Index = idx;
                break;
            case "BACKCOLOR":
                ctrl.BackColorHex = rawVal.Trim();
                break;
            case "FORECOLOR":
                ctrl.ForeColorHex = rawVal.Trim();
                break;
            case "BORDERSTYLE":
                if (int.TryParse(cleanVal, out int bs)) ctrl.BorderStyle = bs;
                break;
            case "ALIGNMENT":
                if (int.TryParse(cleanVal, out int al)) ctrl.Alignment = al;
                break;
            case "MULTILINE":
                ctrl.MultiLine = cleanVal.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                                 cleanVal.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                 cleanVal.Equals("True", StringComparison.OrdinalIgnoreCase);
                break;
            case "SCROLLBARS":
                if (int.TryParse(cleanVal, out int sb)) ctrl.ScrollBars = sb;
                break;
        }
    }

    private static FormFontDescription ParseFontBlock(List<string> lines)
    {
        var font = new FormFontDescription();

        foreach (var line in lines)
        {
            var match = PropertyAssignmentRegex.Match(line.Trim());
            if (match.Success)
            {
                string prop = match.Groups[1].Value.Trim().ToUpperInvariant();
                var (val, _) = SplitPropertyAndComment(match.Groups[2].Value);
                string clean = Unquote(val);

                switch (prop)
                {
                    case "NAME":
                        font.Name = clean;
                        break;
                    case "SIZE":
                        if (double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out double sz))
                        {
                            font.Size = sz;
                        }
                        break;
                    case "CHARSET":
                        if (int.TryParse(clean, out int cs)) font.Charset = cs;
                        break;
                    case "WEIGHT":
                        if (int.TryParse(clean, out int wt)) font.Weight = wt;
                        break;
                    case "UNDERLINE":
                        font.Underline = clean.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                                         clean.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                         clean.Equals("True", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "ITALIC":
                        font.Italic = clean.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                                      clean.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                      clean.Equals("True", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "STRIKETHROUGH":
                        font.Strikethrough = clean.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                                             clean.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                             clean.Equals("True", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
        }

        return font;
    }

    public static (string Value, string? Comment) SplitPropertyAndComment(string rhs)
    {
        bool inQuotes = false;
        for (int i = 0; i < rhs.Length; i++)
        {
            if (rhs[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (rhs[i] == '\'' && !inQuotes)
            {
                string val = rhs.Substring(0, i).Trim();
                string comment = rhs.Substring(i).Trim();
                return (val, comment);
            }
        }

        return (rhs.Trim(), null);
    }

    public static string Unquote(string str)
    {
        if (str.StartsWith("\"") && str.EndsWith("\"") && str.Length >= 2)
        {
            return str.Substring(1, str.Length - 2);
        }
        return str;
    }
}
