using System;
using System.Collections.Generic;

namespace VB7x64.ProjectModel.Execution;

/// <summary>
/// Catálogo de constantes intrínsecas de Visual Basic 6.0
/// (MsgBox, colores, formato, tipos de datos, etc.).
/// </summary>
public static class VbConstants
{
    private static readonly Dictionary<string, VbValue> _constants = new(StringComparer.OrdinalIgnoreCase)
    {
        // MsgBox Botones / Estilos
        ["vbOKOnly"] = VbValue.FromLong(0),
        ["vbOKCancel"] = VbValue.FromLong(1),
        ["vbAbortRetryIgnore"] = VbValue.FromLong(2),
        ["vbYesNoCancel"] = VbValue.FromLong(3),
        ["vbYesNo"] = VbValue.FromLong(4),
        ["vbRetryCancel"] = VbValue.FromLong(5),

        // MsgBox Iconos
        ["vbCritical"] = VbValue.FromLong(16),
        ["vbQuestion"] = VbValue.FromLong(32),
        ["vbExclamation"] = VbValue.FromLong(48),
        ["vbInformation"] = VbValue.FromLong(64),

        // MsgBox Botón predeterminado
        ["vbDefaultButton1"] = VbValue.FromLong(0),
        ["vbDefaultButton2"] = VbValue.FromLong(256),
        ["vbDefaultButton3"] = VbValue.FromLong(512),
        ["vbDefaultButton4"] = VbValue.FromLong(768),

        // MsgBox Modalidad
        ["vbApplicationModal"] = VbValue.FromLong(0),
        ["vbSystemModal"] = VbValue.FromLong(4096),

        // MsgBox Valores de Retorno
        ["vbOK"] = VbValue.FromLong(1),
        ["vbCancel"] = VbValue.FromLong(2),
        ["vbAbort"] = VbValue.FromLong(3),
        ["vbRetry"] = VbValue.FromLong(4),
        ["vbIgnore"] = VbValue.FromLong(5),
        ["vbYes"] = VbValue.FromLong(6),
        ["vbNo"] = VbValue.FromLong(7),

        // Constantes de Caracteres y Cadenas
        ["vbCrLf"] = VbValue.FromString("\r\n"),
        ["vbNewLine"] = VbValue.FromString("\r\n"),
        ["vbCr"] = VbValue.FromString("\r"),
        ["vbLf"] = VbValue.FromString("\n"),
        ["vbTab"] = VbValue.FromString("\t"),
        ["vbBack"] = VbValue.FromString("\b"),
        ["vbNullChar"] = VbValue.FromString("\0"),
        ["vbNullString"] = VbValue.FromString(string.Empty),

        // Constantes de Color
        ["vbBlack"] = VbValue.FromLong(0x000000),
        ["vbRed"] = VbValue.FromLong(0x0000FF),
        ["vbGreen"] = VbValue.FromLong(0x00FF00),
        ["vbYellow"] = VbValue.FromLong(0x00FFFF),
        ["vbBlue"] = VbValue.FromLong(0xFF0000),
        ["vbMagenta"] = VbValue.FromLong(0xFF00FF),
        ["vbCyan"] = VbValue.FromLong(0xFFFF00),
        ["vbWhite"] = VbValue.FromLong(0xFFFFFF),

        // Comparación y Lógica
        ["vbBinaryCompare"] = VbValue.FromLong(0),
        ["vbTextCompare"] = VbValue.FromLong(1),
        ["vbDatabaseCompare"] = VbValue.FromLong(2),
        ["vbTrue"] = VbValue.FromLong(-1),
        ["vbFalse"] = VbValue.FromLong(0),
        ["vbUseDefault"] = VbValue.FromLong(-2),

        // Tipos de datos (VarType)
        ["vbEmpty"] = VbValue.FromLong(0),
        ["vbNull"] = VbValue.FromLong(1),
        ["vbInteger"] = VbValue.FromLong(2),
        ["vbLong"] = VbValue.FromLong(3),
        ["vbSingle"] = VbValue.FromLong(4),
        ["vbDouble"] = VbValue.FromLong(5),
        ["vbCurrency"] = VbValue.FromLong(6),
        ["vbDate"] = VbValue.FromLong(7),
        ["vbString"] = VbValue.FromLong(8),
        ["vbObject"] = VbValue.FromLong(9),
        ["vbError"] = VbValue.FromLong(10),
        ["vbBoolean"] = VbValue.FromLong(11),
        ["vbVariant"] = VbValue.FromLong(12),
        ["vbDataObject"] = VbValue.FromLong(13),
        ["vbDecimal"] = VbValue.FromLong(14),
        ["vbByte"] = VbValue.FromLong(17),
        ["vbArray"] = VbValue.FromLong(8192),

        ["vbObjectError"] = VbValue.FromLong(-2147221504)
    };

    public static bool TryGetConstant(string name, out VbValue value)
    {
        return _constants.TryGetValue(name, out value);
    }
}
