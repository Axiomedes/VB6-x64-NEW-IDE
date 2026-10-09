using System;
using System.IO;
using System.Linq;
using System.Text;
using VB7x64.ProjectModel.Vbp;
using Xunit;

namespace VB7x64.Tests;

public class VbpParserTests : IDisposable
{
    private readonly string _tempDir;

    static VbpParserTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public VbpParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VB7x64_VbpTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Parse_ValidProject_ReadsAllComponentsAndProperties()
    {
        string vbpPath = Path.Combine(_tempDir, "TestApp.vbp");
        string modulePath = Path.Combine(_tempDir, "ModMain.bas");
        string classPath = Path.Combine(_tempDir, "ClsCustomer.cls");
        string formPath = Path.Combine(_tempDir, "FrmMain.frm");

        File.WriteAllText(modulePath, "Attribute VB_Name = \"ModMain\"\r\nSub Main()\r\nEnd Sub\r\n");
        File.WriteAllText(classPath, "VERSION 1.0 CLASS\r\nAttribute VB_Name = \"ClsCustomer\"\r\n");
        File.WriteAllText(formPath, "VERSION 5.00\r\nBegin VB.Form FrmMain\r\nEnd\r\n");

        string vbpContent = @"Type=Exe
Reference=*\G{00020430-0000-0000-C000-000000000046}#2.0#0#..\stdole2.tlb#OLE Automation
Module=ModMain; ModMain.bas
Class=ClsCustomer; ClsCustomer.cls
Form=FrmMain.frm
Startup=""FrmMain""
Name=""TestApp""
Title=""Mi Aplicación VB6""
ExeName32=""TestApp.exe""
UnknownCustomKey=SomeValue
";
        File.WriteAllText(vbpPath, vbpContent, Encoding.GetEncoding(1252));

        var project = VbpParser.Parse(vbpPath);

        Assert.Equal("TestApp", project.Name);
        Assert.Equal("Exe", project.ProjectType);
        Assert.Equal("FrmMain", project.Startup);
        Assert.Equal("Mi Aplicación VB6", project.Title);
        Assert.Equal("TestApp.exe", project.ExeName32);

        // Componentes
        Assert.Single(project.Modules);
        Assert.Equal("ModMain", project.Modules.First().Name);
        Assert.True(project.Modules.First().FileExists);

        Assert.Single(project.Classes);
        Assert.Equal("ClsCustomer", project.Classes.First().Name);
        Assert.True(project.Classes.First().FileExists);

        Assert.Single(project.Forms);
        Assert.Equal("FrmMain", project.Forms.First().Name);
        Assert.True(project.Forms.First().FileExists);

        // Referencias
        Assert.Single(project.References);
        Assert.Equal("{00020430-0000-0000-C000-000000000046}", project.References.First().Guid);
        Assert.Equal("OLE Automation", project.References.First().Description);

        // Propiedad desconocida preservada
        Assert.True(project.Settings.ContainsKey("UnknownCustomKey"));
        Assert.Equal("SomeValue", project.Settings["UnknownCustomKey"]);
    }

    [Fact]
    public void Parse_MissingFile_MarksFileExistsFalseWithoutFailing()
    {
        string vbpPath = Path.Combine(_tempDir, "MissingFile.vbp");
        string vbpContent = "Type=Exe\r\nModule=MissingMod; NonExistent.bas\r\nName=\"MissingProj\"\r\n";
        File.WriteAllText(vbpPath, vbpContent, Encoding.GetEncoding(1252));

        var project = VbpParser.Parse(vbpPath);

        Assert.Single(project.Modules);
        var mod = project.Modules.First();
        Assert.Equal("MissingMod", mod.Name);
        Assert.False(mod.FileExists);
    }
}
