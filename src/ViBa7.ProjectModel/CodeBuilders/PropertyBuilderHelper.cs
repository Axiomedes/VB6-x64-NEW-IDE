using System;
using System.Text;

namespace ViBa7.ProjectModel.CodeBuilders;

public class PropertyBuilderOptions
{
    public string PropertyName { get; set; } = "";
    public string DataType { get; set; } = "String";
    public string BackingFieldName { get; set; } = "";
    public string Scope { get; set; } = "Public";
    public bool GenerateGet { get; set; } = true;
    public bool GenerateLet { get; set; } = true;
    public bool GenerateSet { get; set; } = false;
    public bool GenerateBackingField { get; set; } = true;
    public bool IncludeComments { get; set; } = false;
}

public static class PropertyBuilderHelper
{
    private static readonly HashSet<string> PrimitiveValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Byte", "Integer", "Long", "Single", "Double", "Currency", "Decimal",
        "Boolean", "Date", "String"
    };

    public static bool IsObjectType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return false;
        string clean = typeName.Trim();
        if (PrimitiveValueTypes.Contains(clean)) return false;
        return true;
    }

    public static string GeneratePropertyCode(PropertyBuilderOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        string propName = string.IsNullOrWhiteSpace(options.PropertyName) ? "NewProperty" : options.PropertyName.Trim();
        string typeName = string.IsNullOrWhiteSpace(options.DataType) ? "Variant" : options.DataType.Trim();
        string scope = string.IsNullOrWhiteSpace(options.Scope) ? "Public" : options.Scope.Trim();

        string backingField = !string.IsNullOrWhiteSpace(options.BackingFieldName)
            ? options.BackingFieldName.Trim()
            : $"m_{propName}";

        bool isObject = IsObjectType(typeName);
        var sb = new StringBuilder();

        // 1. Variable de respaldo privada
        if (options.GenerateBackingField)
        {
            if (options.IncludeComments)
            {
                sb.AppendLine($"' Variable de respaldo para la propiedad {propName}");
            }
            sb.AppendLine($"Private {backingField} As {typeName}");
            sb.AppendLine();
        }

        // 2. Property Get
        if (options.GenerateGet)
        {
            if (options.IncludeComments)
            {
                sb.AppendLine($"' Obtiene el valor de la propiedad {propName}");
            }
            sb.AppendLine($"{scope} Property Get {propName}() As {typeName}");
            if (options.GenerateBackingField)
            {
                if (isObject)
                {
                    sb.AppendLine($"    Set {propName} = {backingField}");
                }
                else
                {
                    sb.AppendLine($"    {propName} = {backingField}");
                }
            }
            else
            {
                sb.AppendLine($"    ' TODO: Retornar valor de la propiedad");
            }
            sb.AppendLine("End Property");
            sb.AppendLine();
        }

        // 3. Property Let (Tipos por valor / primitivos)
        if (options.GenerateLet)
        {
            if (options.IncludeComments)
            {
                sb.AppendLine($"' Establece el valor de la propiedad {propName}");
            }
            sb.AppendLine($"{scope} Property Let {propName}(ByVal value As {typeName})");
            if (options.GenerateBackingField)
            {
                sb.AppendLine($"    {backingField} = value");
            }
            else
            {
                sb.AppendLine($"    ' TODO: Asignar valor a la propiedad");
            }
            sb.AppendLine("End Property");
            sb.AppendLine();
        }

        // 4. Property Set (Objetos / referencias)
        if (options.GenerateSet)
        {
            if (options.IncludeComments)
            {
                sb.AppendLine($"' Establece la referencia de objeto de la propiedad {propName}");
            }
            sb.AppendLine($"{scope} Property Set {propName}(ByVal value As {typeName})");
            if (options.GenerateBackingField)
            {
                sb.AppendLine($"    Set {backingField} = value");
            }
            else
            {
                sb.AppendLine($"    ' TODO: Asignar referencia de objeto a la propiedad");
            }
            sb.AppendLine("End Property");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
