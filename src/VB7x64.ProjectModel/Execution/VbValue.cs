using System;
using System.Globalization;

namespace VB7x64.ProjectModel.Execution;

public enum VbVarType
{
    Empty = 0,
    Null = 1,
    Integer = 2,
    Long = 3,
    Single = 4,
    Double = 5,
    Currency = 6,
    Date = 7,
    String = 8,
    Object = 9,
    Error = 10,
    Boolean = 11,
    Variant = 12
}

/// <summary>
/// Representa un valor tipado en tiempo de ejecución de Visual Basic 6.0 con semántica
/// de coerción implícita, valores Empty/Null/Nothing y operaciones aritméticas/lógicas exactas.
/// </summary>
public readonly struct VbValue : IEquatable<VbValue>
{
    public VbVarType VarType { get; }
    public object? RawValue { get; }

    public static readonly VbValue Empty = new(VbVarType.Empty, null);
    public static readonly VbValue Null = new(VbVarType.Null, null);
    public static readonly VbValue Nothing = new(VbVarType.Object, null);
    public static readonly VbValue True = new(VbVarType.Boolean, true);
    public static readonly VbValue False = new(VbVarType.Boolean, false);

    private VbValue(VbVarType varType, object? rawValue)
    {
        VarType = varType;
        RawValue = rawValue;
    }

    #region Constructores estáticos

    public static VbValue FromInteger(short value) => new(VbVarType.Integer, value);
    public static VbValue FromLong(int value) => new(VbVarType.Long, value);
    public static VbValue FromSingle(float value) => new(VbVarType.Single, value);
    public static VbValue FromDouble(double value) => new(VbVarType.Double, value);
    public static VbValue FromCurrency(decimal value) => new(VbVarType.Currency, value);
    public static VbValue FromString(string? value) => new(VbVarType.String, value ?? string.Empty);
    public static VbValue FromBoolean(bool value) => value ? True : False;
    public static VbValue FromDate(DateTime value) => new(VbVarType.Date, value);
    public static VbValue FromObject(object? obj) => obj == null ? Nothing : new(VbVarType.Object, obj);

    public static VbValue FromRaw(object? value)
    {
        return value switch
        {
            null => Empty,
            VbValue v => v,
            bool b => FromBoolean(b),
            short s => FromInteger(s),
            int i => FromLong(i),
            long l => FromLong((int)l),
            float f => FromSingle(f),
            double d => FromDouble(d),
            decimal m => FromCurrency(m),
            string str => FromString(str),
            DateTime dt => FromDate(dt),
            _ => FromObject(value)
        };
    }

    #endregion

    #region Propiedades de comprobación

    public bool IsEmpty => VarType == VbVarType.Empty;
    public bool IsNull => VarType == VbVarType.Null;
    public bool IsNothing => VarType == VbVarType.Object && RawValue == null;
    public bool IsNumeric => VarType is VbVarType.Integer or VbVarType.Long or VbVarType.Single or VbVarType.Double or VbVarType.Currency
        || (VarType == VbVarType.String && double.TryParse(RawValue as string, NumberStyles.Any, CultureInfo.InvariantCulture, out _));

    #endregion

    #region Conversiones implícitas y explícitas de tipos

    public string AsString()
    {
        if (IsEmpty || IsNull) return string.Empty;
        if (VarType == VbVarType.Boolean) return (bool)RawValue! ? "True" : "False";
        if (VarType == VbVarType.Date) return ((DateTime)RawValue!).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        if (VarType == VbVarType.Double) return ((double)RawValue!).ToString(CultureInfo.InvariantCulture);
        if (VarType == VbVarType.Single) return ((float)RawValue!).ToString(CultureInfo.InvariantCulture);
        if (VarType == VbVarType.Currency) return ((decimal)RawValue!).ToString(CultureInfo.InvariantCulture);
        if (RawValue is string s) return s;
        return RawValue?.ToString() ?? string.Empty;
    }

    public bool AsBoolean()
    {
        if (IsEmpty || IsNull) return false;
        if (VarType == VbVarType.Boolean) return (bool)RawValue!;
        if (VarType == VbVarType.String)
        {
            string s = (string)RawValue!;
            if (bool.TryParse(s, out bool b)) return b;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) return d != 0.0;
            return false;
        }
        return AsDouble() != 0.0;
    }

    public int AsLong()
    {
        if (IsEmpty || IsNull) return 0;
        if (VarType == VbVarType.Boolean) return (bool)RawValue! ? -1 : 0;
        if (VarType == VbVarType.String)
        {
            if (double.TryParse((string)RawValue!, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return (int)Math.Round(d);
            return 0;
        }
        return Convert.ToInt32(RawValue, CultureInfo.InvariantCulture);
    }

    public double AsDouble()
    {
        if (IsEmpty || IsNull) return 0.0;
        if (VarType == VbVarType.Boolean) return (bool)RawValue! ? -1.0 : 0.0;
        if (VarType == VbVarType.String)
        {
            if (double.TryParse((string)RawValue!, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            return 0.0;
        }
        return Convert.ToDouble(RawValue, CultureInfo.InvariantCulture);
    }

    public DateTime AsDate()
    {
        if (VarType == VbVarType.Date) return (DateTime)RawValue!;
        if (VarType == VbVarType.String && DateTime.TryParse((string)RawValue!, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.MinValue;
    }

    #endregion

    #region Operaciones Aritméticas y de Concatenación

    public static VbValue operator +(VbValue a, VbValue b)
    {
        // En VB6: Si ambos son strings, concatena. Si uno es string y otro número, intenta sumar.
        if (a.VarType == VbVarType.String && b.VarType == VbVarType.String)
        {
            return FromString(a.AsString() + b.AsString());
        }

        if (a.VarType == VbVarType.String && double.TryParse(a.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double aNum))
        {
            return FromDouble(aNum + b.AsDouble());
        }

        if (b.VarType == VbVarType.String && double.TryParse(b.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double bNum))
        {
            return FromDouble(a.AsDouble() + bNum);
        }

        return FromDouble(a.AsDouble() + b.AsDouble());
    }

    public static VbValue operator -(VbValue a, VbValue b) => FromDouble(a.AsDouble() - b.AsDouble());
    public static VbValue operator *(VbValue a, VbValue b) => FromDouble(a.AsDouble() * b.AsDouble());

    public static VbValue operator /(VbValue a, VbValue b)
    {
        double divisor = b.AsDouble();
        if (divisor == 0.0) throw new DivideByZeroException("División por cero en tiempo de ejecución.");
        return FromDouble(a.AsDouble() / divisor);
    }

    public static VbValue IntegerDivide(VbValue a, VbValue b)
    {
        int divisor = b.AsLong();
        if (divisor == 0) throw new DivideByZeroException("División por cero en tiempo de ejecución.");
        return FromLong(a.AsLong() / divisor);
    }

    public static VbValue Modulo(VbValue a, VbValue b)
    {
        int divisor = b.AsLong();
        if (divisor == 0) throw new DivideByZeroException("División por cero en tiempo de ejecución.");
        return FromLong(a.AsLong() % divisor);
    }

    public static VbValue Power(VbValue a, VbValue b) => FromDouble(Math.Pow(a.AsDouble(), b.AsDouble()));

    /// <summary>
    /// Operador de concatenación ampersand (&) que siempre convierte a cadena.
    /// </summary>
    public static VbValue Concat(VbValue a, VbValue b) => FromString(a.AsString() + b.AsString());

    #endregion

    #region Operadores Lógicos y Bitwise de VB6

    public static VbValue operator !(VbValue a) => FromBoolean(!a.AsBoolean());

    public static VbValue VbNot(VbValue a)
    {
        if (a.VarType == VbVarType.Boolean) return FromBoolean(!a.AsBoolean());
        return FromLong(~a.AsLong());
    }

    public static VbValue VbAnd(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.Boolean && b.VarType == VbVarType.Boolean)
            return FromBoolean(a.AsBoolean() && b.AsBoolean());
        return FromLong(a.AsLong() & b.AsLong());
    }

    public static VbValue VbOr(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.Boolean && b.VarType == VbVarType.Boolean)
            return FromBoolean(a.AsBoolean() || b.AsBoolean());
        return FromLong(a.AsLong() | b.AsLong());
    }

    public static VbValue VbXor(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.Boolean && b.VarType == VbVarType.Boolean)
            return FromBoolean(a.AsBoolean() ^ b.AsBoolean());
        return FromLong(a.AsLong() ^ b.AsLong());
    }

    public static VbValue VbEqv(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.Boolean && b.VarType == VbVarType.Boolean)
            return FromBoolean(a.AsBoolean() == b.AsBoolean());
        return FromLong(~(a.AsLong() ^ b.AsLong()));
    }

    public static VbValue VbImp(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.Boolean && b.VarType == VbVarType.Boolean)
            return FromBoolean(!a.AsBoolean() || b.AsBoolean());
        return FromLong(~a.AsLong() | b.AsLong());
    }

    #endregion

    #region Operadores de Comparación

    public static VbValue CompareEqual(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.String || b.VarType == VbVarType.String)
            return FromBoolean(string.Equals(a.AsString(), b.AsString(), StringComparison.OrdinalIgnoreCase));
        return FromBoolean(Math.Abs(a.AsDouble() - b.AsDouble()) < 0.0000000001);
    }

    public static VbValue CompareNotEqual(VbValue a, VbValue b) => FromBoolean(!CompareEqual(a, b).AsBoolean());

    public static VbValue CompareLessThan(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.String && b.VarType == VbVarType.String)
            return FromBoolean(string.Compare(a.AsString(), b.AsString(), StringComparison.OrdinalIgnoreCase) < 0);
        return FromBoolean(a.AsDouble() < b.AsDouble());
    }

    public static VbValue CompareGreaterThan(VbValue a, VbValue b)
    {
        if (a.VarType == VbVarType.String && b.VarType == VbVarType.String)
            return FromBoolean(string.Compare(a.AsString(), b.AsString(), StringComparison.OrdinalIgnoreCase) > 0);
        return FromBoolean(a.AsDouble() > b.AsDouble());
    }

    public static VbValue CompareLessOrEqual(VbValue a, VbValue b) =>
        FromBoolean(CompareLessThan(a, b).AsBoolean() || CompareEqual(a, b).AsBoolean());

    public static VbValue CompareGreaterOrEqual(VbValue a, VbValue b) =>
        FromBoolean(CompareGreaterThan(a, b).AsBoolean() || CompareEqual(a, b).AsBoolean());

    #endregion

    public override string ToString() => AsString();

    public bool Equals(VbValue other) =>
        VarType == other.VarType && Equals(RawValue, other.RawValue);

    public override bool Equals(object? obj) => obj is VbValue other && Equals(other);

    public override int GetHashCode() => HashCode.Combine((int)VarType, RawValue);

    public static bool operator ==(VbValue left, VbValue right) => left.Equals(right);
    public static bool operator !=(VbValue left, VbValue right) => !left.Equals(right);
}
