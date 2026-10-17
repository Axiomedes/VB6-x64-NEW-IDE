using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ViBa7.IDE.Logging;
using ViBa7.ProjectModel.Settings;

namespace ViBa7.IDE.SyntaxHighlighting;

public static class VB6SyntaxHighlighting
{
    private static string? _cachedXshdRaw;
    private static IHighlightingDefinition? _defaultDefinition;

    public static IHighlightingDefinition Definition => _defaultDefinition ??= CreateDefinition(new AppSettings());

    public static IHighlightingDefinition CreateDefinition(AppSettings settings)
    {
        try
        {
            string rawXml = GetRawXshd();
            if (string.IsNullOrWhiteSpace(rawXml))
            {
                return Definition;
            }

            // Sustituir colores configurados por el usuario
            rawXml = ReplaceColorHex(rawXml, "Comment", settings.CommentColor);
            rawXml = ReplaceColorHex(rawXml, "String", settings.StringColor);
            rawXml = ReplaceColorHex(rawXml, "Keyword", settings.KeywordColor);
            rawXml = ReplaceColorHex(rawXml, "MethodName", settings.MethodNameColor);
            rawXml = ReplaceColorHex(rawXml, "Type", settings.TypeColor);
            rawXml = ReplaceColorHex(rawXml, "Constant", settings.ConstantColor);
            rawXml = ReplaceColorHex(rawXml, "Number", settings.NumberColor);

            using var reader = new XmlTextReader(new StringReader(rawXml));
            var customDef = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("VB6", [".bas", ".cls", ".frm", ".ctl", ".pag", ".dob", ".dsr"], customDef);
            return customDef;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error al generar la definición sintáctica personalizada de VB6", ex);
            return _defaultDefinition ?? HighlightingLoader.Load(new XmlTextReader(new StringReader(GetRawXshd())), HighlightingManager.Instance);
        }
    }

    private static string ReplaceColorHex(string xml, string colorName, string newHex)
    {
        if (string.IsNullOrWhiteSpace(newHex)) return xml;
        string pattern = $@"(<Color\s+name=""{colorName}""[^>]*?foreground="")[^""]*("")";
        return Regex.Replace(xml, pattern, $"${{1}}{newHex}${{2}}", RegexOptions.IgnoreCase);
    }

    private static string GetRawXshd()
    {
        if (_cachedXshdRaw != null) return _cachedXshdRaw;

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "ViBa7.IDE.SyntaxHighlighting.VB6.xshd";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var streamReader = new StreamReader(stream);
                _cachedXshdRaw = streamReader.ReadToEnd();
                return _cachedXshdRaw;
            }

            string localFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyntaxHighlighting", "VB6.xshd");
            if (File.Exists(localFilePath))
            {
                _cachedXshdRaw = File.ReadAllText(localFilePath);
                return _cachedXshdRaw;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error al obtener la plantilla XSHD de VB6", ex);
        }

        return string.Empty;
    }

    public static void Register()
    {
        _defaultDefinition = CreateDefinition(new AppSettings());
    }
}
