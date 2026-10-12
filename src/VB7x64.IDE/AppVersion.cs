namespace VB7x64.IDE;

/// <summary>
/// Control de versiones centralizado de Visual Basic x64 IDE.
/// Estructura de 3 grupos: [Mayores].[Mejoras/Hitos].[Revisiones/Compilaciones] (00.000.0000).
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// Versión final respecto de cambios grandes e importantes en el proyecto (2 dígitos).
    /// </summary>
    public const string Major = "00";

    /// <summary>
    /// Mejoras implementadas sin renovar demasiado el IDE o su funcionalidad (3 dígitos).
    /// </summary>
    public const string Minor = "001";

    /// <summary>
    /// Correcciones y compilaciones realizadas acumuladas desde el inicio (4 dígitos).
    /// </summary>
    public const string Revision = "0563";

    /// <summary>
    /// Número de versión completo formateado (ej. 00.001.0561).
    /// </summary>
    public static string VersionString => $"{Major}.{Minor}.{Revision}";

    /// <summary>
    /// Título oficial del proyecto.
    /// </summary>
    public static string AppTitle => "Visual Basic x64 IDE";

    /// <summary>
    /// Autoría oficial del proyecto.
    /// </summary>
    public static string Author => "David Rojas [Axio.UK]";

    /// <summary>
    /// Licencia oficial del proyecto.
    /// </summary>
    public const string License = "GNU General Public License v3.0 (GPL-3.0)";
}
