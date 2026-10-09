using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using VB7x64.ProjectModel.Documents;
using Xunit;

namespace VB7x64.Tests;

public class VbHeaderSafetyTests : IDisposable
{
    private readonly string _tempDir;

    static VbHeaderSafetyTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public VbHeaderSafetyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VB7x64_HeaderTests_" + Guid.NewGuid().ToString("N"));
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
    public void BasFile_HeaderIsHiddenAndPreservedByteForByte()
    {
        string basPath = Path.Combine(_tempDir, "Module1.bas");
        string originalBas = "Attribute VB_Name = \"Module1\"\r\nOption Explicit\r\n\r\nPublic Sub Main()\r\n    MsgBox \"Hola\"\r\nEnd Sub\r\n";
        File.WriteAllText(basPath, originalBas, Encoding.GetEncoding(1252));

        // 1. Cargar documento mediante SafeFileStorage
        var doc = SafeFileStorage.LoadDocument(basPath);

        // 2. Verificar que la cabecera está oculta y no visible en el editor
        Assert.True(doc.HasHeader);
        Assert.Contains("Attribute VB_Name = \"Module1\"", doc.Header);
        Assert.DoesNotContain("Attribute VB_Name", doc.Text);
        Assert.StartsWith("Option Explicit", doc.Text.TrimStart());

        // 3. Modificar código de usuario
        doc.Text = "Option Explicit\r\n\r\nPublic Sub Main()\r\n    MsgBox \"Hola Modificado\"\r\nEnd Sub\r\n";

        // 4. Guardar documento
        var saveResult = SafeFileStorage.SaveDocument(doc, createBackup: false);
        Assert.Equal(SaveStatus.Success, saveResult.Status);

        // 5. Verificar contenido en disco
        string savedContent = File.ReadAllText(basPath, Encoding.GetEncoding(1252));
        Assert.StartsWith("Attribute VB_Name = \"Module1\"", savedContent);
        Assert.Contains("MsgBox \"Hola Modificado\"", savedContent);

        // 6. Recargar y verificar que la cabecera técnica sigue oculta
        var reloaded = SafeFileStorage.LoadDocument(basPath);
        Assert.True(reloaded.HasHeader);
        Assert.DoesNotContain("Attribute VB_Name", reloaded.Text);
        Assert.Contains("MsgBox \"Hola Modificado\"", reloaded.Text);
    }

    [Fact]
    public void ClsFile_HeaderIsHiddenAndPreservedByteForByte()
    {
        string clsPath = Path.Combine(_tempDir, "Class1.cls");
        string originalCls = @"VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = ""Class1""
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private m_Value As Long

Public Property Get Value() As Long
    Value = m_Value
End Property
";
        File.WriteAllText(clsPath, originalCls, Encoding.GetEncoding(1252));

        // 1. Cargar documento mediante SafeFileStorage
        var doc = SafeFileStorage.LoadDocument(clsPath);

        // 2. Verificar que VERSION, BEGIN...END y atributos están ocultos para el usuario
        Assert.True(doc.HasHeader);
        Assert.Contains("VERSION 1.0 CLASS", doc.Header);
        Assert.Contains("MultiUse = -1", doc.Header);
        Assert.Contains("Attribute VB_Name = \"Class1\"", doc.Header);
        Assert.Contains("Attribute VB_Exposed = False", doc.Header);

        Assert.DoesNotContain("VERSION 1.0 CLASS", doc.Text);
        Assert.DoesNotContain("BEGIN", doc.Text);
        Assert.DoesNotContain("MultiUse", doc.Text);
        Assert.DoesNotContain("Attribute VB_", doc.Text);
        Assert.StartsWith("Option Explicit", doc.Text.TrimStart());

        // 3. Modificar código del usuario
        doc.Text = @"Option Explicit

Private m_Value As Long

Public Property Get Value() As Long
    Value = m_Value * 2
End Property
";

        // 4. Guardar documento
        var saveResult = SafeFileStorage.SaveDocument(doc, createBackup: false);
        Assert.Equal(SaveStatus.Success, saveResult.Status);

        // 5. Verificar contenido en disco: cabecera completa intacta + nuevo código
        string savedDisk = File.ReadAllText(clsPath, Encoding.GetEncoding(1252));
        Assert.Contains("VERSION 1.0 CLASS", savedDisk);
        Assert.Contains("MultiUse = -1", savedDisk);
        Assert.Contains("Attribute VB_Exposed = False", savedDisk);
        Assert.Contains("Value = m_Value * 2", savedDisk);
    }

    [Fact]
    public void CtlFile_UserControlHeaderIsHiddenAndPreserved()
    {
        string ctlPath = Path.Combine(_tempDir, "MyControl.ctl");
        string originalCtl = @"VERSION 5.00
Begin VB.UserControl MyControl 
   ClientHeight    =   3600
   ClientWidth     =   4800
   Begin VB.TextBox Text1 
      Height          =   495
      Left            =   240
      TabIndex        =   0
      Top             =   240
      Width           =   1215
   End
End
Attribute VB_Name = ""MyControl""
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = True
Option Explicit

Private Sub UserControl_Initialize()
    Text1.Text = ""Listo""
End Sub
";
        File.WriteAllText(ctlPath, originalCtl, Encoding.GetEncoding(1252));

        var doc = SafeFileStorage.LoadDocument(ctlPath);
        Assert.True(doc.HasHeader);
        Assert.Contains("Begin VB.UserControl MyControl", doc.Header);
        Assert.Contains("Begin VB.TextBox Text1", doc.Header);
        Assert.Contains("Attribute VB_Name = \"MyControl\"", doc.Header);

        Assert.DoesNotContain("Begin VB.UserControl", doc.Text);
        Assert.DoesNotContain("Begin VB.TextBox", doc.Text);
        Assert.DoesNotContain("Attribute VB_", doc.Text);
        Assert.StartsWith("Option Explicit", doc.Text.TrimStart());

        // Modificar y guardar
        doc.Text = "Option Explicit\r\n\r\nPrivate Sub UserControl_Initialize()\r\n    Text1.Text = \"Modificado\"\r\nEnd Sub\r\n";
        var saveResult = SafeFileStorage.SaveDocument(doc, createBackup: false);
        Assert.Equal(SaveStatus.Success, saveResult.Status);

        string savedDisk = File.ReadAllText(ctlPath, Encoding.GetEncoding(1252));
        Assert.Contains("Begin VB.UserControl MyControl", savedDisk);
        Assert.Contains("Attribute VB_Exposed = True", savedDisk);
        Assert.Contains("Text1.Text = \"Modificado\"", savedDisk);
    }

    [Fact]
    public void PagAndDobAndDsr_AreRecognizedAsVbCodeFiles()
    {
        Assert.True(VbHeaderHandler.IsVbCodeFile(".bas"));
        Assert.True(VbHeaderHandler.IsVbCodeFile(".cls"));
        Assert.True(VbHeaderHandler.IsVbCodeFile(".frm"));
        Assert.True(VbHeaderHandler.IsVbCodeFile(".ctl"));
        Assert.True(VbHeaderHandler.IsVbCodeFile(".pag"));
        Assert.True(VbHeaderHandler.IsVbCodeFile(".dob"));
        Assert.True(VbHeaderHandler.IsVbCodeFile(".dsr"));
        Assert.False(VbHeaderHandler.IsVbCodeFile(".vbp"));
        Assert.False(VbHeaderHandler.IsVbCodeFile(".txt"));
        Assert.False(VbHeaderHandler.IsVbCodeFile(".frx"));
    }

    [Theory]
    [InlineData(".frx")]
    [InlineData(".ctx")]
    [InlineData(".pgx")]
    [InlineData(".dox")]
    [InlineData(".dsx")]
    [InlineData(".res")]
    public void BinaryResourceFiles_CannotBeOpenedAsText(string ext)
    {
        string binaryPath = Path.Combine(_tempDir, "test" + ext);
        File.WriteAllBytes(binaryPath, [0x00, 0x01, 0x02, 0x03]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            SafeFileStorage.LoadDocument(binaryPath);
        });

        Assert.Contains("recursos binarios", ex.Message);
    }

    [Fact]
    public void ProcedureAttributes_InsideCode_AreNotStrippedAsHeader()
    {
        string basPath = Path.Combine(_tempDir, "WithProcAttr.bas");
        string original = @"Attribute VB_Name = ""WithProcAttr""
Option Explicit

Public Property Get Item(Index As Variant) As Variant
Attribute Item.VB_UserMemId = 0
    Set Item = mCol(Index)
End Property
";
        File.WriteAllText(basPath, original, Encoding.GetEncoding(1252));

        var doc = SafeFileStorage.LoadDocument(basPath);
        Assert.True(doc.HasHeader);
        Assert.Contains("Attribute VB_Name = \"WithProcAttr\"", doc.Header);

        // El atributo a nivel de procedimiento dentro del código debe seguir en el código de usuario
        Assert.Contains("Attribute Item.VB_UserMemId = 0", doc.Text);
        Assert.DoesNotContain("Attribute VB_Name", doc.Text);

        // Guardar sin cambios y verificar que se reconstruye idéntico
        string currentHash = doc.CalculateCurrentContentHash();
        Assert.Equal(doc.OriginalHash, currentHash);
    }

    [Fact]
    public void ClsFile_UnclosedBegin_MarkedAsNotSafelyDelimited()
    {
        string invalidCls = @"VERSION 1.0 CLASS
BEGIN
  MultiUse = -1
Attribute VB_Name = ""BrokenClass""
Option Explicit
";
        var split = VbHeaderHandler.SplitFileContent(invalidCls, ".cls");
        Assert.False(split.IsSafelyDelimited);
        Assert.NotNull(split.WarningMessage);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "VB7x64.slnx")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public void FixtureProjects_AllFilesHaveHeadersHiddenAndAreLosslessRoundtrip()
    {
        string repoRoot = FindRepoRoot();
        string[] fixtureFiles =
        [
            Path.Combine(repoRoot, "tests", "fixtures", "SimpleProject", "Module1.bas"),
            Path.Combine(repoRoot, "tests", "fixtures", "SimpleProject", "Class1.cls"),
            Path.Combine(repoRoot, "tests", "fixtures", "SimpleProject", "Form1.frm"),
            Path.Combine(repoRoot, "tests", "fixtures", "GroupProject", "MainMod.bas"),
            Path.Combine(repoRoot, "tests", "fixtures", "GroupProject", "Customer.cls"),
            Path.Combine(repoRoot, "tests", "fixtures", "GroupProject", "Form1.frm")
        ];

        foreach (string file in fixtureFiles)
        {
            Assert.True(File.Exists(file), $"Fixture file must exist: {file}");
            var doc = SafeFileStorage.LoadDocument(file);

            Assert.True(doc.HasHeader, $"File {Path.GetFileName(file)} should have header extracted");
            Assert.DoesNotContain("Attribute VB_Name", doc.Text);
            Assert.DoesNotContain("VERSION ", doc.Text);
            Assert.DoesNotContain("Begin VB.", doc.Text);

            // Reconstrucción sin modificaciones debe ser idéntica al hash original
            string reconstructedHash = doc.CalculateCurrentContentHash();
            Assert.Equal(doc.OriginalHash, reconstructedHash);
        }
    }
}
