namespace VB7x64.ProjectModel.Navigation;

public enum ProcedureKind
{
    Sub,
    Function,
    PropertyGet,
    PropertyLet,
    PropertySet
}

public class ProcedureSymbol
{
    public string Name { get; set; } = string.Empty;
    public ProcedureKind Kind { get; set; }
    public string Scope { get; set; } = "Public";
    public string Signature { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Offset { get; set; }

    public string DisplayIcon => Kind switch
    {
        ProcedureKind.Sub => "⚡",
        ProcedureKind.Function => "ƒ",
        ProcedureKind.PropertyGet => "📖",
        ProcedureKind.PropertyLet => "✏️",
        ProcedureKind.PropertySet => "🔧",
        _ => "•"
    };

    public string DisplayText => $"{DisplayIcon} {Name} ({Kind})";
}
