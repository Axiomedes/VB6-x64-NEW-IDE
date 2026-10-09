using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VB7x64.ProjectModel.Documents;

public class EditorDocument
{
    private string _text = string.Empty;
    private bool _isDirty;

    public string FilePath { get; set; }
    public string DisplayName { get; set; }
    public Encoding Encoding { get; set; }
    public bool HasByteOrderMark { get; set; }
    public NewLineConvention NewLineConvention { get; set; }
    public bool IsReadOnly { get; set; }
    public string OriginalHash { get; private set; }
    public string LastSavedHash { get; private set; }
    public DateTime LastModifiedTimeUtc { get; private set; }
    public long FileSize { get; private set; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                _isDirty = true;
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => _isDirty = value;
    }

    public EditorDocument(
        string filePath,
        string text,
        Encoding encoding,
        bool hasByteOrderMark,
        NewLineConvention newLineConvention,
        bool isReadOnly,
        string sha256Hash,
        DateTime lastModifiedTimeUtc,
        long fileSize)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileName(filePath);
        _text = text;
        Encoding = encoding;
        HasByteOrderMark = hasByteOrderMark;
        NewLineConvention = newLineConvention;
        IsReadOnly = isReadOnly;
        OriginalHash = sha256Hash;
        LastSavedHash = sha256Hash;
        LastModifiedTimeUtc = lastModifiedTimeUtc;
        FileSize = fileSize;
        _isDirty = false;
    }

    public void MarkSaved(string newSha256Hash, DateTime lastModifiedUtc, long newFileSize)
    {
        LastSavedHash = newSha256Hash;
        LastModifiedTimeUtc = lastModifiedUtc;
        FileSize = newFileSize;
        _isDirty = false;
    }

    public string CalculateCurrentContentHash()
    {
        string normalized = NormalizeNewLines(_text, NewLineConvention);
        byte[] bytes;

        if (HasByteOrderMark && Encoding is UTF8Encoding)
        {
            byte[] preamble = Encoding.GetPreamble();
            byte[] content = Encoding.GetBytes(normalized);
            bytes = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);
        }
        else
        {
            bytes = Encoding.GetBytes(normalized);
        }

        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static string NormalizeNewLines(string text, NewLineConvention convention)
    {
        string targetEnding = convention switch
        {
            NewLineConvention.CrLf => "\r\n",
            NewLineConvention.Lf => "\n",
            NewLineConvention.Cr => "\r",
            _ => "\r\n"
        };

        // Normalizar primero a \n y luego al destino
        string unified = text.Replace("\r\n", "\n").Replace("\r", "\n");
        if (targetEnding == "\n") return unified;
        return unified.Replace("\n", targetEnding);
    }
}
