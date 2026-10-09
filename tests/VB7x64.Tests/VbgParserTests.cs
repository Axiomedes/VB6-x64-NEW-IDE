using System;
using System.IO;
using System.Linq;
using System.Text;
using VB7x64.ProjectModel.Vbp;
using Xunit;

namespace VB7x64.Tests;

public class VbgParserTests : IDisposable
{
    private readonly string _tempDir;

    static VbgParserTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public VbgParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VB7x64_VbgTests_" + Guid.NewGuid().ToString("N"));
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
    public void Parse_ValidProjectGroup_LoadsAllProjectsAndStartupProject()
    {
        string vbgPath = Path.Combine(_tempDir, "Group1.vbg");
        string vbp1Path = Path.Combine(_tempDir, "MainApp.vbp");
        string vbp2Path = Path.Combine(_tempDir, "Component.vbp");

        File.WriteAllText(vbp1Path, "Type=Exe\r\nName=\"MainApp\"\r\nModule=Module1; Mod1.bas\r\n", Encoding.GetEncoding(1252));
        File.WriteAllText(vbp2Path, "Type=OleDll\r\nName=\"ComponentLib\"\r\nClass=Class1; Cls1.cls\r\n", Encoding.GetEncoding(1252));

        string vbgContent = @"VBGROUP 5.0
StartupProject=MainApp.vbp
Project=Component.vbp
";
        File.WriteAllText(vbgPath, vbgContent, Encoding.GetEncoding(1252));

        var group = VbgParser.Parse(vbgPath);

        Assert.Equal("Group1", group.Name);
        Assert.Equal(2, group.Projects.Count);
        Assert.NotNull(group.StartupProject);
        Assert.Equal("MainApp", group.StartupProject.Name);

        var compProj = group.Projects.FirstOrDefault(p => p.Name == "ComponentLib");
        Assert.NotNull(compProj);
        Assert.Single(compProj.Classes);
    }
}
