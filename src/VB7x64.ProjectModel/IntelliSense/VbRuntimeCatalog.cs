using System;
using System.Collections.Generic;
using System.Linq;

namespace VB7x64.ProjectModel.IntelliSense;

/// <summary>
/// Catálogo exhaustivo de símbolos del runtime de Visual Basic 6.0: funciones intrínsecas,
/// constantes del sistema, tipos de datos y palabras clave con metadatos completos de parámetros.
/// </summary>
public static class VbRuntimeCatalog
{
    private static readonly Dictionary<string, VbSymbolItem> _functions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, VbSymbolItem> _constants = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, VbSymbolItem> _dataTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, VbSymbolItem> _keywords = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<VbSymbolItem> _allGlobalSymbols = [];

    static VbRuntimeCatalog()
    {
        InitializeFunctions();
        InitializeConstants();
        InitializeDataTypes();
        InitializeKeywords();

        _allGlobalSymbols.AddRange(_functions.Values);
        _allGlobalSymbols.AddRange(_constants.Values);
        _allGlobalSymbols.AddRange(_dataTypes.Values);
        _allGlobalSymbols.AddRange(_keywords.Values);
    }

    public static VbSymbolItem? FindFunction(string name) =>
        _functions.TryGetValue(name, out var sym) ? sym : null;

    public static VbSymbolItem? FindConstant(string name) =>
        _constants.TryGetValue(name, out var sym) ? sym : null;

    public static VbSymbolItem? FindSymbol(string name)
    {
        if (_functions.TryGetValue(name, out var f)) return f;
        if (_constants.TryGetValue(name, out var c)) return c;
        if (_dataTypes.TryGetValue(name, out var d)) return d;
        if (_keywords.TryGetValue(name, out var k)) return k;
        return null;
    }

    public static IReadOnlyCollection<VbSymbolItem> GetAllFunctions() => _functions.Values;
    public static IReadOnlyCollection<VbSymbolItem> GetAllConstants() => _constants.Values;
    public static IReadOnlyCollection<VbSymbolItem> GetAllDataTypes() => _dataTypes.Values;
    public static IReadOnlyCollection<VbSymbolItem> GetAllKeywords() => _keywords.Values;
    public static IReadOnlyList<VbSymbolItem> GetAllGlobalSymbols() => _allGlobalSymbols;

    private static void AddFunc(string name, string returnType, string description, params (string Name, string Type, bool Opt, string? Def, string Desc)[] parameters)
    {
        var paramList = parameters.Select(p => new VbParameterInfo
        {
            Name = p.Name,
            Type = p.Type,
            IsOptional = p.Opt,
            DefaultValue = p.Def,
            Description = p.Desc
        }).ToList();

        var item = new VbSymbolItem
        {
            Name = name,
            Kind = VbSymbolKind.IntrinsicFunction,
            ReturnType = returnType,
            Description = description,
            Parameters = paramList
        };
        _functions[name] = item;
    }

    private static void AddConst(string name, string type, string description)
    {
        _constants[name] = new VbSymbolItem
        {
            Name = name,
            Kind = VbSymbolKind.Constant,
            ReturnType = type,
            Description = description
        };
    }

    private static void AddType(string name, string description)
    {
        _dataTypes[name] = new VbSymbolItem
        {
            Name = name,
            Kind = VbSymbolKind.DataType,
            Description = description
        };
    }

    private static void AddKeyword(string name, string description)
    {
        _keywords[name] = new VbSymbolItem
        {
            Name = name,
            Kind = VbSymbolKind.Keyword,
            Description = description
        };
    }

    private static void InitializeFunctions()
    {
        // 1. Cadenas
        AddFunc("Mid", "String", "Devuelve una subcadena que contiene un número especificado de caracteres de una cadena.",
            ("String", "String", false, null, "Expresión de cadena de la que se devuelven los caracteres."),
            ("Start", "Long", false, null, "Posición inicial del carácter en la cadena (base 1)."),
            ("Length", "Variant", true, null, "Número de caracteres que se devolverán. Si se omite, se devuelve el resto de la cadena."));

        AddFunc("Left", "String", "Devuelve un número especificado de caracteres desde el lado izquierdo de una cadena.",
            ("String", "String", false, null, "Expresión de cadena de la que se extraen los caracteres del extremo izquierdo."),
            ("Length", "Long", false, null, "Número de caracteres que se devolverán."));

        AddFunc("Right", "String", "Devuelve un número especificado de caracteres desde el lado derecho de una cadena.",
            ("String", "String", false, null, "Expresión de cadena de la que se extraen los caracteres del extremo derecho."),
            ("Length", "Long", false, null, "Número de caracteres que se devolverán."));

        AddFunc("InStr", "Long", "Devuelve la posición de la primera aparición de una cadena dentro de otra.",
            ("Start", "Variant", true, "1", "Posición numérica inicial para la búsqueda."),
            ("String1", "String", false, null, "Expresión de cadena en la que se busca."),
            ("String2", "String", false, null, "Expresión de cadena que se busca."),
            ("Compare", "VbCompareMethod", true, "vbBinaryCompare", "Tipo de comparación de cadenas (vbBinaryCompare o vbTextCompare)."));

        AddFunc("InStrRev", "Long", "Devuelve la posición de una aparición de una cadena dentro de otra, empezando desde el final.",
            ("StringCheck", "String", false, null, "Cadena en la que se realiza la búsqueda."),
            ("StringMatch", "String", false, null, "Cadena que se busca."),
            ("Start", "Long", true, "-1", "Posición inicial empezando desde el final (-1 busca desde el extremo derecho)."),
            ("Compare", "VbCompareMethod", true, "vbBinaryCompare", "Tipo de comparación de cadenas."));

        AddFunc("Len", "Long", "Devuelve el número de caracteres de una cadena o el tamaño en bytes de una variable.",
            ("Expression", "Variant", false, null, "Cadena o variable que se va a medir."));

        AddFunc("Trim", "String", "Devuelve una copia de una cadena sin espacios iniciales ni finales.",
            ("String", "String", false, null, "Expresión de cadena que se desea recortar."));

        AddFunc("LTrim", "String", "Devuelve una copia de una cadena sin espacios iniciales a la izquierda.",
            ("String", "String", false, null, "Expresión de cadena."));

        AddFunc("RTrim", "String", "Devuelve una copia de una cadena sin espacios finales a la derecha.",
            ("String", "String", false, null, "Expresión de cadena."));

        AddFunc("UCase", "String", "Devuelve una cadena convertida a mayúsculas.",
            ("String", "String", false, null, "Expresión de cadena que se convertirá a mayúsculas."));

        AddFunc("LCase", "String", "Devuelve una cadena convertida a minúsculas.",
            ("String", "String", false, null, "Expresión de cadena que se convertirá a minúsculas."));

        AddFunc("Replace", "String", "Reemplaza una subcadena especificada dentro de una cadena con otra subcadena.",
            ("Expression", "String", false, null, "Expresión de cadena que contiene la subcadena que se reemplazará."),
            ("Find", "String", false, null, "Subcadena que se busca."),
            ("Replace", "String", false, null, "Subcadena de sustitución."),
            ("Start", "Long", true, "1", "Posición inicial dentro de Expression para buscar."),
            ("Count", "Long", true, "-1", "Número de sustituciones de subcadenas a realizar (-1 = todas)."),
            ("Compare", "VbCompareMethod", true, "vbBinaryCompare", "Tipo de comparación."));

        AddFunc("Split", "Variant", "Devuelve una matriz unidimensional de base cero que contiene un número especificado de subcadenas.",
            ("Expression", "String", false, null, "Expresión de cadena que contiene subcadenas y delimitadores."),
            ("Delimiter", "String", true, "\" \"", "Carácter delimitador usado para identificar los límites de las subcadenas."),
            ("Limit", "Long", true, "-1", "Número de subcadenas que se van a devolver (-1 = todas)."),
            ("Compare", "VbCompareMethod", true, "vbBinaryCompare", "Tipo de comparación."));

        AddFunc("Join", "String", "Une un número de subcadenas contenidas en una matriz en una única cadena.",
            ("SourceArray", "Variant", false, null, "Matriz unidimensional que contiene las subcadenas a unir."),
            ("Delimiter", "String", true, "\" \"", "Cadena utilizada para separar las subcadenas en la cadena devuelta."));

        AddFunc("StrReverse", "String", "Devuelve una cadena en la que el orden de los caracteres se ha invertido.",
            ("Expression", "String", false, null, "Cadena cuyos caracteres se van a invertir."));

        AddFunc("Format", "String", "Aplica un formato específico a una expresión numérica o de fecha.",
            ("Expression", "Variant", false, null, "Expresión que se va a formatear."),
            ("Format", "String", true, "\"\"", "Instrucción de formato (ej: \"#,##0.00\", \"dd/mm/yyyy\")."),
            ("FirstDayOfWeek", "VbDayOfWeek", true, "vbSunday", "Constante que especifica el primer día de la semana."),
            ("FirstWeekOfYear", "VbFirstWeekOfYear", true, "vbFirstJan1", "Constante que especifica la primera semana del año."));

        AddFunc("Space", "String", "Devuelve una cadena compuesta por el número especificado de espacios.",
            ("Number", "Long", false, null, "Número de espacios que se incluirán en la cadena."));

        AddFunc("String", "String", "Devuelve una cadena repetida con la longitud y carácter especificados.",
            ("Number", "Long", false, null, "Longitud de la cadena devuelta."),
            ("Character", "Variant", false, null, "Código de carácter o expresión de cadena cuyo primer carácter se repetirá."));

        // 2. Conversiones y Tipado
        AddFunc("CInt", "Integer", "Convierte una expresión al tipo de datos Integer (-32,768 a 32,767).",
            ("Expression", "Variant", false, null, "Expresión numérica o de cadena que se convertirá."));

        AddFunc("CLng", "Long", "Convierte una expresión al tipo de datos Long entero de 32 bits.",
            ("Expression", "Variant", false, null, "Expresión numérica o de cadena que se convertirá."));

        AddFunc("CDbl", "Double", "Convierte una expresión al tipo de datos Double de coma flotante de doble precisión.",
            ("Expression", "Variant", false, null, "Expresión numérica o de cadena."));

        AddFunc("CSng", "Single", "Convierte una expresión al tipo de datos Single de precisión simple.",
            ("Expression", "Variant", false, null, "Expresión que se convertirá."));

        AddFunc("CStr", "String", "Convierte una expresión a una cadena de texto String.",
            ("Expression", "Variant", false, null, "Expresión que se convertirá a cadena."));

        AddFunc("CBool", "Boolean", "Convierte una expresión a un valor booleano True o False.",
            ("Expression", "Variant", false, null, "Expresión que se evaluará como booleana."));

        AddFunc("CByte", "Byte", "Convierte una expresión al tipo Byte (0 a 255).",
            ("Expression", "Variant", false, null, "Expresión numérica que se convertirá."));

        AddFunc("CDate", "Date", "Convierte una expresión al tipo de datos Date.",
            ("Expression", "Variant", false, null, "Expresión de fecha u hora válida."));

        AddFunc("CCur", "Currency", "Convierte una expresión al tipo de datos Currency monetario.",
            ("Expression", "Variant", false, null, "Expresión numérica que se convertirá."));

        AddFunc("CVar", "Variant", "Convierte una expresión a un tipo de datos Variant.",
            ("Expression", "Variant", false, null, "Expresión que se convertirá a Variant."));

        AddFunc("Val", "Double", "Devuelve los números contenidos en una cadena como un valor numérico.",
            ("String", "String", false, null, "Cadena que se interpretará numéricamente."));

        AddFunc("Str", "String", "Devuelve una representación de cadena de un número precedida por un espacio para números positivos.",
            ("Number", "Variant", false, null, "Expresión numérica válida."));

        AddFunc("Chr", "String", "Devuelve el carácter asociado con el código de carácter ASCII especificado.",
            ("CharCode", "Long", false, null, "Número entre 0 y 255 que identifica un carácter."));

        AddFunc("Asc", "Integer", "Devuelve el código de carácter ASCII correspondiente a la primera letra de una cadena.",
            ("String", "String", false, null, "Cualquier expresión de cadena válida."));

        AddFunc("Hex", "String", "Devuelve una cadena que representa el valor hexadecimal de un número.",
            ("Number", "Variant", false, null, "Expresión numérica que se convertirá a hexadecimal."));

        AddFunc("Oct", "String", "Devuelve una cadena que representa el valor octal de un número.",
            ("Number", "Variant", false, null, "Expresión numérica que se convertirá a octal."));

        // 3. Matemáticas
        AddFunc("Abs", "Variant", "Devuelve el valor absoluto de un número.",
            ("Number", "Variant", false, null, "Expresión numérica."));

        AddFunc("Sqr", "Double", "Devuelve la raíz cuadrada de un número.",
            ("Number", "Double", false, null, "Expresión numérica mayor o igual que cero."));

        AddFunc("Sin", "Double", "Devuelve el seno de un ángulo especificado en radianes.",
            ("Number", "Double", false, null, "Ángulo en radianes."));

        AddFunc("Cos", "Double", "Devuelve el coseno de un ángulo especificado en radianes.",
            ("Number", "Double", false, null, "Ángulo en radianes."));

        AddFunc("Tan", "Double", "Devuelve la tangente de un ángulo especificado en radianes.",
            ("Number", "Double", false, null, "Ángulo en radianes."));

        AddFunc("Atn", "Double", "Devuelve el arcotangente de un número en radianes.",
            ("Number", "Double", false, null, "Expresión numérica."));

        AddFunc("Exp", "Double", "Devuelve la base del logaritmo natural (e) elevada a una potencia.",
            ("Number", "Double", false, null, "Potencia a la que se eleva e."));

        AddFunc("Log", "Double", "Devuelve el logaritmo natural de un número.",
            ("Number", "Double", false, null, "Expresión numérica mayor que cero."));

        AddFunc("Rnd", "Single", "Devuelve un número pseudoaleatorio menor que 1 pero mayor o igual que 0.",
            ("Number", "Variant", true, "1", "Valor numérico opcional que determina el valor devuelto."));

        AddFunc("Round", "Variant", "Devuelve un número redondeado a un número especificado de posiciones decimales.",
            ("Number", "Variant", false, null, "Expresión numérica que se redondea."),
            ("NumDigitsAfterDecimal", "Long", true, "0", "Número de dígitos después del separador decimal."));

        AddFunc("Int", "Variant", "Devuelve la parte entera de un número (redondea hacia abajo al entero menor).",
            ("Number", "Variant", false, null, "Expresión numérica."));

        AddFunc("Fix", "Variant", "Devuelve la parte entera de un número (trunca la parte fraccionaria hacia cero).",
            ("Number", "Variant", false, null, "Expresión numérica."));

        AddFunc("Sgn", "Integer", "Devuelve un valor Integer que indica el signo de un número (-1, 0, 1).",
            ("Number", "Variant", false, null, "Expresión numérica."));

        // 4. Fechas y Horas
        AddFunc("Now", "Date", "Devuelve la fecha y hora actuales según el reloj del sistema.");
        AddFunc("Date", "Date", "Devuelve la fecha actual del sistema.");
        AddFunc("Time", "Date", "Devuelve la hora actual del sistema.");
        AddFunc("Timer", "Single", "Devuelve el número de segundos transcurridos desde la medianoche.");

        AddFunc("DateAdd", "Date", "Devuelve una fecha a la que se ha agregado un intervalo de tiempo especificado.",
            ("Interval", "String", false, null, "Intervalo de tiempo que se desea agregar (ej: \"yyyy\", \"m\", \"d\", \"h\")."),
            ("Number", "Double", false, null, "Número de intervalos que se desean agregar."),
            ("Date", "Date", false, null, "Fecha a la que se agrega el intervalo."));

        AddFunc("DateDiff", "Long", "Devuelve el número de intervalos de tiempo entre dos fechas especificadas.",
            ("Interval", "String", false, null, "Intervalo de tiempo que se utiliza para calcular la diferencia."),
            ("Date1", "Date", false, null, "Primera fecha."),
            ("Date2", "Date", false, null, "Segunda fecha."),
            ("FirstDayOfWeek", "VbDayOfWeek", true, "vbSunday", "Primer día de la semana."),
            ("FirstWeekOfYear", "VbFirstWeekOfYear", true, "vbFirstJan1", "Primera semana del año."));

        AddFunc("DatePart", "Variant", "Devuelve la parte especificada de una fecha determinada.",
            ("Interval", "String", false, null, "Intervalo que se desea extraer."),
            ("Date", "Date", false, null, "Fecha que se evalúa."));

        AddFunc("DateSerial", "Date", "Devuelve un valor de tipo Date para el año, mes y día especificados.",
            ("Year", "Integer", false, null, "Año especificado."),
            ("Month", "Integer", false, null, "Mes del año."),
            ("Day", "Integer", false, null, "Día del mes."));

        AddFunc("Day", "Integer", "Devuelve un número entero entre 1 y 31 que representa el día del mes.",
            ("Date", "Date", false, null, "Expresión de fecha válida."));

        AddFunc("Month", "Integer", "Devuelve un número entero entre 1 y 12 que representa el mes del año.",
            ("Date", "Date", false, null, "Expresión de fecha válida."));

        AddFunc("Year", "Integer", "Devuelve un número entero que representa el año de una fecha.",
            ("Date", "Date", false, null, "Expresión de fecha válida."));

        AddFunc("Hour", "Integer", "Devuelve un número entero entre 0 y 23 que representa la hora del día.",
            ("Time", "Date", false, null, "Expresión de hora válida."));

        AddFunc("Minute", "Integer", "Devuelve un número entero entre 0 y 59 que representa el minuto de la hora.",
            ("Time", "Date", false, null, "Expresión de hora válida."));

        AddFunc("Second", "Integer", "Devuelve un número entero entre 0 y 59 que representa el segundo del minuto.",
            ("Time", "Date", false, null, "Expresión de hora válida."));

        // 5. Interacción y Sistema
        AddFunc("MsgBox", "VbMsgBoxResult", "Muestra un mensaje en un cuadro de diálogo y espera que el usuario presione un botón.",
            ("Prompt", "String", false, null, "Expresión de cadena que se muestra como mensaje."),
            ("Buttons", "VbMsgBoxStyle", true, "vbOKOnly", "Suma de valores numéricos que especifican los botones e icono a mostrar."),
            ("Title", "String", true, "\"\"", "Título del cuadro de diálogo."),
            ("HelpFile", "String", true, "\"\"", "Archivo de ayuda."),
            ("Context", "Long", true, "0", "Número de contexto de la ayuda."));

        AddFunc("InputBox", "String", "Muestra un mensaje solicitando al usuario que ingrese texto en un cuadro de diálogo.",
            ("Prompt", "String", false, null, "Mensaje que se muestra en el cuadro de diálogo."),
            ("Title", "String", true, "\"\"", "Título del cuadro de diálogo."),
            ("Default", "String", true, "\"\"", "Respuesta predeterminada mostrada en el cuadro de texto."),
            ("XPos", "Variant", true, null, "Posición horizontal del cuadro de diálogo en twips."),
            ("YPos", "Variant", true, null, "Posición vertical del cuadro de diálogo en twips."));

        AddFunc("DoEvents", "Integer", "Cede el control de la ejecución al sistema operativo para procesar otros eventos.");

        AddFunc("Shell", "Double", "Ejecuta un programa ejecutable externo y devuelve el identificador de tarea.",
            ("PathName", "String", false, null, "Nombre del programa que se ejecutará y cualquier argumento requerido."),
            ("WindowStyle", "VbAppWinStyle", true, "vbNormalFocus", "Estilo de la ventana del programa que se va a ejecutar."));

        AddFunc("Beep", "Void", "Emite un tono audible por el altavoz de la computadora.");

        AddFunc("Environ", "String", "Devuelve el valor de una variable de entorno del sistema operativo.",
            ("EnvString", "Variant", false, null, "Nombre de la variable de entorno o su índice numérico."));

        // 6. Comprobaciones
        AddFunc("IsNumeric", "Boolean", "Devuelve True si la expresión especificada se puede evaluar como un número.",
            ("Expression", "Variant", false, null, "Expresión que se va a comprobar."));

        AddFunc("IsDate", "Boolean", "Devuelve True si la expresión se puede convertir a una fecha válida.",
            ("Expression", "Variant", false, null, "Expresión que se va a comprobar."));

        AddFunc("IsEmpty", "Boolean", "Devuelve True si una variable de tipo Variant no se ha inicializado.",
            ("Expression", "Variant", false, null, "Variable de tipo Variant que se va a comprobar."));

        AddFunc("IsNull", "Boolean", "Devuelve True si una expresión contiene un valor Null (datos no válidos).",
            ("Expression", "Variant", false, null, "Expresión que se va a comprobar."));

        AddFunc("IsArray", "Boolean", "Devuelve True si una variable es una matriz.",
            ("VarName", "Variant", false, null, "Nombre de variable que se va a comprobar."));

        AddFunc("IsObject", "Boolean", "Devuelve True si una expresión hace referencia a un objeto válido.",
            ("Expression", "Variant", false, null, "Expresión que se va a comprobar."));

        AddFunc("TypeName", "String", "Devuelve el nombre del tipo de datos de una variable u objeto.",
            ("VarName", "Variant", false, null, "Variable o expresión cuyo tipo se desea obtener."));

        AddFunc("VarType", "VbVarType", "Devuelve un valor entero que indica el subtipo de una variable Variant.",
            ("VarName", "Variant", false, null, "Variable cuyo subtipo se comprueba."));
    }

    private static void InitializeConstants()
    {
        // Mensajes y MsgBox
        AddConst("vbCrLf", "String", "Combinación de retorno de carro y salto de línea (Chr(13) + Chr(10)).");
        AddConst("vbNewLine", "String", "Carácter de nueva línea específico de la plataforma.");
        AddConst("vbNullString", "String", "Cadena con valor cero (utilizada para llamadas a la API de Windows).");
        AddConst("vbNullChar", "String", "Carácter nulo Chr(0).");
        AddConst("vbTab", "String", "Carácter de tabulación Chr(9).");
        AddConst("vbBack", "String", "Carácter de retroceso Chr(8).");

        AddConst("vbOKOnly", "VbMsgBoxStyle", "Muestra únicamente el botón Aceptar (0).");
        AddConst("vbOKCancel", "VbMsgBoxStyle", "Muestra los botones Aceptar y Cancelar (1).");
        AddConst("vbAbortRetryIgnore", "VbMsgBoxStyle", "Muestra los botones Anular, Reintentar e Ignorar (2).");
        AddConst("vbYesNoCancel", "VbMsgBoxStyle", "Muestra los botones Sí, No y Cancelar (3).");
        AddConst("vbYesNo", "VbMsgBoxStyle", "Muestra los botones Sí y No (4).");
        AddConst("vbRetryCancel", "VbMsgBoxStyle", "Muestra los botones Reintentar y Cancelar (5).");

        AddConst("vbCritical", "VbMsgBoxStyle", "Muestra el icono de mensaje crítico (16).");
        AddConst("vbQuestion", "VbMsgBoxStyle", "Muestra el icono de consulta o interrogación (32).");
        AddConst("vbExclamation", "VbMsgBoxStyle", "Muestra el icono de advertencia o exclamación (48).");
        AddConst("vbInformation", "VbMsgBoxStyle", "Muestra el icono de información (64).");

        AddConst("vbDefaultButton1", "VbMsgBoxStyle", "El primer botón es el predeterminado (0).");
        AddConst("vbDefaultButton2", "VbMsgBoxStyle", "El segundo botón es el predeterminado (256).");
        AddConst("vbDefaultButton3", "VbMsgBoxStyle", "El tercer botón es el predeterminado (512).");

        AddConst("vbOK", "VbMsgBoxResult", "Se presionó el botón Aceptar (1).");
        AddConst("vbCancel", "VbMsgBoxResult", "Se presionó el botón Cancelar (2).");
        AddConst("vbAbort", "VbMsgBoxResult", "Se presionó el botón Anular (3).");
        AddConst("vbRetry", "VbMsgBoxResult", "Se presionó el botón Reintentar (4).");
        AddConst("vbIgnore", "VbMsgBoxResult", "Se presionó el botón Ignorar (5).");
        AddConst("vbYes", "VbMsgBoxResult", "Se presionó el botón Sí (6).");
        AddConst("vbNo", "VbMsgBoxResult", "Se presionó el botón No (7).");

        // Colores
        AddConst("vbBlack", "Long", "Color negro RGB(&H00, &H00, &H00) = &H00000000&.");
        AddConst("vbRed", "Long", "Color rojo RGB(&HFF, &H00, &H00) = &H000000FF&.");
        AddConst("vbGreen", "Long", "Color verde RGB(&H00, &HFF, &H00) = &H0000FF00&.");
        AddConst("vbYellow", "Long", "Color amarillo RGB(&HFF, &HFF, &H00) = &H0000FFFF&.");
        AddConst("vbBlue", "Long", "Color azul RGB(&H00, &H00, &HFF) = &H00FF0000&.");
        AddConst("vbMagenta", "Long", "Color magenta RGB(&HFF, &H00, &HFF) = &H00FF00FF&.");
        AddConst("vbCyan", "Long", "Color cian RGB(&H00, &HFF, &HFF) = &H00FFFF00&.");
        AddConst("vbWhite", "Long", "Color blanco RGB(&HFF, &HFF, &HFF) = &H00FFFFFF&.");

        // Modos de comparación
        AddConst("vbBinaryCompare", "VbCompareMethod", "Realiza una comparación binaria exacta de cadenas (0).");
        AddConst("vbTextCompare", "VbCompareMethod", "Realiza una comparación textual sin distinción de mayúsculas (1).");
    }

    private static void InitializeDataTypes()
    {
        AddType("Boolean", "Tipo de datos que almacena True o False (2 bytes).");
        AddType("Byte", "Tipo de datos numérico entero sin signo de 0 a 255 (1 byte).");
        AddType("Integer", "Tipo de datos entero con signo de 16 bits (-32,768 a 32,767).");
        AddType("Long", "Tipo de datos entero con signo de 32 bits (-2,147,483,648 a 2,147,483,647).");
        AddType("Single", "Tipo de datos de coma flotante de precisión simple (4 bytes).");
        AddType("Double", "Tipo de datos de coma flotante de doble precisión (8 bytes).");
        AddType("Currency", "Tipo de datos numérico de punto fijo para valores monetarios exactos (8 bytes).");
        AddType("Date", "Tipo de datos para fechas y horas desde el año 100 al 9999 (8 bytes).");
        AddType("String", "Tipo de datos para cadenas de caracteres de longitud variable o fija.");
        AddType("Object", "Tipo de datos que contiene una referencia a cualquier objeto COM o formulario.");
        AddType("Variant", "Tipo de datos genérico de VB6 capaz de contener cualquier tipo de datos (16 bytes).");
    }

    private static void InitializeKeywords()
    {
        AddKeyword("Dim", "Declara variables y asigna espacio de almacenamiento.");
        AddKeyword("ReDim", "Asigna o reasigna dinámicamente el tamaño de una matriz en tiempo de ejecución.");
        AddKeyword("Set", "Asigna una referencia a un objeto a una variable o propiedad.");
        AddKeyword("Let", "Asigna el valor de una expresión a una variable o propiedad.");
        AddKeyword("Sub", "Inicia la definición de un procedimiento Sub que ejecuta código sin devolver un valor.");
        AddKeyword("Function", "Inicia la definición de un procedimiento Function que ejecuta código y devuelve un valor.");
        AddKeyword("Property Get", "Declara un procedimiento de propiedad para recuperar el valor de una propiedad.");
        AddKeyword("Property Let", "Declara un procedimiento de propiedad para asignar un valor escalar a una propiedad.");
        AddKeyword("Property Set", "Declara un procedimiento de propiedad para asignar una referencia de objeto a una propiedad.");
        AddKeyword("If", "Evalúa condicionalmente una expresión y ejecuta un bloque de instrucciones.");
        AddKeyword("Then", "Introduce el bloque de instrucciones a ejecutar si la condición If es verdadera.");
        AddKeyword("Else", "Introduce el bloque alternativo si la condición If previa es falsa.");
        AddKeyword("ElseIf", "Evalúa una condición alternativa dentro de una estructura If.");
        AddKeyword("End If", "Cierra una estructura condicional If...Then...Else.");
        AddKeyword("Select Case", "Ejecuta uno de varios grupos de instrucciones basándose en el valor de una expresión.");
        AddKeyword("Case", "Define un caso condicional dentro de una estructura Select Case.");
        AddKeyword("End Select", "Cierra una estructura Select Case.");
        AddKeyword("For", "Inicia un bucle que se repite un número especificado de veces.");
        AddKeyword("To", "Especifica el límite superior en un bucle For.");
        AddKeyword("Step", "Especifica el incremento o decremento del contador en un bucle For.");
        AddKeyword("Next", "Avanza al siguiente ciclo en un bucle For.");
        AddKeyword("For Each", "Repite un bloque de instrucciones para cada elemento de una matriz o colección.");
        AddKeyword("In", "Especifica la colección o matriz en un bucle For Each.");
        AddKeyword("Do", "Inicia un bucle Do While o Do Until.");
        AddKeyword("While", "Ejecuta un bucle mientras una condición sea verdadera.");
        AddKeyword("Until", "Ejecuta un bucle hasta que una condición sea verdadera.");
        AddKeyword("Loop", "Cierra un bucle Do.");
        AddKeyword("Wend", "Cierra un bucle While.");
        AddKeyword("With", "Ejecuta una serie de instrucciones sobre un único objeto o estructura.");
        AddKeyword("End With", "Cierra un bloque With.");
        AddKeyword("Exit Sub", "Sale inmediatamente de un procedimiento Sub.");
        AddKeyword("Exit Function", "Sale inmediatamente de un procedimiento Function.");
        AddKeyword("Exit For", "Sale inmediatamente de un bucle For.");
        AddKeyword("Exit Do", "Sale inmediatamente de un bucle Do.");
        AddKeyword("On Error GoTo", "Habilita una rutina de control de errores y especifica la etiqueta de salto.");
        AddKeyword("On Error Resume Next", "Ignora el error ocurrido y continúa la ejecución en la línea siguiente.");
        AddKeyword("Resume", "Reanuda la ejecución del código tras completarse el control de un error.");
        AddKeyword("Option Explicit", "Obliga a declarar explícitamente todas las variables antes de utilizarlas.");
        AddKeyword("Option Base", "Declara el límite inferior predeterminado para las dimensiones de las matrices.");
        AddKeyword("Option Compare", "Declara el método de comparación de cadenas predeterminado (Binary o Text).");
        AddKeyword("Private", "Declara variables, constantes o procedimientos accesibles solo dentro de su propio módulo.");
        AddKeyword("Public", "Declara variables, constantes o procedimientos accesibles desde todos los módulos del proyecto.");
        AddKeyword("Friend", "Declara procedimientos accesibles dentro de todo el proyecto pero no fuera del componente COM.");
        AddKeyword("Static", "Declara variables locales que preservan su valor entre llamadas sucesivas al procedimiento.");
        AddKeyword("Const", "Declara constantes para representar valores literales inmutables.");
        AddKeyword("Type", "Define un tipo de datos definido por el usuario (estructura UDT).");
        AddKeyword("Enum", "Declara un tipo de datos de enumeración.");
        AddKeyword("Event", "Declara un evento definido por el usuario en una clase o control.");
        AddKeyword("RaiseEvent", "Desencadena un evento declarado a nivel de módulo.");
        AddKeyword("Call", "Transfiere el control a un procedimiento Sub o Function.");
        AddKeyword("New", "Crea una nueva instancia de un objeto o clase.");
        AddKeyword("Nothing", "Desasocia una variable de objeto de cualquier instancia real de objeto.");
        AddKeyword("True", "Valor booleano verdadero (-1 en VB6).");
        AddKeyword("False", "Valor booleano falso (0 en VB6).");
    }
}
