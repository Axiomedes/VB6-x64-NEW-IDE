using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ViBa7.ProjectModel.Documents;
using Xunit;

namespace ViBa7.Tests;

public class SafeFileStorageTests : IDisposable
{
    private readonly string _testDir;

    static SafeFileStorageTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public SafeFileStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ViBa7_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                // Limpiar atributos de solo lectura antes de borrar directorio
                foreach (var file in Directory.GetFiles(_testDir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_testDir, true);
            }
            catch { }
        }
    }

    [Fact]
    public void LoadDocument_AnsiWithAccentedCharacters_PreservesContentAndEncoding()
    {
        string filePath = Path.Combine(_testDir, "ModuleAnsi.bas");
        Encoding ansi = Encoding.GetEncoding(1252);
        string originalText = "' Comentario en Español con acentos: áéíóú, ñ, Ñ, ¿¡\r\nSub Saludar()\r\n    MsgBox \"¡Hola, España!\"\r\nEnd Sub\r\n";
        byte[] originalBytes = ansi.GetBytes(originalText);
        File.WriteAllBytes(filePath, originalBytes);

        var doc = SafeFileStorage.LoadDocument(filePath);

        Assert.Equal(originalText, doc.Text);
        Assert.Equal(1252, doc.Encoding.CodePage);
        Assert.False(doc.HasByteOrderMark);
        Assert.Equal(NewLineConvention.CrLf, doc.NewLineConvention);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void LoadDocument_Utf8WithBom_PreservesBomAndEncoding()
    {
        string filePath = Path.Combine(_testDir, "ModuleUtf8Bom.bas");
        string originalText = "Public Sub Test()\r\n    Debug.Print \"UTF-8 with BOM\"\r\nEnd Sub\r\n";
        byte[] contentBytes = Encoding.UTF8.GetBytes(originalText);
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] allBytes = new byte[bom.Length + contentBytes.Length];
        Buffer.BlockCopy(bom, 0, allBytes, 0, bom.Length);
        Buffer.BlockCopy(contentBytes, 0, allBytes, bom.Length, contentBytes.Length);
        File.WriteAllBytes(filePath, allBytes);

        var doc = SafeFileStorage.LoadDocument(filePath);

        Assert.Equal(originalText, doc.Text);
        Assert.True(doc.HasByteOrderMark);
        Assert.Equal(Encoding.UTF8.CodePage, doc.Encoding.CodePage);
    }

    [Fact]
    public void LoadDocument_Utf16Le_PreservesEncoding()
    {
        string filePath = Path.Combine(_testDir, "ModuleUtf16.bas");
        string originalText = "Public Sub Test()\r\nEnd Sub\r\n";
        byte[] allBytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(originalText)).ToArray();
        File.WriteAllBytes(filePath, allBytes);

        var doc = SafeFileStorage.LoadDocument(filePath);

        Assert.Equal(originalText, doc.Text);
        Assert.True(doc.HasByteOrderMark);
        Assert.Equal(Encoding.Unicode.CodePage, doc.Encoding.CodePage);
    }

    [Fact]
    public void LoadDocument_DetectsLfLineEndings()
    {
        string filePath = Path.Combine(_testDir, "ModuleLf.bas");
        string originalText = "Public Sub Test()\n    Dim x As Integer\nEnd Sub\n";
        File.WriteAllText(filePath, originalText, Encoding.GetEncoding(1252));

        var doc = SafeFileStorage.LoadDocument(filePath);

        Assert.Equal(NewLineConvention.Lf, doc.NewLineConvention);
    }

    [Fact]
    public void LoadDocument_ThrowsWhenOpeningFrxAsText()
    {
        string filePath = Path.Combine(_testDir, "Form1.frx");
        File.WriteAllBytes(filePath, new byte[] { 0x00, 0x01, 0xFF, 0xFE });

        Assert.Throws<InvalidOperationException>(() => SafeFileStorage.LoadDocument(filePath));
    }

    [Fact]
    public void SaveDocument_UnmodifiedDocument_DoesNotRewriteFile()
    {
        string filePath = Path.Combine(_testDir, "Untouched.bas");
        byte[] originalBytes = Encoding.GetEncoding(1252).GetBytes("Sub Test()\r\nEnd Sub\r\n");
        File.WriteAllBytes(filePath, originalBytes);
        DateTime originalWriteTime = File.GetLastWriteTimeUtc(filePath);

        var doc = SafeFileStorage.LoadDocument(filePath);
        var result = SafeFileStorage.SaveDocument(doc);

        Assert.Equal(SaveStatus.NotModified, result.Status);
        byte[] afterBytes = File.ReadAllBytes(filePath);
        Assert.Equal(originalBytes, afterBytes);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(filePath));
    }

    [Fact]
    public void SaveDocument_ModifiedDocument_CreatesBackupAndSavesAccurately()
    {
        string filePath = Path.Combine(_testDir, "EditMe.bas");
        Encoding ansi = Encoding.GetEncoding(1252);
        string originalText = "Sub Original()\r\n    ' Código antiguo\r\nEnd Sub\r\n";
        File.WriteAllBytes(filePath, ansi.GetBytes(originalText));

        var doc = SafeFileStorage.LoadDocument(filePath);
        doc.Text = "Sub Modificado()\r\n    ' Código nuevo con acento áéíóú\r\nEnd Sub\r\n";

        var result = SafeFileStorage.SaveDocument(doc, createBackup: true);

        Assert.Equal(SaveStatus.Success, result.Status);
        Assert.False(doc.IsDirty);
        Assert.True(File.Exists(filePath + ".bak"));

        // Comprobar que el backup contiene el texto original
        string backupText = ansi.GetString(File.ReadAllBytes(filePath + ".bak"));
        Assert.Equal(originalText, backupText);

        // Comprobar que el archivo actual contiene el texto modificado
        string savedText = ansi.GetString(File.ReadAllBytes(filePath));
        Assert.Equal(doc.Text, savedText);
    }

    [Fact]
    public void SaveDocument_ReadOnlyFile_ReturnsReadOnlyStatusAndDoesNotModify()
    {
        string filePath = Path.Combine(_testDir, "ReadOnly.bas");
        byte[] originalBytes = Encoding.GetEncoding(1252).GetBytes("Sub ReadOnlySub()\r\nEnd Sub\r\n");
        File.WriteAllBytes(filePath, originalBytes);
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        var doc = SafeFileStorage.LoadDocument(filePath);
        Assert.True(doc.IsReadOnly);

        doc.Text = "Sub Modified()\r\nEnd Sub\r\n";
        var result = SafeFileStorage.SaveDocument(doc);

        Assert.Equal(SaveStatus.ReadOnlyFile, result.Status);
        File.SetAttributes(filePath, FileAttributes.Normal);
        byte[] afterBytes = File.ReadAllBytes(filePath);
        Assert.Equal(originalBytes, afterBytes);
    }

    [Fact]
    public void SaveDocument_ExternalModification_DetectsConflict()
    {
        string filePath = Path.Combine(_testDir, "ExternalConflict.bas");
        File.WriteAllText(filePath, "Original content\r\n", Encoding.GetEncoding(1252));

        var doc = SafeFileStorage.LoadDocument(filePath);

        // Simular modificación externa por otro editor
        File.WriteAllText(filePath, "External edit was made!\r\n", Encoding.GetEncoding(1252));
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));

        doc.Text = "User edit from IDE\r\n";
        var result = SafeFileStorage.SaveDocument(doc);

        Assert.Equal(SaveStatus.ExternalConflict, result.Status);
    }
}
