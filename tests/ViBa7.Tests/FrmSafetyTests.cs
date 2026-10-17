using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ViBa7.ProjectModel.Documents;
using Xunit;

namespace ViBa7.Tests;

public class FrmSafetyTests : IDisposable
{
    private readonly string _tempDir;

    static FrmSafetyTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public FrmSafetyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ViBa7_FrmTests_" + Guid.NewGuid().ToString("N"));
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
    public void SplitAndCombineFrm_PreservesDesignHeaderByteForByte()
    {
        string frmPath = Path.Combine(_tempDir, "Form1.frm");
        string frxPath = Path.Combine(_tempDir, "Form1.frx");

        string originalFrm = @"VERSION 5.00
Begin VB.Form Form1 
   Caption         =   ""Test Form""
   ClientHeight    =   3195
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   4680
   Icon            =   ""Form1.frx"":0000
   Begin VB.CommandButton Command1 
      Caption         =   ""Click Me""
      Height          =   495
      Left            =   1320
      TabIndex        =   0
      Top             =   1200
      Width           =   1935
   End
End
Attribute VB_Name = ""Form1""
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Private Sub Command1_Click()
    MsgBox ""Original Code""
End Sub
";
        byte[] originalFrxBytes = [0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x20, 0x20, 0x00, 0x00];
        string originalFrxHash = Convert.ToHexString(SHA256.HashData(originalFrxBytes));

        File.WriteAllText(frmPath, originalFrm, Encoding.GetEncoding(1252));
        File.WriteAllBytes(frxPath, originalFrxBytes);

        // 1. Separar secciones
        var split = FrmDocumentHandler.SplitFrmContent(originalFrm);
        Assert.True(split.IsSafelyDelimited);
        Assert.Contains("VERSION 5.00", split.HeaderAndDesign);
        Assert.Contains("Begin VB.CommandButton Command1", split.HeaderAndDesign);
        Assert.Contains("Attribute VB_Name = \"Form1\"", split.HeaderAndDesign);
        Assert.Contains("Private Sub Command1_Click()", split.Code);

        // 2. Editar sólo el código
        string newCode = @"Option Explicit

Private Sub Command1_Click()
    MsgBox ""Código Modificado y Seguro""
End Sub

Private Sub Form_Load()
    Caption = ""Nuevo Título""
End Sub
";
        string combined = FrmDocumentHandler.CombineFrmContent(split.HeaderAndDesign, newCode, NewLineConvention.CrLf);
        File.WriteAllText(frmPath, combined, Encoding.GetEncoding(1252));

        // 3. Volver a leer y verificar que la sección de diseño es idéntica
        var reReadSplit = FrmDocumentHandler.SplitFrmContent(File.ReadAllText(frmPath, Encoding.GetEncoding(1252)));
        Assert.True(reReadSplit.IsSafelyDelimited);
        Assert.Equal(
            split.HeaderAndDesign.Trim().Replace("\r\n", "\n"),
            reReadSplit.HeaderAndDesign.Trim().Replace("\r\n", "\n")
        );
        Assert.Contains("Código Modificado y Seguro", reReadSplit.Code);

        // 4. Regla de oro: el archivo .frx no sufrió ningún cambio
        byte[] afterFrxBytes = File.ReadAllBytes(frxPath);
        string afterFrxHash = Convert.ToHexString(SHA256.HashData(afterFrxBytes));
        Assert.Equal(originalFrxHash, afterFrxHash);
        Assert.Equal(originalFrxBytes, afterFrxBytes);
    }

    [Fact]
    public void SplitFrm_InvalidHierarchy_MarksNotSafelyDelimited()
    {
        string invalidFrm = @"VERSION 5.00
Begin VB.Form Form1
   Caption = ""Unclosed Form""
Attribute VB_Name = ""Form1""
Option Explicit
";
        var split = FrmDocumentHandler.SplitFrmContent(invalidFrm);
        Assert.False(split.IsSafelyDelimited);
        Assert.NotNull(split.WarningMessage);
    }
}
