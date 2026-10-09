using System;
using System.Collections.Generic;
using System.Text;

namespace VB7x64.ProjectModel.CodeBuilders;

public enum BuilderProcedureKind
{
    Sub,
    Function
}

public class ProcedureParameter
{
    public string Name { get; set; } = "";
    public string PassingMechanism { get; set; } = "ByVal"; // ByVal o ByRef
    public string DataType { get; set; } = "String";
    public bool IsOptional { get; set; } = false;
    public string DefaultValue { get; set; } = "";

    public string ToVbDeclaration()
    {
        string pName = string.IsNullOrWhiteSpace(Name) ? "param" : Name.Trim();
        string pass = string.IsNullOrWhiteSpace(PassingMechanism) ? "ByVal" : PassingMechanism.Trim();
        string type = string.IsNullOrWhiteSpace(DataType) ? "Variant" : DataType.Trim();

        var sb = new StringBuilder();
        if (IsOptional)
        {
            sb.Append("Optional ");
        }
        sb.Append($"{pass} {pName} As {type}");
        if (IsOptional && !string.IsNullOrWhiteSpace(DefaultValue))
        {
            sb.Append($" = {DefaultValue.Trim()}");
        }
        return sb.ToString();
    }
}

public class ProcedureBuilderOptions
{
    public string ProcedureName { get; set; } = "";
    public BuilderProcedureKind Kind { get; set; } = BuilderProcedureKind.Sub;
    public string Scope { get; set; } = "Public"; // Public, Private, Friend
    public string ReturnType { get; set; } = "Boolean";
    public List<ProcedureParameter> Parameters { get; set; } = [];
    public bool IncludeErrorHandler { get; set; } = true;
    public bool IncludeComments { get; set; } = false;
}

public static class ProcedureBuilderHelper
{
    public static string GenerateProcedureCode(ProcedureBuilderOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        string name = string.IsNullOrWhiteSpace(options.ProcedureName) ? "NewProcedure" : options.ProcedureName.Trim();
        string scope = string.IsNullOrWhiteSpace(options.Scope) ? "Public" : options.Scope.Trim();
        string kindStr = options.Kind == BuilderProcedureKind.Function ? "Function" : "Sub";

        var sb = new StringBuilder();

        if (options.IncludeComments)
        {
            sb.AppendLine($"' ===========================================================================");
            sb.AppendLine($"' Procedimiento: {name}");
            sb.AppendLine($"' Tipo         : {kindStr}");
            sb.AppendLine($"' ===========================================================================");
        }

        // Firma
        var paramList = new List<string>();
        foreach (var p in options.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(p.Name))
            {
                paramList.Add(p.ToVbDeclaration());
            }
        }
        string paramStr = string.Join(", ", paramList);

        if (options.Kind == BuilderProcedureKind.Function)
        {
            string retType = string.IsNullOrWhiteSpace(options.ReturnType) ? "Variant" : options.ReturnType.Trim();
            sb.AppendLine($"{scope} {kindStr} {name}({paramStr}) As {retType}");
        }
        else
        {
            sb.AppendLine($"{scope} {kindStr} {name}({paramStr})");
        }

        // Manejo de errores
        if (options.IncludeErrorHandler)
        {
            sb.AppendLine("    On Error GoTo ErrorHandler");
            sb.AppendLine();
            sb.AppendLine("    ' TODO: Código del procedimiento aquí");
            sb.AppendLine();
            if (options.Kind == BuilderProcedureKind.Function)
            {
                sb.AppendLine($"    ' {name} = ...");
            }
            sb.AppendLine($"    Exit {kindStr}");
            sb.AppendLine();
            sb.AppendLine("ErrorHandler:");
            sb.AppendLine($"    MsgBox \"Error \" & Err.Number & \": \" & Err.Description, vbCritical, \"{name}\"");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("    ' TODO: Código del procedimiento aquí");
            sb.AppendLine();
            if (options.Kind == BuilderProcedureKind.Function)
            {
                sb.AppendLine($"    ' {name} = ...");
            }
        }

        sb.AppendLine($"End {kindStr}");

        return sb.ToString().TrimEnd();
    }
}
