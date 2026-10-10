using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VB7x64.ProjectModel.Designers;

public static class TabOrderEngine
{
    private static readonly Regex BeginControlRegex = new(
        @"^\s*Begin\s+([a-zA-Z0-9_.]+)\s+([a-zA-Z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndBlockRegex = new(
        @"^\s*End\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PropertyRegex = new(
        @"^\s*([a-zA-Z0-9_]+)\s*=\s*(.*)$",
        RegexOptions.Compiled);

    // Tipos de control que normalmente no participan en tabulación (imágenes, timers, líneas, formas, etc.)
    private static readonly HashSet<string> NonTabbableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "VB.Timer", "VB.Line", "VB.Shape", "VB.Image", "VB.Menu"
    };

    /// <summary>
    /// Extrae todos los controles tabulables de la cabecera visual de un formulario VB6.
    /// </summary>
    public static List<FormControlItem> ParseControls(string headerAndDesign)
    {
        var controls = new List<FormControlItem>();
        if (string.IsNullOrWhiteSpace(headerAndDesign))
            return controls;

        string[] lines = headerAndDesign.Replace("\r\n", "\n").Split('\n');
        var containerStack = new Stack<string>();
        FormControlItem? currentControl = null;
        int depth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            var beginMatch = BeginControlRegex.Match(trimmed);
            if (beginMatch.Success)
            {
                depth++;
                string typeName = beginMatch.Groups[1].Value.Trim();
                string ctrlName = beginMatch.Groups[2].Value.Trim();

                if (depth == 1 && (typeName.Equals("VB.Form", StringComparison.OrdinalIgnoreCase) ||
                                  typeName.Equals("VB.MDIForm", StringComparison.OrdinalIgnoreCase) ||
                                  typeName.Equals("VB.UserControl", StringComparison.OrdinalIgnoreCase) ||
                                  typeName.Equals("VB.PropertyPage", StringComparison.OrdinalIgnoreCase)))
                {
                    // Es el formulario raíz, no lo agregamos a los controles hijos
                    containerStack.Push(ctrlName);
                    continue;
                }

                string currentContainer = containerStack.Count > 0 ? containerStack.Peek() : string.Empty;

                currentControl = new FormControlItem
                {
                    Name = ctrlName,
                    TypeName = typeName,
                    ContainerName = currentContainer,
                    TabIndex = controls.Count // Índice provisional
                };

                controls.Add(currentControl);
                containerStack.Push(ctrlName);
                continue;
            }

            if (EndBlockRegex.IsMatch(trimmed))
            {
                if (depth > 0) depth--;
                if (containerStack.Count > 0)
                {
                    containerStack.Pop();
                }
                currentControl = null;
                continue;
            }

            // Propiedades dentro de un control
            if (currentControl != null)
            {
                var propMatch = PropertyRegex.Match(trimmed);
                if (propMatch.Success)
                {
                    string propName = propMatch.Groups[1].Value.Trim();
                    string propVal = propMatch.Groups[2].Value.Trim();

                    switch (propName.ToUpperInvariant())
                    {
                        case "TABINDEX":
                            if (int.TryParse(propVal, out int tidx))
                            {
                                currentControl.TabIndex = tidx;
                                currentControl.HasExplicitTabIndex = true;
                            }
                            break;

                        case "TABSTOP":
                            currentControl.TabStop = !propVal.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                                                     !propVal.Equals("False", StringComparison.OrdinalIgnoreCase);
                            break;

                        case "LEFT":
                            if (int.TryParse(propVal, out int left)) currentControl.Left = left;
                            break;

                        case "TOP":
                            if (int.TryParse(propVal, out int top)) currentControl.Top = top;
                            break;

                        case "WIDTH":
                            if (int.TryParse(propVal, out int width)) currentControl.Width = width;
                            break;

                        case "HEIGHT":
                            if (int.TryParse(propVal, out int height)) currentControl.Height = height;
                            break;

                        case "CAPTION":
                            currentControl.Caption = propVal.Trim('"');
                            break;
                    }
                }
            }
        }

        // Filtrar controles no tabulables y ordenar por su TabIndex actual
        return controls
            .Where(c => !NonTabbableTypes.Contains(c.TypeName))
            .OrderBy(c => c.TabIndex)
            .ToList();
    }

    /// <summary>
    /// Reordena los controles geográficamente de arriba a abajo y de izquierda a derecha.
    /// </summary>
    public static List<FormControlItem> AutoOrderByGeography(List<FormControlItem> controls, int rowTolerance = 180)
    {
        if (controls == null || controls.Count <= 1)
            return controls?.ToList() ?? new List<FormControlItem>();

        // Agrupación por filas con tolerancia vertical
        var sorted = controls
            .OrderBy(c => c.Top)
            .ThenBy(c => c.Left)
            .ToList();

        // Refinamiento de filas: si dos controles tienen un Top cercano (dentro de rowTolerance), ordenar por Left
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            for (int j = i + 1; j < sorted.Count; j++)
            {
                if (Math.Abs(sorted[i].Top - sorted[j].Top) <= rowTolerance)
                {
                    if (sorted[i].Left > sorted[j].Left)
                    {
                        var temp = sorted[i];
                        sorted[i] = sorted[j];
                        sorted[j] = temp;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        // Asignar nuevos TabIndex secuenciales
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].TabIndex = i;
        }

        return sorted;
    }

    /// <summary>
    /// Aplica los nuevos valores de TabIndex en el bloque de cabecera visual del archivo .frm.
    /// </summary>
    public static string ApplyTabOrder(string headerAndDesign, List<FormControlItem> orderedControls)
    {
        if (string.IsNullOrWhiteSpace(headerAndDesign) || orderedControls == null || orderedControls.Count == 0)
            return headerAndDesign;

        // Diccionario de nombre de control -> nuevo TabIndex
        var tabIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < orderedControls.Count; i++)
        {
            tabIndexMap[orderedControls[i].Name] = i;
        }

        string[] lines = headerAndDesign.Replace("\r\n", "\n").Split('\n');
        var resultLines = new List<string>();

        string? currentCtrlName = null;
        bool hasWrittenTabIndexForCurrent = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            var beginMatch = BeginControlRegex.Match(trimmed);
            if (beginMatch.Success)
            {
                string ctrlName = beginMatch.Groups[2].Value.Trim();
                if (tabIndexMap.ContainsKey(ctrlName))
                {
                    currentCtrlName = ctrlName;
                    hasWrittenTabIndexForCurrent = false;
                }
                else
                {
                    currentCtrlName = null;
                }

                resultLines.Add(line);
                continue;
            }

            if (currentCtrlName != null)
            {
                if (trimmed.StartsWith("TabIndex", StringComparison.OrdinalIgnoreCase))
                {
                    // Reemplazar la línea existente de TabIndex con el nuevo valor
                    int newIdx = tabIndexMap[currentCtrlName];
                    int leadingSpaces = line.Length - line.TrimStart().Length;
                    string indent = leadingSpaces > 0 ? line.Substring(0, leadingSpaces) : "      ";
                    resultLines.Add($"{indent}TabIndex        =   {newIdx}");
                    hasWrittenTabIndexForCurrent = true;
                    continue;
                }

                if (EndBlockRegex.IsMatch(trimmed))
                {
                    // Si el control no tenía TabIndex explícito, insertarlo antes del End
                    if (!hasWrittenTabIndexForCurrent && tabIndexMap.TryGetValue(currentCtrlName, out int newIdx))
                    {
                        resultLines.Add($"      TabIndex        =   {newIdx}");
                    }
                    currentCtrlName = null;
                    resultLines.Add(line);
                    continue;
                }
            }

            resultLines.Add(line);
        }

        return string.Join("\r\n", resultLines);
    }
}
