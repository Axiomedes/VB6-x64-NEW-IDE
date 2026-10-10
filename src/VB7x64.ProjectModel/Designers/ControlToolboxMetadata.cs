using System;
using System.Collections.Generic;
using System.Linq;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Metadatos y definición de un control disponible en la Caja de Herramientas (Toolbox) de VB6.
/// </summary>
public record ToolboxItemInfo(
    string TypeName,
    string DisplayName,
    string DefaultPrefix,
    string Glyph,
    string ToolTip,
    int DefaultWidthTwips,
    int DefaultHeightTwips,
    bool IsContainer = false,
    bool HasCaption = false,
    bool HasText = false
);

/// <summary>
/// Registro central de herramientas intrínsecas de VB6 para la Caja de Herramientas y creación de controles.
/// </summary>
public static class ControlToolboxRegistry
{
    public const string PointerTool = "Pointer";

    private static readonly List<ToolboxItemInfo> _tools = new()
    {
        new ToolboxItemInfo(
            TypeName: PointerTool,
            DisplayName: "Puntero",
            DefaultPrefix: "",
            Glyph: "↖",
            ToolTip: "Puntero de selección e interactividad",
            DefaultWidthTwips: 0,
            DefaultHeightTwips: 0
        ),
        new ToolboxItemInfo(
            TypeName: "VB.PictureBox",
            DisplayName: "PictureBox",
            DefaultPrefix: "Picture",
            Glyph: "🖼",
            ToolTip: "Caja de imagen y lienzo contenedor (PictureBox)",
            DefaultWidthTwips: 1575,
            DefaultHeightTwips: 1215,
            IsContainer: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Label",
            DisplayName: "Label",
            DefaultPrefix: "Label",
            Glyph: "A",
            ToolTip: "Etiqueta de texto estático (Label)",
            DefaultWidthTwips: 1095,
            DefaultHeightTwips: 255,
            HasCaption: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.TextBox",
            DisplayName: "TextBox",
            DefaultPrefix: "Text",
            Glyph: "ab|",
            ToolTip: "Caja de edición de texto (TextBox)",
            DefaultWidthTwips: 1455,
            DefaultHeightTwips: 375,
            HasText: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Frame",
            DisplayName: "Frame",
            DefaultPrefix: "Frame",
            Glyph: "[...]",
            ToolTip: "Marco agrupador de controles (Frame)",
            DefaultWidthTwips: 2535,
            DefaultHeightTwips: 1695,
            IsContainer: true,
            HasCaption: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.CommandButton",
            DisplayName: "CommandButton",
            DefaultPrefix: "Command",
            Glyph: "🔲",
            ToolTip: "Botón de comando (CommandButton)",
            DefaultWidthTwips: 1335,
            DefaultHeightTwips: 495,
            HasCaption: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.CheckBox",
            DisplayName: "CheckBox",
            DefaultPrefix: "Check",
            Glyph: "☑",
            ToolTip: "Casilla de verificación (CheckBox)",
            DefaultWidthTwips: 1335,
            DefaultHeightTwips: 315,
            HasCaption: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.OptionButton",
            DisplayName: "OptionButton",
            DefaultPrefix: "Option",
            Glyph: "🔘",
            ToolTip: "Botón de opción radial (OptionButton)",
            DefaultWidthTwips: 1335,
            DefaultHeightTwips: 315,
            HasCaption: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.ComboBox",
            DisplayName: "ComboBox",
            DefaultPrefix: "Combo",
            Glyph: "▼",
            ToolTip: "Lista desplegable y cuadro combinado (ComboBox)",
            DefaultWidthTwips: 1575,
            DefaultHeightTwips: 315,
            HasText: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.ListBox",
            DisplayName: "ListBox",
            DefaultPrefix: "List",
            Glyph: "≡",
            ToolTip: "Cuadro de lista con opciones (ListBox)",
            DefaultWidthTwips: 1455,
            DefaultHeightTwips: 1410
        ),
        new ToolboxItemInfo(
            TypeName: "VB.HScrollBar",
            DisplayName: "HScrollBar",
            DefaultPrefix: "HScroll",
            Glyph: "◄▬►",
            ToolTip: "Barra de desplazamiento horizontal (HScrollBar)",
            DefaultWidthTwips: 1455,
            DefaultHeightTwips: 255
        ),
        new ToolboxItemInfo(
            TypeName: "VB.VScrollBar",
            DisplayName: "VScrollBar",
            DefaultPrefix: "VScroll",
            Glyph: "▲❚▼",
            ToolTip: "Barra de desplazamiento vertical (VScrollBar)",
            DefaultWidthTwips: 255,
            DefaultHeightTwips: 1455
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Timer",
            DisplayName: "Timer",
            DefaultPrefix: "Timer",
            Glyph: "⏱",
            ToolTip: "Temporizador periódico (Timer)",
            DefaultWidthTwips: 480,
            DefaultHeightTwips: 480
        ),
        new ToolboxItemInfo(
            TypeName: "VB.DriveListBox",
            DisplayName: "DriveListBox",
            DefaultPrefix: "Drive",
            Glyph: "🖴",
            ToolTip: "Lista de unidades de disco (DriveListBox)",
            DefaultWidthTwips: 1695,
            DefaultHeightTwips: 315
        ),
        new ToolboxItemInfo(
            TypeName: "VB.DirListBox",
            DisplayName: "DirListBox",
            DefaultPrefix: "Dir",
            Glyph: "📁",
            ToolTip: "Lista de directorios del sistema (DirListBox)",
            DefaultWidthTwips: 1695,
            DefaultHeightTwips: 1410
        ),
        new ToolboxItemInfo(
            TypeName: "VB.FileListBox",
            DisplayName: "FileListBox",
            DefaultPrefix: "File",
            Glyph: "📄",
            ToolTip: "Lista de archivos de carpeta (FileListBox)",
            DefaultWidthTwips: 1695,
            DefaultHeightTwips: 1410
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Shape",
            DisplayName: "Shape",
            DefaultPrefix: "Shape",
            Glyph: "◯",
            ToolTip: "Forma geométrica decorativa (Shape)",
            DefaultWidthTwips: 1335,
            DefaultHeightTwips: 855
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Line",
            DisplayName: "Line",
            DefaultPrefix: "Line",
            Glyph: "╱",
            ToolTip: "Línea gráfica divisoria (Line)",
            DefaultWidthTwips: 1455,
            DefaultHeightTwips: 0
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Image",
            DisplayName: "Image",
            DefaultPrefix: "Image",
            Glyph: "🎨",
            ToolTip: "Imagen gráfica ligera (Image)",
            DefaultWidthTwips: 1215,
            DefaultHeightTwips: 975
        ),
        new ToolboxItemInfo(
            TypeName: "VB.Data",
            DisplayName: "Data",
            DefaultPrefix: "Data",
            Glyph: "⛁",
            ToolTip: "Control de enlace de datos DAO/ODBC (Data)",
            DefaultWidthTwips: 1935,
            DefaultHeightTwips: 375,
            HasCaption: true
        ),
        new ToolboxItemInfo(
            TypeName: "VB.OLE",
            DisplayName: "OLE",
            DefaultPrefix: "OLE",
            Glyph: "◫",
            ToolTip: "Contenedor de objetos OLE incrustados (OLE)",
            DefaultWidthTwips: 1575,
            DefaultHeightTwips: 1215
        )
    };

    public static IReadOnlyList<ToolboxItemInfo> GetAllTools() => _tools;

    public static ToolboxItemInfo? Find(string typeName)
    {
        return _tools.FirstOrDefault(t =>
            string.Equals(t.TypeName, typeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.DisplayName, typeName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Instancia un nuevo control con nombre autogenerado único, dimensiones estándar y textos por defecto.
    /// </summary>
    public static FormDesignerControl CreateControl(
        string typeName,
        FormDesignModel model,
        int leftTwips,
        int topTwips,
        int? widthTwips = null,
        int? heightTwips = null,
        FormDesignerControl? parentControl = null)
    {
        var info = Find(typeName);
        string prefix = info?.DefaultPrefix ?? "Control";
        string uniqueName = model.GenerateUniqueName(prefix);

        int w = widthTwips ?? info?.DefaultWidthTwips ?? 1200;
        int h = heightTwips ?? info?.DefaultHeightTwips ?? 400;

        // Asegurar que controles de 1 dimensión como Line mantengan dimensiones válidas
        if (w <= 0 && !string.Equals(typeName, "VB.Line", StringComparison.OrdinalIgnoreCase)) w = 300;
        if (h <= 0 && !string.Equals(typeName, "VB.Line", StringComparison.OrdinalIgnoreCase)) h = 300;

        var control = new FormDesignerControl
        {
            TypeName = typeName.StartsWith("VB.", StringComparison.OrdinalIgnoreCase) ? typeName : $"VB.{typeName}",
            Name = uniqueName,
            Left = Math.Max(0, leftTwips),
            Top = Math.Max(0, topTwips),
            Width = w,
            Height = h,
            ParentControl = parentControl
        };

        if (info?.HasCaption == true)
        {
            control.Caption = uniqueName;
        }

        if (info?.HasText == true)
        {
            control.Text = uniqueName;
        }

        return control;
    }
}
