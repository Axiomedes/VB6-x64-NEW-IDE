using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.Vbp;
using Xunit;

namespace ViBa7.Tests;

public class Phase4OcxAndVisualFidelityTests : IDisposable
{
    private readonly string _tempDir;

    static Phase4OcxAndVisualFidelityTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Phase4OcxAndVisualFidelityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VB7_Phase4Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Limpieza temporal
        }
    }

    // =========================================================================
    // 1. Resolución de Propiedades Universales de Controles de Terceros (OCX)
    // =========================================================================

    [Fact]
    public void FormDesignParser_CustomOcxProperties_Caption1AndCaption2_ParsedCorrectly()
    {
        string frmContent = @"VERSION 5.00
Object = ""{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0""; ""MSCOMCTL.OCX""
Begin VB.Form Form1
   Caption         =   ""Formulario de Prueba Fase 4""
   ClientHeight    =   6000
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   8000
   Begin AxFramework.AxGButtonLabel btnAceptar
      Height          =   495
      Left            =   1200
      Top             =   1500
      Width           =   2400
      _ExtentX        =   4233
      _ExtentY        =   873
      Caption1        =   ""Aceptar""
      Caption2        =   ""Guardar los cambios realizados""
      BackColor1      =   &H00FF8000&
      BackColor2      =   &H00804000&
      BackAngle       =   90
      BorderColor     =   &H00000000&
      BorderWidth     =   2
      CornerCurve     =   10
   End
End
";
        var form = FormDesignParser.Parse(frmContent);
        Assert.NotNull(form);
        Assert.Single(form.Children);

        var ctrl = form.Children[0];
        Assert.Equal("AxFramework.AxGButtonLabel", ctrl.TypeName);
        Assert.Equal("btnAceptar", ctrl.Name);
        Assert.Equal("Aceptar", ctrl.GetCustomPropertyString("Caption1"));
        Assert.Equal("Guardar los cambios realizados", ctrl.GetCustomPropertyString("Caption2"));
        Assert.Equal("&H00FF8000&", ctrl.GetCustomProperty("BackColor1"));
        Assert.Equal("&H00804000&", ctrl.GetCustomProperty("BackColor2"));
        Assert.Equal(90, ctrl.GetCustomPropertyInt("BackAngle"));
        Assert.Equal("&H00000000&", ctrl.GetCustomProperty("BorderColor"));
        Assert.Equal(2, ctrl.GetCustomPropertyInt("BorderWidth"));
        Assert.Equal(10, ctrl.GetCustomPropertyInt("CornerCurve"));
    }

    [Fact]
    public void FormDesignParser_EmbeddedCustomFontBlock_ParsedSuccessfully()
    {
        var rawFontLines = new List<string>
        {
            "   BeginProperty Caption1Font",
            "      Name            =   \"Segoe UI\"",
            "      Size            =   11.25",
            "      Charset         =   0",
            "      Weight          =   700",
            "      Underline       =   0   'False",
            "      Italic          =   -1  'True",
            "      Strikethrough   =   0   'False",
            "   EndProperty"
        };

        var font = FormDesignParser.ParseFontBlock(rawFontLines);
        Assert.NotNull(font);
        Assert.Equal("Segoe UI", font.Name);
        Assert.Equal(11.25, font.Size);
        Assert.True(font.IsBold);
        Assert.True(font.Italic);
        Assert.False(font.Underline);
        Assert.False(font.Strikethrough);
    }

    [Fact]
    public void FormDesignParser_FormatString_GridProperties_ParsedCorrectly()
    {
        string frmContent = @"VERSION 5.00
Begin VB.Form Form1
   Caption         =   ""Formulario Grilla""
   ClientHeight    =   5000
   ClientWidth     =   7000
   Begin MSFlexGridLib.MSFlexGrid grdArticulos
      Height          =   3000
      Left            =   200
      Top             =   300
      Width           =   6000
      _ExtentX        =   10583
      _ExtentY        =   5292
      FormatString    =   ""| ^Código | <Descripción del Producto | >Cantidad | >Precio | >Total |""
      BackColorFixed  =   -2147483633
      ForeColorFixed  =   -2147483630
      GridColor       =   12632256
      BackColorBkg    =   8421504
   End
End
";
        var form = FormDesignParser.Parse(frmContent);
        Assert.NotNull(form);
        Assert.Single(form.Children);

        var grid = form.Children[0];
        Assert.Equal("MSFlexGridLib.MSFlexGrid", grid.TypeName);
        string? formatStr = grid.GetCustomProperty("FormatString");
        Assert.NotNull(formatStr);
        Assert.Contains("Código", formatStr);
        Assert.Contains("Descripción del Producto", formatStr);
        Assert.Equal("-2147483633", grid.GetCustomProperty("BackColorFixed"));
        Assert.Equal("12632256", grid.GetCustomProperty("GridColor"));
    }

    // =========================================================================
    // 2. Soporte de Colores OLE Negativos y Formatos de Color VB6
    // =========================================================================

    [Fact]
    public void VbColorModel_ParseSigned32BitNegativeColors_CorrectlyResolvesSystemColors()
    {
        // En VB6 los colores del sistema OLE se guardan con frecuencia como enteros decimales negativos de 32 bits:
        // -2147483633 = 0x8000000F (COLOR_BTNFACE)
        var btnFace = VbColorModel.ParseColor("-2147483633", VbColorModel.DefaultControlColor);
        Assert.True(btnFace.IsSystemColor);
        Assert.Equal(0x0F, btnFace.SystemColorIndex);
        Assert.Equal(240, btnFace.R);
        Assert.Equal(240, btnFace.G);
        Assert.Equal(240, btnFace.B);

        // -2147483643 = 0x80000005 (COLOR_WINDOW / Blanco)
        var winCol = VbColorModel.ParseColor("-2147483643", VbColorModel.DefaultWindowColor);
        Assert.True(winCol.IsSystemColor);
        Assert.Equal(0x05, winCol.SystemColorIndex);
        Assert.Equal(255, winCol.R);
        Assert.Equal(255, winCol.G);
        Assert.Equal(255, winCol.B);

        // -2147483640 = 0x80000008 (COLOR_WINDOWTEXT / Negro)
        var textCol = VbColorModel.ParseColor("-2147483640", VbColorModel.DefaultTextColor);
        Assert.True(textCol.IsSystemColor);
        Assert.Equal(0x08, textCol.SystemColorIndex);
        Assert.Equal(0, textCol.R);
        Assert.Equal(0, textCol.G);
        Assert.Equal(0, textCol.B);

        // -2147483635 = 0x8000000D (COLOR_HIGHLIGHT / Azul Selección)
        var hlCol = VbColorModel.ParseColor("-2147483635", VbColorModel.DefaultControlColor);
        Assert.True(hlCol.IsSystemColor);
        Assert.Equal(0x0D, hlCol.SystemColorIndex);
        Assert.Equal(10, hlCol.R);
        Assert.Equal(36, hlCol.G);
        Assert.Equal(106, hlCol.B);
    }

    [Fact]
    public void VbColorModel_ParseDecimalPositiveColors_HandlesBgrEncoding()
    {
        // 16711680 = 0x00FF0000 (Azul puro en BGR)
        var blue = VbColorModel.ParseColor("16711680", VbColorModel.DefaultControlColor);
        Assert.Equal(0, blue.R);
        Assert.Equal(0, blue.G);
        Assert.Equal(255, blue.B);

        // 65280 = 0x0000FF00 (Verde puro)
        var green = VbColorModel.ParseColor("65280", VbColorModel.DefaultControlColor);
        Assert.Equal(0, green.R);
        Assert.Equal(255, green.G);
        Assert.Equal(0, green.B);

        // 255 = 0x000000FF (Rojo puro)
        var red = VbColorModel.ParseColor("255", VbColorModel.DefaultControlColor);
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);
    }

    // =========================================================================
    // 3. Extracción y Filtrado de Referencias Binarias .frx/.ctx
    // =========================================================================

    [Fact]
    public void FormDesignParser_FrxOffsetReferences_IdentifiedAndPreserved()
    {
        string frmContent = @"VERSION 5.00
Begin VB.Form Form1
   Caption         =   ""Test FRX Filter""
   ClientHeight    =   4000
   ClientWidth     =   6000
   Begin ServTecnico.axButton btnGuardar
      Height          =   495
      Left            =   600
      Top             =   800
      Width           =   1800
      Caption         =   ""Form1.frx"":0000
      Caption1        =   ""Guardar Registro""
      Title           =   ""Operación de Guardado""
   End
End
";
        var form = FormDesignParser.Parse(frmContent);
        Assert.NotNull(form);
        var ctrl = form.Children[0];

        // Caption contiene la referencia a FRX
        Assert.Equal("\"Form1.frx\":0000", ctrl.Caption);
        // Caption1 contiene el texto legible
        Assert.Equal("Guardar Registro", ctrl.GetCustomPropertyString("Caption1"));
        Assert.Equal("Operación de Guardado", ctrl.GetCustomPropertyString("Title"));
    }

    // =========================================================================
    // 4. Jerarquía de UserControl con Escalamiento Proporcional
    // =========================================================================

    [Fact]
    public void UserControl_ConstituentControls_Hierarchy_And_Scaling()
    {
        // Definición del UserControl en .ctl
        string ctlContent = @"VERSION 5.00
Begin VB.UserControl SearchPanel
   ClientHeight    =   1000
   ClientWidth     =   3000
   ScaleHeight     =   1000
   ScaleWidth      =   3000
   Begin VB.TextBox txtSearch
      Height          =   315
      Left            =   150
      Top             =   300
      Width           =   2000
   End
   Begin VB.CommandButton cmdSearch
      Caption         =   ""Buscar""
      Height          =   315
      Left            =   2200
      Top             =   300
      Width           =   700
   End
End
";
        var ucModel = FormDesignParser.Parse(ctlContent);
        Assert.NotNull(ucModel);
        Assert.Equal("SearchPanel", ucModel.Name);
        Assert.Equal(3000, ucModel.ClientWidth);
        Assert.Equal(1000, ucModel.ClientHeight);
        Assert.Equal(2, ucModel.Children.Count);

        // Instancia en un formulario .frm con tamaño el doble (6000x2000 twips)
        var instanceControl = new FormDesignerControl
        {
            TypeName = "SearchPanel",
            Name = "SearchPanel1",
            Left = 500,
            Top = 500,
            Width = 6000,
            Height = 2000
        };

        // Factores de escala calculados
        double scaleX = instanceControl.Width / (double)ucModel.ClientWidth;
        double scaleY = instanceControl.Height / (double)ucModel.ClientHeight;

        Assert.Equal(2.0, scaleX);
        Assert.Equal(2.0, scaleY);

        var txtChild = ucModel.Children.First(c => c.Name == "txtSearch");
        double scaledTxtLeft = txtChild.Left * scaleX;
        double scaledTxtWidth = txtChild.Width * scaleX;

        Assert.Equal(300.0, scaledTxtLeft);
        Assert.Equal(4000.0, scaledTxtWidth);
    }

    // =========================================================================
    // 5. Formulario Realista con Múltiples OCX de Terceros
    // =========================================================================

    [Fact]
    public void RealWorld_ServTecDev_Forms_WithThirdPartyOcxs_ParsedFully()
    {
        string frmContent = @"VERSION 5.00
Object = ""{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0""; ""MSCOMCTL.OCX""
Begin VB.Form frmLiquidacion
   Caption         =   ""Liquidación de Servicio""
   ClientHeight    =   8500
   ClientWidth     =   11000
   Begin AXLPCTRL.axLabelPlus lblEncabezado
      Height          =   375
      Left            =   300
      Top             =   200
      Width           =   5000
      Caption         =   ""Datos del Cliente y Facturación""
      Style           =   ""Divider""
      ForeColor       =   &H00800000&
      LineColor       =   &H00C0C0C0&
   End
   Begin ServTecnico.ucAxText txtRut
      Height          =   315
      Left            =   300
      Top             =   700
      Width           =   1800
      Text            =   ""12.345.678-9""
      BackColor       =   -2147483643
      ForeColor       =   -2147483640
      TextAlign       =   1
   End
   Begin ServTecnico.AxCheckBox chkFacturado
      Height          =   255
      Left            =   2400
      Top             =   730
      Width           =   1600
      Caption         =   ""Factura Emitida""
      Value           =   1
   End
   Begin AxioGrid.AxGrid grdDetalle
      Height          =   4000
      Left            =   300
      Top             =   1200
      Width           =   10400
      _ExtentX        =   18344
      _ExtentY        =   7056
      FormatString    =   ""|^Ítem|<Descripción del Trabajo|>Horas|>Valor Hora|>Total Neto|""
      BackColorFixed  =   -2147483633
      ForeColorFixed  =   0
      GridColor       =   12632256
      StripedColor    =   16775150
   End
   Begin AxFramework.AxGButtonLabel btnImprimir
      Height          =   550
      Left            =   8500
      Top             =   7600
      Width           =   2200
      Caption1        =   ""Imprimir Comprobante""
      Caption2        =   ""Enviar a impresora predeterminada""
      BackColor1      =   &H00D0E0FF&
      BackColor2      =   &H0090B0F0&
      BackAngle       =   180
      BorderColor     =   &H005070C0&
      CornerCurve     =   6
   End
End
";
        var form = FormDesignParser.Parse(frmContent);
        Assert.NotNull(form);
        Assert.Equal("frmLiquidacion", form.Name);
        Assert.Equal("Liquidación de Servicio", form.Caption);
        Assert.Equal(5, form.Children.Count);

        // 1. Divider Label
        var lbl = form.Children.First(c => c.Name == "lblEncabezado");
        Assert.Equal("AXLPCTRL.axLabelPlus", lbl.TypeName);
        Assert.Equal("Datos del Cliente y Facturación", lbl.Caption);
        Assert.Equal("Divider", lbl.GetCustomPropertyString("Style"));

        // 2. Custom TextBox
        var txt = form.Children.First(c => c.Name == "txtRut");
        Assert.Equal("ServTecnico.ucAxText", txt.TypeName);
        Assert.Equal("12.345.678-9", txt.Text);
        Assert.Equal(1, txt.GetCustomPropertyInt("TextAlign"));

        // 3. Custom CheckBox
        var chk = form.Children.First(c => c.Name == "chkFacturado");
        Assert.Equal("ServTecnico.AxCheckBox", chk.TypeName);
        Assert.Equal("Factura Emitida", chk.Caption);
        Assert.Equal(1, chk.GetCustomPropertyInt("Value"));

        // 4. Custom Grid
        var grd = form.Children.First(c => c.Name == "grdDetalle");
        Assert.Equal("AxioGrid.AxGrid", grd.TypeName);
        Assert.NotNull(grd.GetCustomProperty("FormatString"));
        Assert.Equal("16775150", grd.GetCustomProperty("StripedColor"));

        // 5. Custom Gradient Button with subcaption
        var btn = form.Children.First(c => c.Name == "btnImprimir");
        Assert.Equal("AxFramework.AxGButtonLabel", btn.TypeName);
        Assert.Equal("Imprimir Comprobante", btn.GetCustomPropertyString("Caption1"));
        Assert.Equal("Enviar a impresora predeterminada", btn.GetCustomPropertyString("Caption2"));
        Assert.Equal(180, btn.GetCustomPropertyInt("BackAngle"));
        Assert.Equal(6, btn.GetCustomPropertyInt("CornerCurve"));
    }

    // =========================================================================
    // 7. Pruebas de Detección Exhaustiva de OLE_COLOR y Extracción de UserControls
    // =========================================================================

    [Theory]
    [InlineData("BackColor", null, null, true)]
    [InlineData("BackColor1", null, null, true)]
    [InlineData("BackColor2", null, null, true)]
    [InlineData("ForeColorList", null, null, true)]
    [InlineData("BorderColorActive", null, null, true)]
    [InlineData("GridLineColor", null, null, true)]
    [InlineData("StyleThumbColor", null, null, true)]
    [InlineData("BoxColor", null, null, true)]
    [InlineData("ColorFace", null, null, true)]
    [InlineData("LineColor", null, null, true)]
    [InlineData("FillColor", null, null, true)]
    [InlineData("CustomProp", "OLE_COLOR", null, true)]
    [InlineData("PaletteEntry", "VbColor", null, true)]
    [InlineData("UnknownProp", null, "&H00FFFFFF&", true)]
    [InlineData("UnknownProp", null, "&H8000000F&", true)]
    [InlineData("UnknownProp", null, "-2147483633", true)]
    [InlineData("Caption", null, "Hello", false)]
    [InlineData("Text", null, "Sample", false)]
    [InlineData("Width", null, "1200", false)]
    [InlineData("Index", null, "0", false)]
    public void DesignerPropertyFactory_IsColorProperty_DetectsAccurately(string name, string? type, string? val, bool expected)
    {
        bool actual = DesignerPropertyFactory.IsColorProperty(name, type, val);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UserControlPropertyExtractor_ExtractsPropertiesWithoutPublicKeyword()
    {
        string ctlCode = @"
VERSION 5.00
Begin VB.UserControl axJCombo2 
End
Property Get BackColorList() As OLE_COLOR
    BackColorList = m_BackColorList
End Property

Property Let BackColorList(ByVal New_BackColorList As OLE_COLOR)
    m_BackColorList = New_BackColorList
    PropertyChanged ""BackColorList""
End Property

Property Get FocusRect() As Boolean
    FocusRect = m_FocusRect
End Property

Property Let FocusRect(ByVal New_FocusRect As Boolean)
    m_FocusRect = New_FocusRect
End Property

Private Property Get InternalSecret() As String
    InternalSecret = ""Hidden""
End Property

Private Sub UserControl_ReadProperties(PropBag As PropertyBag)
    m_BackColorList = PropBag.ReadProperty(""BackColorList"", &H00FFFFFF&)
    m_FocusRect = PropBag.ReadProperty(""FocusRect"", True)
    m_StripedColor = PropBag.ReadProperty(""StripedColor"", &H00EEEEEE&)
    m_FontCellText = PropBag.ReadProperty(""FontCellText"", Ambient.Font)
End Sub
";
        var props = UserControlPropertyExtractor.ExtractProperties(ctlCode);
        Assert.NotNull(props);

        // Debe extraer BackColorList aunque no tenga 'Public'
        var propBackColorList = props.FirstOrDefault(p => p.Name.Equals("BackColorList", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(propBackColorList);
        Assert.Equal("OLE_COLOR", propBackColorList.TypeName);
        Assert.Equal(DesignerPropertyType.ColorHex, propBackColorList.InferredType);

        // Debe extraer FocusRect aunque no tenga 'Public'
        var propFocusRect = props.FirstOrDefault(p => p.Name.Equals("FocusRect", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(propFocusRect);
        Assert.Equal("Boolean", propFocusRect.TypeName);
        Assert.Equal(DesignerPropertyType.Boolean, propFocusRect.InferredType);

        // NO debe extraer InternalSecret porque es 'Private'
        var propSecret = props.FirstOrDefault(p => p.Name.Equals("InternalSecret", StringComparison.OrdinalIgnoreCase));
        Assert.Null(propSecret);

        // Debe extraer propiedades de PropBag (StripedColor, FontCellText)
        var propStriped = props.FirstOrDefault(p => p.Name.Equals("StripedColor", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(propStriped);
        Assert.Equal("OLE_COLOR", propStriped.TypeName);
        Assert.Equal(DesignerPropertyType.ColorHex, propStriped.InferredType);

        var propFont = props.FirstOrDefault(p => p.Name.Equals("FontCellText", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(propFont);
        Assert.Equal("StdFont", propFont.TypeName);
        Assert.Equal(DesignerPropertyType.FontDescription, propFont.InferredType);
    }

    [Fact]
    public void DesignerPropertyFactory_LivePropertySetter_UpdatesCustomPropertiesAndStandardFields()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var ctrl = new FormDesignerControl
        {
            Name = "ucTest1",
            TypeName = "CustomUC.ucTest",
            Left = 1000,
            Top = 500,
            Width = 2000,
            Height = 1000
        };
        ctrl.CustomProperties.Add(new FormPropertyItem("BackColorList", "&H00FFFFFF&"));
        ctrl.CustomProperties.Add(new FormPropertyItem("Caption1", "Título Original"));
        ctrl.CustomProperties.Add(new FormPropertyItem("BorderColor", "&H00000000&"));
        model.Children.Add(ctrl);

        bool modelChangedInvoked = false;
        var props = DesignerPropertyFactory.GetPropertiesForControl(
            ctrl,
            model,
            onModelChanged: () => { modelChangedInvoked = true; });

        // 1. Verificar que BackColorList y BorderColor son identificados como ColorHex
        var pColorList = props.FirstOrDefault(p => p.Name == "BackColorList");
        Assert.NotNull(pColorList);
        Assert.Equal(DesignerPropertyType.ColorHex, pColorList.Type);

        var pBorder = props.FirstOrDefault(p => p.Name == "BorderColor");
        Assert.NotNull(pBorder);
        Assert.Equal(DesignerPropertyType.ColorHex, pBorder.Type);

        // 2. Modificar BackColorList
        bool setOk = pColorList.SetValue("&H00808080&");
        Assert.True(setOk);
        Assert.True(modelChangedInvoked);
        Assert.Equal("&H00808080&", ctrl.GetCustomProperty("BackColorList"));

        // 3. Modificar Caption en CustomProperties y verificar sincronización
        modelChangedInvoked = false;
        var pCaption1 = props.FirstOrDefault(p => p.Name == "Caption1");
        Assert.NotNull(pCaption1);
        pCaption1.SetValue("Nuevo Título 1");
        Assert.True(modelChangedInvoked);
        Assert.Equal("Nuevo Título 1", ctrl.GetCustomProperty("Caption1"));
    }

    // =========================================================================
    // 6. Aporte 1: Páginas de Propiedades del Fabricante y Propiedad (Custom)
    // =========================================================================

    [Fact]
    public void Aporte1_DesignerPropertyFactory_InsertsCustomPropertyAtBeginning_ForNonIntrinsicControls()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var ocxCtrl = new FormDesignerControl
        {
            Name = "ProgressBar1",
            TypeName = "MSComctlLib.ProgBar",
            Left = 1000,
            Top = 500,
            Width = 3000,
            Height = 400
        };
        model.Children.Add(ocxCtrl);

        var props = DesignerPropertyFactory.GetPropertiesForControl(ocxCtrl, model, () => { });

        Assert.NotEmpty(props);
        var firstProp = props[0];
        Assert.Equal("Custom", firstProp.Name);
        Assert.Equal("(Custom)", firstProp.DisplayName);
        Assert.Equal(DesignerPropertyType.CustomDialog, firstProp.Type);
        Assert.True(firstProp.IsReadOnly);
    }

    [Fact]
    public void Aporte1_DesignerPropertyFactory_InsertsCustomProperty_WhenHasPropertyPagesIsTrue()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var intrinsicCtrl = new FormDesignerControl
        {
            Name = "Command1",
            TypeName = "CommandButton",
            HasPropertyPages = true
        };
        model.Children.Add(intrinsicCtrl);

        var props = DesignerPropertyFactory.GetPropertiesForControl(intrinsicCtrl, model, () => { });

        Assert.NotEmpty(props);
        Assert.Equal("Custom", props[0].Name);
        Assert.Equal("(Custom)", props[0].DisplayName);
        Assert.Equal(DesignerPropertyType.CustomDialog, props[0].Type);
    }

    [Fact]
    public void Aporte1_IsColorProperty_DetectsAllVb6ColorRepresentations()
    {
        // 1. Tipos COM
        Assert.True(DesignerPropertyFactory.IsColorProperty("AnyProp", "OLE_COLOR", null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("AnyProp", "stdole.OLE_COLOR", null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("AnyProp", "ColorConstants", null));

        // 2. Nombres con indicios de color
        Assert.True(DesignerPropertyFactory.IsColorProperty("LineColor", null, null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("BorderColor", null, null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("FillColor", null, null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("TextClr", null, null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("BkColor", null, null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("FgColor", null, null));
        Assert.True(DesignerPropertyFactory.IsColorProperty("ColorFace", null, null));

        // 3. Valores hexadecimales VB6
        Assert.True(DesignerPropertyFactory.IsColorProperty("Custom1", null, "&H8000000F&"));
        Assert.True(DesignerPropertyFactory.IsColorProperty("Custom2", null, "&H00FFFFFF&"));
        Assert.True(DesignerPropertyFactory.IsColorProperty("Custom3", null, "&H00000000&"));
        Assert.True(DesignerPropertyFactory.IsColorProperty("Custom4", null, "&H00808080&"));

        // 4. Valores decimales de sistema con signo
        Assert.True(DesignerPropertyFactory.IsColorProperty("CustomSys", null, "-2147483633"));

        // 5. Valores decimales típicos RGB si el nombre tiene sugerencia de color
        Assert.True(DesignerPropertyFactory.IsColorProperty("BackColorNumber", null, "16777215"));
    }

    // =========================================================================
    // 7. Aporte 4: Detección, Preservación y Visual de Controles No Resueltos (IsUnresolved)
    // =========================================================================

    [Fact]
    public void Aporte4_FormDesignParser_MarksUnregisteredControlAsUnresolved_PreservingRawLines()
    {
        string frmContent = @"VERSION 5.00
Object = ""{E684D8A3-716C-4E59-AA94-7144C04B0074}#1.1#0""; ""GridEx.ocx""
Begin VB.Form Form1
   Caption         =   ""Formulario con OCX Faltante""
   ClientHeight    =   6000
   ClientWidth     =   8000
   Begin GridEx.JanusGrid Grid1
      Height          =   3500
      Left            =   400
      Top             =   500
      Width           =   7000
      _ExtentX        =   12348
      _ExtentY        =   6174
      _Version        =   393216
      GroupByBoxVisible=   0   'False
      FilterBar       =   -1  'True
   End
End
";
        string formPath = Path.Combine(_tempDir, "UnresolvedForm.frm");
        File.WriteAllText(formPath, frmContent, Encoding.Latin1);

        // Configurar delegado de resolución para simular que GridEx.ocx no está registrado
        FormDesignParser.ControlResolutionChecker = typeName =>
        {
            if (typeName.StartsWith("GridEx.", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        };

        try
        {
            var model = FormDesignParser.Parse(File.ReadAllText(formPath));
            Assert.NotNull(model);
            Assert.Single(model.Children);

            var unresolvedCtrl = model.Children[0];
            Assert.Equal("Grid1", unresolvedCtrl.Name);
            Assert.Equal("GridEx.JanusGrid", unresolvedCtrl.TypeName);
            Assert.True(unresolvedCtrl.IsUnresolved);
            Assert.False(string.IsNullOrWhiteSpace(unresolvedCtrl.UnresolvedErrorMessage));
            Assert.NotEmpty(unresolvedCtrl.RawControlLines);

            // Verificar que dimensiones y CustomProperties se preservaron intactas
            Assert.Equal(400, unresolvedCtrl.Left);
            Assert.Equal(500, unresolvedCtrl.Top);
            Assert.Equal(7000, unresolvedCtrl.Width);
            Assert.Equal(3500, unresolvedCtrl.Height);
            Assert.Equal("12348", unresolvedCtrl.GetCustomProperty("_ExtentX"));
            Assert.Equal("6174", unresolvedCtrl.GetCustomProperty("_ExtentY"));
            Assert.Equal("393216", unresolvedCtrl.GetCustomProperty("_Version"));
            Assert.Equal("0", unresolvedCtrl.GetCustomProperty("GroupByBoxVisible"));

            // Serializar y verificar que nada se perdió
            string serialized = FormDesignSerializer.Serialize(model);
            Assert.Contains("Begin GridEx.JanusGrid Grid1", serialized);
            Assert.Contains("_ExtentX", serialized);
            Assert.Contains("_ExtentY", serialized);
            Assert.Contains("GroupByBoxVisible", serialized);
            Assert.Contains("FilterBar", serialized);
        }
        finally
        {
            FormDesignParser.ControlResolutionChecker = null;
        }
    }

    [Fact]
    public void Aporte4_FormDesignerControl_Clone_PreservesUnresolvedFields()
    {
        var original = new FormDesignerControl
        {
            Name = "ctrlMissing",
            TypeName = "ThirdPartyLib.OcxGadget",
            IsUnresolved = true,
            UnresolvedErrorMessage = "El componente 'ThirdPartyLib.OcxGadget' no está registrado.",
            HasPropertyPages = true,
            RawControlLines = new List<string> { "Begin ThirdPartyLib.OcxGadget ctrlMissing", "End" }
        };

        var clone = original.Clone();

        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.TypeName, clone.TypeName);
        Assert.True(clone.IsUnresolved);
        Assert.Equal(original.UnresolvedErrorMessage, clone.UnresolvedErrorMessage);
        Assert.True(clone.HasPropertyPages);
        Assert.Equal(2, clone.RawControlLines.Count);
        Assert.Equal("Begin ThirdPartyLib.OcxGadget ctrlMissing", clone.RawControlLines[0]);
    }
}
