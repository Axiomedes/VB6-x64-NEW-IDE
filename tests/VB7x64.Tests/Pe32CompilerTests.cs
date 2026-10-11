using System;
using System.IO;
using System.Text;
using VB7x64.ProjectModel.Compilation;
using VB7x64.ProjectModel.Vbp;
using Xunit;

namespace VB7x64.Tests;

public class Pe32CompilerTests
{
    [Fact]
    public void Pe32Builder_GeneratesValidDosAndPeHeaders()
    {
        var builder = new Pe32Builder();
        var secText = new Pe32Section(".text", Pe32Constants.SectionCharacteristicsText);
        secText.WriteBytes(new byte[] { 0x90, 0x90, 0xC3 }); // NOP, NOP, RET
        builder.AddSection(secText);

        byte[] exeBytes = builder.Build();

        Assert.NotNull(exeBytes);
        Assert.True(exeBytes.Length >= 512);

        // 1. Cabecera DOS: "MZ"
        Assert.Equal((byte)'M', exeBytes[0]);
        Assert.Equal((byte)'Z', exeBytes[1]);

        // 2. Offset a PE en 0x3C: 0x80 (128)
        uint peOffset = BitConverter.ToUInt32(exeBytes, 0x3C);
        Assert.Equal(0x80u, peOffset);

        // 3. Firma PE: "PE\0\0"
        Assert.Equal((byte)'P', exeBytes[peOffset]);
        Assert.Equal((byte)'E', exeBytes[peOffset + 1]);
        Assert.Equal(0, exeBytes[peOffset + 2]);
        Assert.Equal(0, exeBytes[peOffset + 3]);

        // 4. COFF Machine: i386 (0x014C)
        ushort machine = BitConverter.ToUInt16(exeBytes, (int)peOffset + 4);
        Assert.Equal(Pe32Constants.MachineI386, machine);

        // 5. Optional Header Magic: PE32 (0x010B)
        ushort optMagic = BitConverter.ToUInt16(exeBytes, (int)peOffset + 24);
        Assert.Equal(Pe32Constants.OptionalHeaderMagicPe32, optMagic);

        // 6. Subsystem: Windows GUI (2)
        ushort subsystem = BitConverter.ToUInt16(exeBytes, (int)peOffset + 92);
        Assert.Equal(Pe32Constants.SubsystemWindowsGui, subsystem);
    }

    [Fact]
    public void Msvbvm60PeBuilder_EmitsThunRTMainImport_And_Vb5Header()
    {
        var builder = new Msvbvm60PeBuilder();
        var options = new Vb6CompileOptions(
            OutputExePath: string.Empty,
            ProjectName: "TestProject",
            ProjectTitle: "Test Title",
            StartupObject: "frmMain"
        );

        byte[] exeBytes = builder.BuildExecutable(options, new[] { "frmMain", "frmAbout" });

        Assert.NotNull(exeBytes);
        Assert.True(exeBytes.Length > 1024);

        string rawAscii = Encoding.ASCII.GetString(exeBytes);

        // 1. Debe importar MSVBVM60.DLL
        Assert.Contains("MSVBVM60.DLL", rawAscii);

        // 2. Debe importar ThunRTMain
        Assert.Contains("ThunRTMain", rawAscii);

        // 3. Debe contener la firma mágica "VB5!" del runtime de VB6
        Assert.Contains("VB5!", rawAscii);

        // 4. Debe contener los nombres de proyecto y formulario
        Assert.Contains("TestProject", rawAscii);
        Assert.Contains("Test Title", rawAscii);
        Assert.Contains("frmMain", rawAscii);
        Assert.Contains("frmAbout", rawAscii);

        // 5. Validar Entry Point x86 Stub (0x68 [PUSH imm32], 0xFF 0x15 [CALL [IAT]])
        // El Entry Point está en PointerToRawData de la primera sección
        // Localizamos el byte de PUSH (0x68) al inicio de .text
        // Buscamos la secuencia: 0x68, ..., 0xFF, 0x15
        int pushIndex = -1;
        for (int i = 0x200; i < 0x600; i++)
        {
            if (exeBytes[i] == 0x68 && exeBytes[i + 5] == 0xFF && exeBytes[i + 6] == 0x15)
            {
                pushIndex = i;
                break;
            }
        }
        Assert.True(pushIndex >= 0, "No se encontró el stub de inicio x86 (push VBHeader; call ThunRTMain)");
    }

    [Fact]
    public void Vb6ProjectCompiler_CompilesProjectToDiskSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "VB7_CompileTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string outExe = Path.Combine(tempDir, "MiApp.exe");

            var project = new VB6Project
            {
                Name = "MiApp",
                Title = "Mi Aplicación VB6",
                Startup = "frmMain",
                ProjectFilePath = Path.Combine(tempDir, "MiApp.vbp"),
                MajorVer = 2,
                MinorVer = 5,
                RevisionVer = 1
            };
            project.Items.Add(new ProjectItem { Name = "frmMain", RelativePath = "frmMain.frm", Type = ProjectItemType.Form });

            var compiler = new Vb6ProjectCompiler();
            var result = compiler.Compile(project, outExe);

            Assert.True(result.Success);
            Assert.True(File.Exists(outExe));
            Assert.True(result.OutputSizeBytes > 1000);
            Assert.Equal(new FileInfo(outExe).Length, result.OutputSizeBytes);

            byte[] fileBytes = File.ReadAllBytes(outExe);
            Assert.Equal((byte)'M', fileBytes[0]);
            Assert.Equal((byte)'Z', fileBytes[1]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Vb6Project_MetadataAndAutoIncrementVer_Preserved()
    {
        var project = new VB6Project
        {
            Name = "MetadataApp",
            Title = "Metadata Application",
            MajorVer = 3,
            MinorVer = 14,
            RevisionVer = 42,
            AutoIncrementVer = true,
            CompanyName = "VB7 Modern Labs",
            FileDescription = "VB6 Native Suite",
            LegalCopyright = "Copyright (C) 2026"
        };

        Assert.Equal(3, project.MajorVer);
        Assert.Equal(14, project.MinorVer);
        Assert.Equal(42, project.RevisionVer);
        Assert.True(project.AutoIncrementVer);
        Assert.Equal("VB7 Modern Labs", project.CompanyName);
        Assert.Equal("VB6 Native Suite", project.FileDescription);
        Assert.Equal("Copyright (C) 2026", project.LegalCopyright);

        // Turn off AutoIncrement
        project.AutoIncrementVer = false;
        Assert.False(project.AutoIncrementVer);
    }
}
