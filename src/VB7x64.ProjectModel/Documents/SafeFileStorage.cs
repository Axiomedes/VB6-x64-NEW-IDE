using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VB7x64.ProjectModel.Documents;

public enum SaveStatus
{
    Success,
    NotModified,
    ReadOnlyFile,
    ExternalConflict,
    Error
}

public record SaveResult(SaveStatus Status, string Message, string? BackupPath = null);

public static class SafeFileStorage
{
    public static EditorDocument LoadDocument(string filePath, Encoding? defaultFallbackEncoding = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"El archivo no existe: {filePath}", filePath);
        }

        // Regla de oro 5: Archivos .frx nunca deben abrirse como texto
        if (string.Equals(Path.GetExtension(filePath), ".frx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Los archivos .frx contienen recursos binarios y no deben abrirse como texto.");
        }

        FileInfo fileInfo = new(filePath);
        byte[] bytes = File.ReadAllBytes(filePath);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));

        var detection = EncodingDetector.Detect(bytes, defaultFallbackEncoding);
        string text = detection.Encoding.GetString(bytes.AsSpan(detection.PreambleLength));

        bool isReadOnly = (fileInfo.Attributes & FileAttributes.ReadOnly) != 0;

        return new EditorDocument(
            filePath: filePath,
            text: text,
            encoding: detection.Encoding,
            hasByteOrderMark: detection.HasBom,
            newLineConvention: detection.NewLineConvention,
            isReadOnly: isReadOnly,
            sha256Hash: hash,
            lastModifiedTimeUtc: fileInfo.LastWriteTimeUtc,
            fileSize: fileInfo.Length
        );
    }

    public static SaveResult SaveDocument(EditorDocument document, bool createBackup = true)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Regla 2.5: No reescribir un archivo que no haya sido modificado
        if (!document.IsDirty)
        {
            return new SaveResult(SaveStatus.NotModified, "El documento no presenta modificaciones pendientes.");
        }

        string filePath = document.FilePath;

        // Regla 2.9: Respetar atributo de solo lectura
        if (File.Exists(filePath))
        {
            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) != 0 || document.IsReadOnly)
            {
                return new SaveResult(SaveStatus.ReadOnlyFile, $"El archivo '{Path.GetFileName(filePath)}' es de solo lectura.");
            }

            // Regla 2.8: Detección de modificaciones externas
            var currentFileInfo = new FileInfo(filePath);
            if (currentFileInfo.LastWriteTimeUtc > document.LastModifiedTimeUtc.AddSeconds(1))
            {
                // Verificar si el hash actual en disco difiere del último hash guardado
                byte[] currentDiskBytes = File.ReadAllBytes(filePath);
                string currentDiskHash = Convert.ToHexString(SHA256.HashData(currentDiskBytes));
                if (!string.Equals(currentDiskHash, document.LastSavedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new SaveResult(SaveStatus.ExternalConflict, $"El archivo '{Path.GetFileName(filePath)}' ha sido modificado externamente por otro programa.");
                }
            }
        }

        string? backupPath = null;
        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        string tempFilePath = Path.Combine(dir, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            // Regla 2.7: Crear copia .bak antes de sobrescribir según configuración
            if (createBackup && File.Exists(filePath))
            {
                backupPath = filePath + ".bak";
                File.Copy(filePath, backupPath, overwrite: true);
            }

            // Preparar contenido normalizando saltos de línea (CRLF/LF)
            string normalizedText = EditorDocument.NormalizeNewLines(document.Text, document.NewLineConvention);
            byte[] contentBytes = document.Encoding.GetBytes(normalizedText);

            // Escribir a archivo temporal
            using (var stream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (document.HasByteOrderMark)
                {
                    byte[] preamble = document.Encoding.GetPreamble();
                    if (preamble.Length > 0)
                    {
                        stream.Write(preamble, 0, preamble.Length);
                    }
                }
                stream.Write(contentBytes, 0, contentBytes.Length);
                stream.Flush(true); // Forzar persistencia en disco
            }

            // Reemplazo atómico / controlado
            if (File.Exists(filePath))
            {
                File.Move(tempFilePath, filePath, overwrite: true);
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }

            // Actualizar metadatos del documento
            var updatedInfo = new FileInfo(filePath);
            byte[] writtenBytes = File.ReadAllBytes(filePath);
            string newHash = Convert.ToHexString(SHA256.HashData(writtenBytes));

            document.MarkSaved(newHash, updatedInfo.LastWriteTimeUtc, updatedInfo.Length);

            return new SaveResult(SaveStatus.Success, "Guardado exitosamente.", backupPath);
        }
        catch (Exception ex)
        {
            // Limpiar archivo temporal si quedó huérfano
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            return new SaveResult(SaveStatus.Error, $"Error al guardar el archivo: {ex.Message}");
        }
    }
}
