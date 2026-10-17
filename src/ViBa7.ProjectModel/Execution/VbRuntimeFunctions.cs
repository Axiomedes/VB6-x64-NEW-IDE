using System;
using System.Globalization;

namespace ViBa7.ProjectModel.Execution;

/// <summary>
/// Implementación de funciones intrínsecas del runtime de Visual Basic 6.0
/// invocables directamente por el motor de ejecución en caliente.
/// </summary>
public static class VbRuntimeFunctions
{
    private static readonly Random _random = new();

    #region Funciones de Cadena

    public static VbValue Len(VbValue val) => VbValue.FromLong(val.AsString().Length);

    public static VbValue Mid(VbValue str, VbValue start, VbValue? length = null)
    {
        string s = str.AsString();
        int st = Math.Max(1, start.AsLong()) - 1;
        if (st >= s.Length) return VbValue.FromString(string.Empty);

        if (length == null || length.Value.IsEmpty)
        {
            return VbValue.FromString(s[st..]);
        }

        int len = Math.Max(0, length.Value.AsLong());
        int available = Math.Min(len, s.Length - st);
        return VbValue.FromString(s.Substring(st, available));
    }

    public static VbValue Left(VbValue str, VbValue length)
    {
        string s = str.AsString();
        int len = Math.Max(0, Math.Min(length.AsLong(), s.Length));
        return VbValue.FromString(s[..len]);
    }

    public static VbValue Right(VbValue str, VbValue length)
    {
        string s = str.AsString();
        int len = Math.Max(0, Math.Min(length.AsLong(), s.Length));
        return VbValue.FromString(s.Substring(s.Length - len, len));
    }

    public static VbValue InStr(VbValue arg1, VbValue arg2, VbValue? arg3 = null)
    {
        // Firma: InStr([start], string1, string2)
        int start = 1;
        string s1, s2;

        if (arg3 != null && !arg3.Value.IsEmpty)
        {
            start = Math.Max(1, arg1.AsLong());
            s1 = arg2.AsString();
            s2 = arg3.Value.AsString();
        }
        else
        {
            s1 = arg1.AsString();
            s2 = arg2.AsString();
        }

        if (string.IsNullOrEmpty(s2)) return VbValue.FromLong(start);
        if (string.IsNullOrEmpty(s1) || start > s1.Length) return VbValue.FromLong(0);

        int idx = s1.IndexOf(s2, start - 1, StringComparison.OrdinalIgnoreCase);
        return VbValue.FromLong(idx >= 0 ? idx + 1 : 0);
    }

    public static VbValue UCase(VbValue val) => VbValue.FromString(val.AsString().ToUpperInvariant());
    public static VbValue LCase(VbValue val) => VbValue.FromString(val.AsString().ToLowerInvariant());

    public static VbValue Trim(VbValue val) => VbValue.FromString(val.AsString().Trim());
    public static VbValue LTrim(VbValue val) => VbValue.FromString(val.AsString().TrimStart());
    public static VbValue RTrim(VbValue val) => VbValue.FromString(val.AsString().TrimEnd());

    public static VbValue Space(VbValue count) => VbValue.FromString(new string(' ', Math.Max(0, count.AsLong())));

    public static VbValue Chr(VbValue charCode) => VbValue.FromString(((char)charCode.AsLong()).ToString());

    public static VbValue Asc(VbValue val)
    {
        string s = val.AsString();
        return s.Length > 0 ? VbValue.FromInteger((short)s[0]) : VbValue.FromInteger(0);
    }

    public static VbValue Replace(VbValue expression, VbValue find, VbValue replaceWith)
    {
        return VbValue.FromString(expression.AsString().Replace(find.AsString(), replaceWith.AsString(), StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Funciones de Conversión y Comprobación

    public static VbValue Val(VbValue val)
    {
        string s = val.AsString().Trim();
        if (string.IsNullOrEmpty(s)) return VbValue.FromDouble(0.0);

        int end = 0;
        bool hasDot = false;
        if (end < s.Length && (s[end] == '+' || s[end] == '-')) end++;
        while (end < s.Length)
        {
            char c = s[end];
            if (char.IsDigit(c)) { end++; continue; }
            if (c == '.' && !hasDot) { hasDot = true; end++; continue; }
            break;
        }

        if (end == 0) return VbValue.FromDouble(0.0);
        string sub = s[..end];
        if (double.TryParse(sub, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
        {
            return VbValue.FromDouble(d);
        }
        return VbValue.FromDouble(0.0);
    }

    public static VbValue Str(VbValue val)
    {
        double d = val.AsDouble();
        return VbValue.FromString(d >= 0 ? " " + d.ToString(CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture));
    }

    public static VbValue CInt(VbValue val) => VbValue.FromInteger((short)val.AsLong());
    public static VbValue CLng(VbValue val) => VbValue.FromLong(val.AsLong());
    public static VbValue CSng(VbValue val) => VbValue.FromSingle((float)val.AsDouble());
    public static VbValue CDbl(VbValue val) => VbValue.FromDouble(val.AsDouble());
    public static VbValue CBool(VbValue val) => VbValue.FromBoolean(val.AsBoolean());
    public static VbValue CStr(VbValue val) => VbValue.FromString(val.AsString());
    public static VbValue CDate(VbValue val) => VbValue.FromDate(val.AsDate());

    public static VbValue IsNumeric(VbValue val) => VbValue.FromBoolean(val.IsNumeric);
    public static VbValue IsDate(VbValue val) => VbValue.FromBoolean(val.VarType == VbVarType.Date || DateTime.TryParse(val.AsString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
    public static VbValue IsEmpty(VbValue val) => VbValue.FromBoolean(val.IsEmpty);
    public static VbValue IsNull(VbValue val) => VbValue.FromBoolean(val.IsNull);

    #endregion

    #region Funciones Matemáticas

    public static VbValue Abs(VbValue val) => VbValue.FromDouble(Math.Abs(val.AsDouble()));
    public static VbValue Sqr(VbValue val) => VbValue.FromDouble(Math.Sqrt(val.AsDouble()));
    public static VbValue Int(VbValue val) => VbValue.FromLong((int)Math.Floor(val.AsDouble()));
    public static VbValue Fix(VbValue val) => VbValue.FromLong((int)Math.Truncate(val.AsDouble()));
    public static VbValue Round(VbValue val, VbValue? decimals = null)
    {
        int dec = decimals?.AsLong() ?? 0;
        return VbValue.FromDouble(Math.Round(val.AsDouble(), Math.Max(0, dec)));
    }
    public static VbValue Rnd(VbValue? val = null) => VbValue.FromSingle((float)_random.NextDouble());
    public static VbValue Sgn(VbValue val) => VbValue.FromInteger((short)Math.Sign(val.AsDouble()));

    #endregion

    #region Fecha y Hora

    public static VbValue Now() => VbValue.FromDate(DateTime.Now);
    public static VbValue Date() => VbValue.FromDate(DateTime.Today);
    public static VbValue Time() => VbValue.FromString(DateTime.Now.ToString("HH:mm:ss"));
    public static VbValue Year(VbValue date) => VbValue.FromInteger((short)date.AsDate().Year);
    public static VbValue Month(VbValue date) => VbValue.FromInteger((short)date.AsDate().Month);
    public static VbValue Day(VbValue date) => VbValue.FromInteger((short)date.AsDate().Day);
    public static VbValue Hour(VbValue date) => VbValue.FromInteger((short)date.AsDate().Hour);
    public static VbValue Minute(VbValue date) => VbValue.FromInteger((short)date.AsDate().Minute);
    public static VbValue Second(VbValue date) => VbValue.FromInteger((short)date.AsDate().Second);

    public static VbValue IIf(VbValue expression, VbValue truePart, VbValue falsePart) =>
        expression.AsBoolean() ? truePart : falsePart;

    #endregion

    public static bool TryInvoke(string functionName, VbValue[] args, out VbValue result)
    {
        result = VbValue.Empty;
        string fn = functionName.Trim().ToUpperInvariant();

        switch (fn)
        {
            case "IIF":
                if (args.Length >= 3) { result = IIf(args[0], args[1], args[2]); return true; }
                break;
            case "LEN":
                if (args.Length >= 1) { result = Len(args[0]); return true; }
                break;
            case "MID":
                if (args.Length >= 2) { result = Mid(args[0], args[1], args.Length >= 3 ? args[2] : null); return true; }
                break;
            case "LEFT":
                if (args.Length >= 2) { result = Left(args[0], args[1]); return true; }
                break;
            case "RIGHT":
                if (args.Length >= 2) { result = Right(args[0], args[1]); return true; }
                break;
            case "INSTR":
                if (args.Length >= 2) { result = InStr(args[0], args[1], args.Length >= 3 ? args[2] : null); return true; }
                break;
            case "UCASE":
                if (args.Length >= 1) { result = UCase(args[0]); return true; }
                break;
            case "LCASE":
                if (args.Length >= 1) { result = LCase(args[0]); return true; }
                break;
            case "TRIM":
                if (args.Length >= 1) { result = Trim(args[0]); return true; }
                break;
            case "LTRIM":
                if (args.Length >= 1) { result = LTrim(args[0]); return true; }
                break;
            case "RTRIM":
                if (args.Length >= 1) { result = RTrim(args[0]); return true; }
                break;
            case "SPACE":
                if (args.Length >= 1) { result = Space(args[0]); return true; }
                break;
            case "CHR":
                if (args.Length >= 1) { result = Chr(args[0]); return true; }
                break;
            case "ASC":
                if (args.Length >= 1) { result = Asc(args[0]); return true; }
                break;
            case "REPLACE":
                if (args.Length >= 3) { result = Replace(args[0], args[1], args[2]); return true; }
                break;
            case "VAL":
                if (args.Length >= 1) { result = Val(args[0]); return true; }
                break;
            case "STR":
                if (args.Length >= 1) { result = Str(args[0]); return true; }
                break;
            case "CINT":
                if (args.Length >= 1) { result = CInt(args[0]); return true; }
                break;
            case "CLNG":
                if (args.Length >= 1) { result = CLng(args[0]); return true; }
                break;
            case "CSNG":
                if (args.Length >= 1) { result = CSng(args[0]); return true; }
                break;
            case "CDBL":
                if (args.Length >= 1) { result = CDbl(args[0]); return true; }
                break;
            case "CBOOL":
                if (args.Length >= 1) { result = CBool(args[0]); return true; }
                break;
            case "CSTR":
                if (args.Length >= 1) { result = CStr(args[0]); return true; }
                break;
            case "CDATE":
                if (args.Length >= 1) { result = CDate(args[0]); return true; }
                break;
            case "ISNUMERIC":
                if (args.Length >= 1) { result = IsNumeric(args[0]); return true; }
                break;
            case "ISDATE":
                if (args.Length >= 1) { result = IsDate(args[0]); return true; }
                break;
            case "ISEMPTY":
                if (args.Length >= 1) { result = IsEmpty(args[0]); return true; }
                break;
            case "ISNULL":
                if (args.Length >= 1) { result = IsNull(args[0]); return true; }
                break;
            case "ABS":
                if (args.Length >= 1) { result = Abs(args[0]); return true; }
                break;
            case "SQR":
                if (args.Length >= 1) { result = Sqr(args[0]); return true; }
                break;
            case "INT":
                if (args.Length >= 1) { result = Int(args[0]); return true; }
                break;
            case "FIX":
                if (args.Length >= 1) { result = Fix(args[0]); return true; }
                break;
            case "ROUND":
                if (args.Length >= 1) { result = Round(args[0], args.Length >= 2 ? args[1] : null); return true; }
                break;
            case "RND":
                result = Rnd(args.Length >= 1 ? args[0] : null); return true;
            case "SGN":
                if (args.Length >= 1) { result = Sgn(args[0]); return true; }
                break;
            case "NOW":
                result = Now(); return true;
            case "DATE":
                result = Date(); return true;
            case "TIME":
                result = Time(); return true;
            case "YEAR":
                if (args.Length >= 1) { result = Year(args[0]); return true; }
                break;
            case "MONTH":
                if (args.Length >= 1) { result = Month(args[0]); return true; }
                break;
            case "DAY":
                if (args.Length >= 1) { result = Day(args[0]); return true; }
                break;
            case "HOUR":
                if (args.Length >= 1) { result = Hour(args[0]); return true; }
                break;
            case "MINUTE":
                if (args.Length >= 1) { result = Minute(args[0]); return true; }
                break;
            case "SECOND":
                if (args.Length >= 1) { result = Second(args[0]); return true; }
                break;
        }

        return false;
    }
}
