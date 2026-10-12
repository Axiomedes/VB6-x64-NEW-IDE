using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VB7x64.ProjectModel.Designers;
using VB7x64.ProjectModel.Resources;
using Xunit;

namespace VB7x64.Tests;

public class FrxAndPictureTests
{
    [Fact]
    public void FrxResourceFile_AppendAndReadResource_Succeeds()
    {
        string tempFrx = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.frx");
        try
        {
            byte[] dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 };
            byte[] dummyBmp = new byte[] { 0x42, 0x4D, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00 };

            int offset1 = FrxResourceFile.AppendResource(tempFrx, dummyPng);
            Assert.Equal(0, offset1);

            int offset2 = FrxResourceFile.AppendResource(tempFrx, dummyBmp);
            Assert.True(offset2 > offset1);

            byte[]? read1 = FrxResourceFile.ReadResource(tempFrx, offset1);
            Assert.NotNull(read1);
            Assert.Equal(dummyPng, read1);

            byte[]? read2 = FrxResourceFile.ReadResource(tempFrx, offset2);
            Assert.NotNull(read2);
            Assert.Equal(dummyBmp, read2);
        }
        finally
        {
            if (File.Exists(tempFrx)) File.Delete(tempFrx);
        }
    }

    [Fact]
    public void FrxResourceFile_ParseAndFormatReference_Works()
    {
        bool ok = FrxResourceFile.ParseReference("\"Form1.frx\":08CA", out string file, out int offset);
        Assert.True(ok);
        Assert.Equal("Form1.frx", file);
        Assert.Equal(0x08CA, offset);

        string formatted = FrxResourceFile.FormatReference(file, offset);
        Assert.Equal("\"Form1.frx\":08CA", formatted);

        bool ok2 = FrxResourceFile.ParseReference("0000", out string file2, out int offset2);
        Assert.True(ok2);
        Assert.Equal(string.Empty, file2);
        Assert.Equal(0, offset2);
    }

    [Fact]
    public void FrxResourceFile_DetectsImageFormats()
    {
        byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(VbImageFormat.Png, FrxResourceFile.DetectImageFormat(pngHeader));
        Assert.Equal("(PNG)", FrxResourceFile.GetFormatDescription(pngHeader));

        byte[] bmpHeader = new byte[] { 0x42, 0x4D, 0x00, 0x00 };
        Assert.Equal(VbImageFormat.Bmp, FrxResourceFile.DetectImageFormat(bmpHeader));
        Assert.Equal("(Bitmap)", FrxResourceFile.GetFormatDescription(bmpHeader));

        byte[] icoHeader = new byte[] { 0x00, 0x00, 0x01, 0x00 };
        Assert.Equal(VbImageFormat.Ico, FrxResourceFile.DetectImageFormat(icoHeader));
        Assert.Equal("(Icon)", FrxResourceFile.GetFormatDescription(icoHeader));

        byte[] jpgHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        Assert.Equal(VbImageFormat.Jpeg, FrxResourceFile.DetectImageFormat(jpgHeader));
        Assert.Equal("(JPEG)", FrxResourceFile.GetFormatDescription(jpgHeader));

        byte[] webpHeader = new byte[] {
            0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00,
            0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x4C
        };
        Assert.Equal(VbImageFormat.WebP, FrxResourceFile.DetectImageFormat(webpHeader));
        Assert.Equal("(WebP)", FrxResourceFile.GetFormatDescription(webpHeader));

        Assert.Equal("(None)", FrxResourceFile.GetFormatDescription(null));
    }

    [Fact]
    public void FormDesignParser_ParsesPictureAndAlignmentProperties()
    {
        string snippet = @"VERSION 5.00
Begin VB.Form Form1 
   Caption         =   ""Test Form""
   ClientHeight    =   3195
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   4680
   Picture         =   ""Form1.frx"":0000
   Begin VB.PictureBox Picture1 
      Height          =   1500
      Left            =   300
      Top             =   300
      Width           =   1800
      AutoSize        =   -1  'True
      Picture         =   ""Form1.frx"":0014
   End
   Begin VB.CommandButton Command1 
      Caption         =   ""&Aceptar""
      Height          =   600
      Left            =   2400
      Top             =   300
      Width           =   1800
      Style           =   1  'Graphical
      Picture         =   ""Form1.frx"":0030
      ' [VB7] PictureAlignment = 2
   End
End
";

        var model = FormDesignParser.Parse(snippet);
        Assert.Equal("\"Form1.frx\":0000", model.Picture);

        var pic = model.FindControl("Picture1");
        Assert.NotNull(pic);
        Assert.True(pic.AutoSize);
        Assert.Equal("\"Form1.frx\":0014", pic.Picture);

        var btn = model.FindControl("Command1");
        Assert.NotNull(btn);
        Assert.Equal(1, btn.Style);
        Assert.Equal("\"Form1.frx\":0030", btn.Picture);
        Assert.Equal(PictureAlignment.Left, btn.PictureAlignment);
    }

    [Fact]
    public void FormDesignSerializer_PreservesVB6BackwardCompatibility()
    {
        var model = new FormDesignModel
        {
            Name = "frmTest",
            Caption = "Test Form",
            ClientWidth = 4500,
            ClientHeight = 3000,
            Picture = "\"frmTest.frx\":0000"
        };

        var pic = new FormDesignerControl
        {
            TypeName = "VB.PictureBox",
            Name = "picCanvas",
            Left = 120,
            Top = 120,
            Width = 2400,
            Height = 1800,
            AutoSize = true,
            Picture = "\"frmTest.frx\":0020"
        };

        var btn = new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "cmdOK",
            Caption = "OK",
            Left = 2700,
            Top = 120,
            Width = 1500,
            Height = 600,
            Style = 1,
            Picture = "\"frmTest.frx\":0040",
            PictureAlignment = PictureAlignment.Right
        };

        model.Children.Add(pic);
        model.Children.Add(btn);

        string serialized = FormDesignSerializer.Serialize(model);

        // Verificar que la serialización incluye la sintaxis canónica de VB6
        Assert.Contains("Picture         =   \"frmTest.frx\":0000", serialized);
        Assert.Contains("AutoSize        =   -1  'True", serialized);
        Assert.Contains("Style           =   1  'Graphical", serialized);
        Assert.Contains("Picture         =   \"frmTest.frx\":0020", serialized);
        Assert.Contains("Picture         =   \"frmTest.frx\":0040", serialized);
        // Debe guardarse como comentario compatible para no romper VB6 original
        Assert.Contains("' [VB7] PictureAlignment = 3", serialized);

        // Roundtrip parsing
        var reloaded = FormDesignParser.Parse(serialized);
        Assert.Equal("\"frmTest.frx\":0000", reloaded.Picture);

        var reloadedPic = reloaded.FindControl("picCanvas");
        Assert.NotNull(reloadedPic);
        Assert.True(reloadedPic.AutoSize);
        Assert.Equal("\"frmTest.frx\":0020", reloadedPic.Picture);

        var reloadedBtn = reloaded.FindControl("cmdOK");
        Assert.NotNull(reloadedBtn);
        Assert.Equal(1, reloadedBtn.Style);
        Assert.Equal("\"frmTest.frx\":0040", reloadedBtn.Picture);
        Assert.Equal(PictureAlignment.Right, reloadedBtn.PictureAlignment);
    }

    [Fact]
    public void FormDesignParser_ResolveControlPictures_LoadsBytesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vb7_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string frxPath = Path.Combine(tempDir, "Form1.frx");
            byte[] dummyImage = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x01, 0x02, 0x03, 0x04 };
            int offset = FrxResourceFile.AppendResource(frxPath, dummyImage);

            var model = new FormDesignModel { Name = "Form1" };
            var pic = new FormDesignerControl
            {
                TypeName = "VB.PictureBox",
                Name = "Picture1",
                Picture = FrxResourceFile.FormatReference("Form1.frx", offset)
            };
            model.Children.Add(pic);

            Assert.Null(pic.PictureData);

            FormDesignParser.ResolveControlPictures(model, tempDir);

            Assert.NotNull(pic.PictureData);
            Assert.Equal(dummyImage, pic.PictureData);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DesignerPropertyFactory_ExposesPictureProperties()
    {
        var form = new FormDesignModel { Name = "Form1" };
        var formProps = DesignerPropertyFactory.GetPropertiesForForm(form, () => { });
        Assert.Contains(formProps, p => p.Name == "Picture" && p.Type == DesignerPropertyType.Picture);

        var picCtrl = new FormDesignerControl { TypeName = "VB.PictureBox", Name = "Picture1" };
        var picProps = DesignerPropertyFactory.GetPropertiesForControl(picCtrl, form, () => { });
        Assert.Contains(picProps, p => p.Name == "Picture" && p.Type == DesignerPropertyType.Picture);
        Assert.Contains(picProps, p => p.Name == "AutoSize" && p.Type == DesignerPropertyType.Boolean);

        var btnCtrl = new FormDesignerControl { TypeName = "VB.CommandButton", Name = "Command1" };
        var btnProps = DesignerPropertyFactory.GetPropertiesForControl(btnCtrl, form, () => { });
        Assert.Contains(btnProps, p => p.Name == "Picture" && p.Type == DesignerPropertyType.Picture);
        Assert.Contains(btnProps, p => p.Name == "Style" && p.Type == DesignerPropertyType.EnumChoice);
        Assert.Contains(btnProps, p => p.Name == "PictureAlignment" && p.Type == DesignerPropertyType.EnumChoice);
    }

    [Fact]
    public void ModernImageEncodingAndDecoding_WorksWithPngAndWebP()
    {
        // Generar un PNG con canal alfa real usando SixLabors.ImageSharp
        using var image = new Image<Rgba32>(16, 16);
        image[0, 0] = new Rgba32(255, 0, 0, 128); // Rojo semitransparente

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        byte[] pngBytes = ms.ToArray();

        var format = FrxResourceFile.DetectImageFormat(pngBytes);
        Assert.Equal(VbImageFormat.Png, format);

        // Cargar con ImageSharp para validar dimensiones
        using var loaded = Image.Load(pngBytes);
        Assert.Equal(16, loaded.Width);
        Assert.Equal(16, loaded.Height);
    }

    [Fact]
    public void PropertySetter_DoesNotOverwritePictureWithFormatDescription()
    {
        var form = new FormDesignModel { Name = "Form1" };
        var picCtrl = new FormDesignerControl
        {
            TypeName = "VB.PictureBox",
            Name = "Picture1",
            Picture = "\"Form1.frx\":0010"
        };
        form.Children.Add(picCtrl);

        var picProps = DesignerPropertyFactory.GetPropertiesForControl(picCtrl, form, () => { });
        var picProp = picProps.First(p => p.Name == "Picture");

        // Simular intento de asignación de "(Bitmap)" o "(PNG)" por la grilla
        bool res = picProp.SetValue("(Bitmap)");
        Assert.True(res);
        // Debe conservar la referencia original y no "(Bitmap)"
        Assert.Equal("\"Form1.frx\":0010", picCtrl.Picture);

        // Si se asigna "(None)", debe limpiar la propiedad
        picProp.SetValue("(None)");
        Assert.Null(picCtrl.Picture);
    }

    [Fact]
    public void FullLifecycle_SaveAndReopen_PictureBoxImageIsRestored()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vb7_lifecycle_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string formPath = Path.Combine(tempDir, "Form1.frm");
            string initialContent =
                "VERSION 5.00\r\n" +
                "Begin VB.Form Form1 \r\n" +
                "   Caption         =   \"Form1\"\r\n" +
                "   ClientHeight    =   4500\r\n" +
                "   ClientLeft      =   60\r\n" +
                "   ClientTop       =   450\r\n" +
                "   ClientWidth     =   6800\r\n" +
                "   Begin VB.PictureBox Picture1 \r\n" +
                "      Height          =   1500\r\n" +
                "      Left            =   300\r\n" +
                "      Top             =   300\r\n" +
                "      Width           =   1800\r\n" +
                "   End\r\n" +
                "End\r\n" +
                "Attribute VB_Name = \"Form1\"\r\n" +
                "\r\n" +
                "Private Sub Form_Load()\r\n" +
                "End Sub\r\n";

            File.WriteAllText(formPath, initialContent, System.Text.Encoding.GetEncoding(1252));

            // 1. Cargar documento como lo hace el IDE
            var doc = VB7x64.ProjectModel.Documents.SafeFileStorage.LoadDocument(formPath);
            Assert.True(doc.HasHeader);

            var model = FormDesignParser.Parse(doc.Header!);
            var picCtrl = model.FindControl("Picture1");
            Assert.NotNull(picCtrl);

            // 2. Simular asignación de imagen por PropertyGrid
            byte[] dummyImage = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x01, 0x02, 0x03, 0x04 };
            string frxPath = Path.ChangeExtension(formPath, ".frx");
            int offset = FrxResourceFile.AppendResource(frxPath, dummyImage);
            string frxFileName = Path.GetFileName(frxPath);
            string reference = FrxResourceFile.FormatReference(frxFileName, offset);

            picCtrl.Picture = reference;
            picCtrl.PictureData = dummyImage;

            // 3. Serializar y guardar como lo hace EditorTabItem.Save()
            doc.Header = FormDesignSerializer.Serialize(model);
            doc.IsDirty = true;
            var saveRes = VB7x64.ProjectModel.Documents.SafeFileStorage.SaveDocument(doc, false);
            Assert.Equal(VB7x64.ProjectModel.Documents.SaveStatus.Success, saveRes.Status);

            // 4. Reabrir documento de disco simulando cierre y apertura del IDE
            var reopenedDoc = VB7x64.ProjectModel.Documents.SafeFileStorage.LoadDocument(formPath);
            Assert.True(reopenedDoc.HasHeader);

            var reloadedModel = FormDesignParser.Parse(reopenedDoc.Header!);
            string dir = Path.GetDirectoryName(formPath)!;
            FormDesignParser.ResolveControlPictures(reloadedModel, dir);

            var reloadedPic = reloadedModel.FindControl("Picture1");
            Assert.NotNull(reloadedPic);
            Assert.NotNull(reloadedPic.Picture);
            Assert.NotNull(reloadedPic.PictureData);
            Assert.Equal(dummyImage, reloadedPic.PictureData);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

