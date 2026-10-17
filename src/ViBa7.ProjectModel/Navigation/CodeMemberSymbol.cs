namespace ViBa7.ProjectModel.Navigation;

public enum CodeMemberKind
{
    Sub,
    Function,
    PropertyGet,
    PropertyLet,
    PropertySet,
    Event,
    Constant,
    Variable,
    Enum,
    UserType
}

public class CodeMemberSymbol
{
    public string Name { get; set; } = string.Empty;
    public CodeMemberKind Kind { get; set; }
    public string Scope { get; set; } = "Public";
    public string TypeName { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string SourceFilePath { get; set; } = string.Empty;

    public string DisplayIcon => Kind switch
    {
        CodeMemberKind.Sub => "⚡",
        CodeMemberKind.Function => "ƒ",
        CodeMemberKind.PropertyGet => "📖",
        CodeMemberKind.PropertyLet => "✏️",
        CodeMemberKind.PropertySet => "🔧",
        CodeMemberKind.Event => "🔔",
        CodeMemberKind.Constant => "🔒",
        CodeMemberKind.Variable => "📦",
        CodeMemberKind.Enum => "📑",
        CodeMemberKind.UserType => "📐",
        _ => "•"
    };

    public string KindDescription => Kind switch
    {
        CodeMemberKind.Sub => "Sub",
        CodeMemberKind.Function => "Function",
        CodeMemberKind.PropertyGet => "Property Get",
        CodeMemberKind.PropertyLet => "Property Let",
        CodeMemberKind.PropertySet => "Property Set",
        CodeMemberKind.Event => "Event",
        CodeMemberKind.Constant => "Const",
        CodeMemberKind.Variable => "Var",
        CodeMemberKind.Enum => "Enum",
        CodeMemberKind.UserType => "Type",
        _ => ""
    };

    public string DisplayText
    {
        get
        {
            string typeSuffix = !string.IsNullOrWhiteSpace(TypeName) ? $" As {TypeName}" : "";
            string sigText = !string.IsNullOrWhiteSpace(Signature) ? Signature : "";
            return $"{DisplayIcon} {Name}{sigText}{typeSuffix} [{Scope} {KindDescription}]";
        }
    }

    public override string ToString() => $"{DisplayText} (Ln {LineNumber})";
}
