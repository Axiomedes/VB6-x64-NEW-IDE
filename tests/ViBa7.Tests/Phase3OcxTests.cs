using System;
using System.IO;
using System.Linq;
using System.Text;
using ViBa7.ProjectModel.ActiveX;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.IntelliSense;
using ViBa7.ProjectModel.Vbp;
using Xunit;

namespace ViBa7.Tests;

public class Phase3OcxTests : IDisposable
{
    private readonly string _tempDir;

    static Phase3OcxTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Phase3OcxTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ViBa7_Phase3Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private static OcxLibraryInfo CreateMockOcxLibrary()
    {
        var lib = new OcxLibraryInfo
        {
            LibraryName = "AxAdvancedLib",
            FilePath = @"C:\Mock\AxAdvancedLib.ocx",
            Guid = "{99999999-8888-7777-6666-555555555555}",
            MajorVersion = 2,
            MinorVersion = 0
        };

        var ctrl = new OcxControlInfo
        {
            Name = "AxAdvancedGrid",
            Description = "Advanced Enterprise Grid Control",
            IsControl = true,
            Clsid = "{99999999-8888-7777-6666-000000000001}"
        };

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "ShowHeader",
            Kind = VbSymbolKind.Property,
            ReturnType = "Boolean",
            Description = "Determina si la cabecera de la grilla está visible"
        });

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "GridColor",
            Kind = VbSymbolKind.Property,
            ReturnType = "OLE_COLOR",
            Description = "Color de las líneas de la cuadrícula"
        });

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "RowCount",
            Kind = VbSymbolKind.Property,
            ReturnType = "Long",
            Description = "Número total de filas"
        });

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "CustomTitle",
            Kind = VbSymbolKind.Property,
            ReturnType = "String",
            Description = "Título personalizado de la cuadrícula"
        });

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "Watermark",
            Kind = VbSymbolKind.Property,
            ReturnType = "IPictureDisp",
            Description = "Gráfico de marca de agua de fondo"
        });

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "RefreshData",
            Kind = VbSymbolKind.Method,
            ReturnType = "Void",
            Description = "Recarga los datos de la grilla"
        });

        ctrl.Events.Add(new OcxEventInfo
        {
            Name = "RowClick",
            Description = "Se produce al hacer clic sobre una fila"
        });

        lib.Controls.Add(ctrl);
        return lib;
    }

    [Fact]
    public void VbControlMemberCatalog_RegistersOcx_AndExposesMembersInDesignerPropertyGrid()
    {
        var lib = CreateMockOcxLibrary();
        VbControlMemberCatalog.RegisterOcxLibrary(lib);

        try
        {
            var form = new FormDesignModel { Name = "Form1" };
            var ctrl = new FormDesignerControl
            {
                Name = "AxAdvancedGrid1",
                TypeName = "AxAdvancedGrid",
                Left = 120,
                Top = 240,
                Width = 4800,
                Height = 3600
            };
            form.Children.Add(ctrl);

            bool changed = false;
            var props = DesignerPropertyFactory.CreateForControl(ctrl, () => changed = true, form);

            var ocxProps = props.Where(p => p.Category == "ActiveX / TypeLib").ToList();
            Assert.NotEmpty(ocxProps);

            // Verificar ShowHeader (Boolean)
            var showHeaderProp = ocxProps.FirstOrDefault(p => p.Name == "ShowHeader");
            Assert.NotNull(showHeaderProp);
            Assert.Equal(DesignerPropertyType.Boolean, showHeaderProp.Type);
            Assert.Equal(2, showHeaderProp.Choices.Count);

            // Verificar GridColor (ColorHex)
            var gridColorProp = ocxProps.FirstOrDefault(p => p.Name == "GridColor");
            Assert.NotNull(gridColorProp);
            Assert.Equal(DesignerPropertyType.ColorHex, gridColorProp.Type);

            // Verificar RowCount (IntegerRaw)
            var rowCountProp = ocxProps.FirstOrDefault(p => p.Name == "RowCount");
            Assert.NotNull(rowCountProp);
            Assert.Equal(DesignerPropertyType.IntegerRaw, rowCountProp.Type);

            // Verificar CustomTitle (Text)
            var customTitleProp = ocxProps.FirstOrDefault(p => p.Name == "CustomTitle");
            Assert.NotNull(customTitleProp);
            Assert.Equal(DesignerPropertyType.Text, customTitleProp.Type);

            // Verificar Watermark (Picture)
            var watermarkProp = ocxProps.FirstOrDefault(p => p.Name == "Watermark");
            Assert.NotNull(watermarkProp);
            Assert.Equal(DesignerPropertyType.Picture, watermarkProp.Type);

            // Métodos y Eventos no deben estar en la lista de propiedades del diseñador
            Assert.DoesNotContain(props, p => p.Name == "RefreshData");
            Assert.DoesNotContain(props, p => p.Name == "RowClick");

            // Modificar propiedad ShowHeader a False
            bool setResult = showHeaderProp.SetValue("False");
            Assert.True(setResult);
            Assert.True(changed);
            Assert.Equal("False", ctrl.GetCustomProperty("ShowHeader"));

            // Modificar propiedad CustomTitle
            setResult = customTitleProp.SetValue("Ventas Anuales 2026");
            Assert.True(setResult);
            Assert.Equal("Ventas Anuales 2026", ctrl.GetCustomProperty("CustomTitle"));
        }
        finally
        {
            VbControlMemberCatalog.UnregisterOcxLibrary("AxAdvancedLib");
        }
    }

    [Fact]
    public void DesignerPropertyGrid_OcxProperties_PrepopulatesExistingValuesFromCustomProperties()
    {
        var lib = CreateMockOcxLibrary();
        VbControlMemberCatalog.RegisterOcxLibrary(lib);

        try
        {
            var form = new FormDesignModel { Name = "Form1" };
            var ctrl = new FormDesignerControl
            {
                Name = "AxAdvancedGrid1",
                TypeName = "AxAdvancedLib.AxAdvancedGrid",
                Left = 120,
                Top = 240,
                Width = 4800,
                Height = 3600
            };
            ctrl.CustomProperties.Add(new FormPropertyItem("ShowHeader", "False"));
            ctrl.CustomProperties.Add(new FormPropertyItem("RowCount", "5000"));
            form.Children.Add(ctrl);

            var props = DesignerPropertyFactory.CreateForControl(ctrl, () => { }, form);

            var showHeaderProp = props.FirstOrDefault(p => p.Name == "ShowHeader");
            Assert.NotNull(showHeaderProp);
            Assert.Equal("False", showHeaderProp.CurrentValue);

            var rowCountProp = props.FirstOrDefault(p => p.Name == "RowCount");
            Assert.NotNull(rowCountProp);
            Assert.Equal("5000", rowCountProp.CurrentValue);
        }
        finally
        {
            VbControlMemberCatalog.UnregisterOcxLibrary("AxAdvancedLib");
        }
    }

    [Fact]
    public void ControlToolboxRegistry_RegisterOcxTools_RegistersAndUnregistersProperly()
    {
        var lib = CreateMockOcxLibrary();
        ControlToolboxRegistry.RegisterOcxTools(lib);

        try
        {
            var tool = ControlToolboxRegistry.GetAllTools().FirstOrDefault(t => t.TypeName == "AxAdvancedLib.AxAdvancedGrid");
            Assert.NotNull(tool);
            Assert.Equal("AxAdvancedGrid", tool.DisplayName);
            Assert.Equal("AdvancedGrid", tool.DefaultPrefix);
            Assert.Equal("🧩", tool.Glyph);
            Assert.False(tool.IsContainer);
        }
        finally
        {
            ControlToolboxRegistry.UnregisterOcxTools("AxAdvancedLib");
            var toolAfter = ControlToolboxRegistry.GetAllTools().FirstOrDefault(t => t.TypeName == "AxAdvancedLib.AxAdvancedGrid");
            Assert.Null(toolAfter);
        }
    }

    [Fact]
    public void FormDesignerControl_HiMetricAndGeometry_HandlesOcxScalingAccurately()
    {
        // 5080 HiMetric = 2 pulgadas = 2880 Twips = 192 px
        // 2540 HiMetric = 1 pulgada = 1440 Twips = 96 px
        int widthTwips = FormDesignerControl.HiMetricToTwips(5080);
        int heightTwips = FormDesignerControl.HiMetricToTwips(2540);

        Assert.Equal(2880, widthTwips);
        Assert.Equal(1440, heightTwips);

        var ctrl = new FormDesignerControl
        {
            Name = "Grid1",
            TypeName = "MSFlexGridLib.MSFlexGrid",
            Width = widthTwips,
            Height = heightTwips
        };

        Assert.Equal(192.0, ctrl.WidthPixels);
        Assert.Equal(96.0, ctrl.HeightPixels);

        // Inversa: Twips a HiMetric
        Assert.Equal(5080, FormDesignerControl.TwipsToHiMetric(widthTwips));
        Assert.Equal(2540, FormDesignerControl.TwipsToHiMetric(heightTwips));
    }

    [Fact]
    public void FormDesignSerializer_Preserves_OcxCustomProperties_AndBeginProperties()
    {
        string originalFrm = @"VERSION 5.00
Object = ""{5E9E78A0-531B-11CF-91F6-C2863C385E30}#1.0#0""; ""MSFLXGRD.OCX""
Begin VB.Form FormMain 
   Caption         =   ""Grilla Principal""
   ClientHeight    =   4800
   ClientWidth     =   6400
   Begin MSFlexGridLib.MSFlexGrid Flex1 
      Height          =   3600
      Left            =   240
      TabIndex        =   0
      Top             =   240
      Width           =   5800
      _ExtentX        =   10231
      _ExtentY        =   6350
      _Version        =   393216
      Rows            =   50
      Cols            =   10
      FixedRows       =   1
      FixedCols       =   1
      AllowUserResizing=   3
   End
End
";

        var model = FormDesignParser.Parse(originalFrm);
        Assert.NotNull(model);
        Assert.Single(model.Children);

        var gridCtrl = model.Children[0];
        Assert.Equal("Flex1", gridCtrl.Name);
        Assert.Equal("MSFlexGridLib.MSFlexGrid", gridCtrl.TypeName);
        Assert.Equal("50", gridCtrl.GetCustomProperty("Rows"));
        Assert.Equal("10", gridCtrl.GetCustomProperty("Cols"));
        Assert.Equal("3", gridCtrl.GetCustomProperty("AllowUserResizing"));

        // Modificar una propiedad
        gridCtrl.SetCustomProperty("Rows", "100");

        // Serializar de nuevo
        string serialized = FormDesignSerializer.Serialize(model);

        Assert.Contains("Begin MSFlexGridLib.MSFlexGrid Flex1", serialized);
        Assert.Contains("Rows            =   100", serialized);
        Assert.Contains("Cols            =   10", serialized);
        Assert.Contains("AllowUserResizing =   3", serialized);
        Assert.Contains("_ExtentX        =   10231", serialized);
        Assert.Contains("_ExtentY        =   6350", serialized);
    }
}
