using System;
using System.IO;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using VB7x64.IDE.Logging;

namespace VB7x64.IDE.SyntaxHighlighting;

public static class VB6SyntaxHighlighting
{
    private static IHighlightingDefinition? _definition;

    public static IHighlightingDefinition Definition
    {
        get
        {
            if (_definition == null)
            {
                Register();
            }
            return _definition!;
        }
    }

    public static void Register()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "VB7x64.IDE.SyntaxHighlighting.VB6.xshd";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                _definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("VB6", [".bas", ".cls", ".frm"], _definition);
                AppLogger.Info("Resaltado sintáctico de VB6 registrado con éxito desde recurso incrustado.");
                return;
            }

            // Fallback si corre en entorno sin recursos empaquetados
            string localFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyntaxHighlighting", "VB6.xshd");
            if (File.Exists(localFilePath))
            {
                using var fileStream = File.OpenRead(localFilePath);
                using var reader = new XmlTextReader(fileStream);
                _definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("VB6", [".bas", ".cls", ".frm"], _definition);
                AppLogger.Info("Resaltado sintáctico de VB6 registrado desde archivo local.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error al cargar la definición sintáctica de VB6", ex);
        }
    }
}
