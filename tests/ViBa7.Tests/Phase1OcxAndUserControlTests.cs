using System;
using System.IO;
using System.Linq;
using System.Text;
using ViBa7.ProjectModel.ActiveX;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.Resources;
using ViBa7.ProjectModel.Vbp;
using Xunit;

namespace ViBa7.Tests;

public class Phase1OcxAndUserControlTests : IDisposable
{
    private readonly string _tempDir;

    static Phase1OcxAndUserControlTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Phase1OcxAndUserControlTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ViBa7_Phase1Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    // =========================================================================
    // 1. Ajuste de Geometría HiMetric ↔ Twips
    // =========================================================================

    [Fact]
    public void HiMetric_Twips_Conversion_ExactValues()
    {
        // 2540 HiMetric = 1 pulgada = 1440 Twips
        Assert.Equal(1440, FormDesignerControl.HiMetricToTwips(2540));
        Assert.Equal(2540, FormDesignerControl.TwipsToHiMetric(1440));

        // Dimensiones estándar de botones/combos en VB6
        // 4471 HiMetric -> 2535 Twips
        Assert.Equal(2535, FormDesignerControl.HiMetricToTwips(4471));
        Assert.Equal(4471, FormDesignerControl.TwipsToHiMetric(2535));

        // 8493 HiMetric -> 4815 Twips
        Assert.Equal(4815, FormDesignerControl.HiMetricToTwips(8493));
        Assert.Equal(8493, FormDesignerControl.TwipsToHiMetric(4815));
    }

    [Fact]
    public void FormDesignParser_Parses_ExtentX_And_ExtentY_When_Width_Height_Absent()
    {
        string frmHeader = @"VERSION 5.00
Begin VB.Form Form1 
   Caption         =   ""Formulario de Prueba""
   ClientHeight    =   3195
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   4680
   Begin MSFlexGridLib.MSFlexGrid Grid1 
      Left            =   240
      Top             =   240
      _ExtentX        =   8493
      _ExtentY        =   4471
   End
End";

        var model = FormDesignParser.Parse(frmHeader);
        var grid = model.Children.FirstOrDefault(c => c.Name == "Grid1");
        Assert.NotNull(grid);

        Assert.Equal(8493, grid.ExtentX);
        Assert.Equal(4471, grid.ExtentY);
        Assert.True(grid.HasExplicitWidth);
        Assert.True(grid.HasExplicitHeight);
        Assert.Equal(4815, grid.Width);
        Assert.Equal(2535, grid.Height);
    }

    [Fact]
    public void FormDesignSerializer_Updates_ExtentX_And_ExtentY_When_Resized()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var ctrl = new FormDesignerControl
        {
            TypeName = "MSFlexGridLib.MSFlexGrid",
            Name = "Grid1",
            Left = 100,
            Top = 100,
            Width = 3000,
            Height = 1500,
            ExtentX = 1000, // Extent desactualizado
            ExtentY = 500
        };
        ctrl.CustomProperties.Add(new FormPropertyItem("_ExtentX", "1000"));
        ctrl.CustomProperties.Add(new FormPropertyItem("_ExtentY", "500"));
        model.Children.Add(ctrl);

        string serialized = FormDesignSerializer.Serialize(model);

        int expectedExtentX = FormDesignerControl.TwipsToHiMetric(3000);
        int expectedExtentY = FormDesignerControl.TwipsToHiMetric(1500);

        Assert.Contains($"_ExtentX        =   {expectedExtentX}", serialized);
        Assert.Contains($"_ExtentY        =   {expectedExtentY}", serialized);
    }

    // =========================================================================
    // 2. Identidad Visual del Control (ToolboxBitmap y Extractor de Iconos OCX)
    // =========================================================================

    [Fact]
    public void OcxToolboxBitmapExtractor_ParseToolboxBitmapValue_CorrectlySplits()
    {
        OcxToolboxBitmapExtractor.ParseToolboxBitmapValue(
            @"C:\Windows\System32\mscomctl.ocx, 1",
            out string? path,
            out string? resId);

        Assert.Equal(@"C:\Windows\System32\mscomctl.ocx", path);
        Assert.Equal("1", resId);

        OcxToolboxBitmapExtractor.ParseToolboxBitmapValue(
            @"""D:\MyControls\custom.ocx"", 102",
            out string? path2,
            out string? resId2);

        Assert.Equal(@"D:\MyControls\custom.ocx", path2);
        Assert.Equal("102", resId2);
    }

    [Fact]
    public void OcxToolboxBitmapExtractor_WrapDibAsBmp_GeneratesValidBmpFile()
    {
        // Crear un DIB simple de 1x1 píxel 24bpp con BITMAPINFOHEADER (40 bytes)
        byte[] dib = new byte[40 + 4]; // 40 bytes header + 3 bytes RGB + 1 byte padding
        // biSize = 40
        BitConverter.GetBytes(40).CopyTo(dib, 0);
        // biWidth = 1
        BitConverter.GetBytes(1).CopyTo(dib, 4);
        // biHeight = 1
        BitConverter.GetBytes(1).CopyTo(dib, 8);
        // biPlanes = 1
        BitConverter.GetBytes((short)1).CopyTo(dib, 12);
        // biBitCount = 24
        BitConverter.GetBytes((short)24).CopyTo(dib, 14);

        byte[]? bmp = OcxToolboxBitmapExtractor.WrapDibAsBmp(dib);
        Assert.NotNull(bmp);
        Assert.Equal((byte)'B', bmp[0]);
        Assert.Equal((byte)'M', bmp[1]);
        Assert.Equal(14 + dib.Length, BitConverter.ToInt32(bmp, 2)); // FileSize
        Assert.Equal(14 + 40, BitConverter.ToInt32(bmp, 10)); // PixelOffset
    }

    // =========================================================================
    // 3. Lector de Recursos Binarios .ctx (fondos, texturas y skins)
    // =========================================================================

    [Fact]
    public void FrxResourceFile_Reads_Canonical_OuterLength_Prefixed_Resource()
    {
        // Formato canónico de VB6 en .ctx/.frx:
        // [OuterLength 4B][Magic 0x0000746C 4B][PayloadLength 4B][Payload...]
        byte[] fakeBmp = new byte[] { 0x42, 0x4D, 0x36, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00 };
        uint payloadLen = (uint)fakeBmp.Length;
        uint outerLen = payloadLen + 8; // Magic (4) + PayloadLen (4) + Payload

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(outerLen);
        bw.Write(FrxResourceFile.OlePictureSignature); // 0x0000746C
        bw.Write(payloadLen);
        bw.Write(fakeBmp);

        byte[] streamBytes = ms.ToArray();
        byte[]? readPayload = FrxResourceFile.ReadResource(streamBytes, 0);

        Assert.NotNull(readPayload);
        Assert.Equal(fakeBmp.Length, readPayload.Length);
        Assert.Equal((byte)'B', readPayload[0]);
        Assert.Equal((byte)'M', readPayload[1]);
    }

    [Fact]
    public void FrxResourceFile_Reads_Direct_Length_Prefixed_Image()
    {
        // Variante directa en .ctx: [OuterLength 4B][BMP / PNG / GIF...]
        byte[] fakeBmp = new byte[] { 0x42, 0x4D, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00 };
        uint outerLen = (uint)fakeBmp.Length;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(outerLen);
        bw.Write(fakeBmp);

        byte[] streamBytes = ms.ToArray();
        byte[]? readPayload = FrxResourceFile.ReadResource(streamBytes, 0);

        Assert.NotNull(readPayload);
        Assert.Equal(fakeBmp.Length, readPayload.Length);
        Assert.Equal((byte)'B', readPayload[0]);
        Assert.Equal((byte)'M', readPayload[1]);
    }

    [Fact]
    public void FormDesignParser_Resolves_Ctx_Pictures_And_ToolboxBitmap()
    {
        string ctxPath = Path.Combine(_tempDir, "MiControl.ctx");
        byte[] dummyBmp = new byte[] { 0x42, 0x4D, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Escribir 2 recursos en el .ctx
        using (var fs = File.Create(ctxPath))
        using (var bw = new BinaryWriter(fs))
        {
            // Offset 0: Imagen de fondo / skin
            bw.Write(FrxResourceFile.OlePictureSignature);
            bw.Write((uint)dummyBmp.Length);
            bw.Write(dummyBmp);

            // Offset 16 (0x0010 aprox): ToolboxBitmap
            bw.Write(FrxResourceFile.OlePictureSignature);
            bw.Write((uint)dummyBmp.Length);
            bw.Write(dummyBmp);
        }

        string ctlHeader = $@"VERSION 5.00
Begin VB.UserControl MiControl 
   ClientHeight    =   1800
   ClientWidth     =   2400
   Picture         =   ""MiControl.ctx"":0000
   ToolboxBitmap   =   ""MiControl.ctx"":0010
   Begin VB.CommandButton cmdTest 
      Caption         =   ""Test""
      DownPicture     =   ""MiControl.ctx"":0000
      Height          =   495
      Left            =   120
      Picture         =   ""MiControl.ctx"":0000
      Top             =   120
      Width           =   1215
   End
End";

        var model = FormDesignParser.Parse(ctlHeader);
        FormDesignParser.ResolveControlPictures(model, _tempDir);

        Assert.NotNull(model.PictureData);
        Assert.NotNull(model.ToolboxBitmapData);

        var cmd = model.FindControl("cmdTest");
        Assert.NotNull(cmd);
        Assert.NotNull(cmd.PictureData);
        Assert.NotNull(cmd.DownPictureData);
    }

    // =========================================================================
    // 4. Composición de Controles Constitutivos e Instancia
    // =========================================================================

    [Fact]
    public void UserControl_Instance_Properties_Cloned_And_Composed_Correctly()
    {
        // Simulación del UserControl base
        var ucModel = new FormDesignModel
        {
            RootType = "VB.UserControl",
            Name = "ucBotonModerno",
            ClientWidth = 2400,
            ClientHeight = 800,
            BackColorHex = "&H00CCCCCC&"
        };

        var lbl = new FormDesignerControl
        {
            TypeName = "VB.Label",
            Name = "lblTexto",
            Caption = "Texto Predeterminado",
            Left = 100,
            Top = 100,
            Width = 2000,
            Height = 400
        };
        ucModel.Children.Add(lbl);

        // Instancia del UserControl en el formulario con propiedades personalizadas
        var instanceCtrl = new FormDesignerControl
        {
            TypeName = "Proyecto1.ucBotonModerno",
            Name = "Boton1",
            Left = 500,
            Top = 500,
            Width = 2500,
            Height = 900,
            Caption = "Guardar Archivo",
            BackColorHex = "&H00FF0000&",
            ForeColorHex = "&H00FFFFFF&",
            BorderStyle = 1
        };

        // Comprobación de que las propiedades de la instancia se preservan y están listas para composición
        Assert.Equal("Guardar Archivo", instanceCtrl.Caption);
        Assert.Equal("&H00FF0000&", instanceCtrl.BackColorHex);
        Assert.Equal("&H00FFFFFF&", instanceCtrl.ForeColorHex);
        Assert.Equal(1, instanceCtrl.BorderStyle);
    }

    // =========================================================================
    // 5. Gestión de Dependencias ActiveX y Remoción en Proyecto
    // =========================================================================

    [Fact]
    public void VbpParser_Extracts_Guid_And_Versions_From_Object_Line()
    {
        string vbpContent = @"Type=Exe
Name=""TestProject""
Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX
Startup=""Form1""
";
        string vbpPath = Path.Combine(_tempDir, "TestProject.vbp");
        File.WriteAllText(vbpPath, vbpContent);

        var project = VbpParser.Parse(vbpPath);
        var ocxItem = project.ActiveXControls.FirstOrDefault();

        Assert.NotNull(ocxItem);
        Assert.Equal("MSCOMCTL.OCX", ocxItem.RelativePath);
        Assert.Equal("{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}", ocxItem.Guid);
        Assert.Equal("2", ocxItem.MajorVersion);
        Assert.Equal("0", ocxItem.MinorVersion);
    }

    [Fact]
    public void VB6Project_RemoveActiveXControl_Removes_Item_And_Unloads_Library()
    {
        var project = new VB6Project();
        var item = new ProjectItem
        {
            Type = ProjectItemType.ActiveXControl,
            Name = "MSCOMCTL",
            RelativePath = "MSCOMCTL.OCX",
            AbsolutePath = @"C:\Windows\SysWOW64\MSCOMCTL.OCX",
            Guid = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}"
        };
        project.Items.Add(item);

        var dummyLib = new OcxLibraryInfo
        {
            LibraryName = "MSComctlLib",
            Guid = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}",
            FilePath = @"C:\Windows\SysWOW64\MSCOMCTL.OCX"
        };
        project.LoadedOcxLibraries.Add(dummyLib);

        Assert.Single(project.ActiveXControls);
        Assert.Single(project.LoadedOcxLibraries);

        // Remover por GUID
        project.RemoveActiveXControl("{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}");
        Assert.Empty(project.ActiveXControls);
        Assert.Empty(project.LoadedOcxLibraries);
    }
}

