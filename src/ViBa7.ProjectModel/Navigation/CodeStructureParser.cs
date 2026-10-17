using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ViBa7.ProjectModel.Navigation;

public static class CodeStructureParser
{
    private static readonly Regex ProcedureRegex = new(
        @"^\s*(?:(Public|Private|Friend|Static)\s+)?(Sub|Function|Property\s+(?:Get|Let|Set))\s+([a-zA-Z0-9_]+)\s*(\(.*?\))?(?:\s+As\s+([a-zA-Z0-9_.]+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndProcedureRegex = new(
        @"^\s*End\s+(Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventRegex = new(
        @"^\s*(?:(Public)\s+)?Event\s+([a-zA-Z0-9_]+)\s*(\(.*?\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EnumRegex = new(
        @"^\s*(?:(Public|Private)\s+)?Enum\s+([a-zA-Z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndEnumRegex = new(
        @"^\s*End\s+Enum\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeRegex = new(
        @"^\s*(?:(Public|Private)\s+)?Type\s+([a-zA-Z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndTypeRegex = new(
        @"^\s*End\s+Type\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConstRegex = new(
        @"^\s*(?:(Public|Private|Global)\s+)?Const\s+([a-zA-Z0-9_]+)(?:\s+As\s+([a-zA-Z0-9_.]+))?\s*=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VariableRegex = new(
        @"^\s*(Public|Private|Dim|Global)\s+(?:WithEvents\s+)?([a-zA-Z0-9_]+)(?:\s*\([^)]*\))?(?:\s+As\s+(?:New\s+)?([a-zA-Z0-9_.]+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedKeywordsNotVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sub", "Function", "Property", "Const", "Enum", "Type", "Event", "Declare", "Static", "Explicit", "Compare"
    };

    public static List<CodeMemberSymbol> ParseMembers(string text, string sourceFilePath = "")
    {
        var members = new List<CodeMemberSymbol>();
        if (string.IsNullOrWhiteSpace(text)) return members;

        using var reader = new StringReader(text);
        string? rawLine;
        int lineNumber = 0;

        bool inProcedure = false;
        bool inEnum = false;
        bool inType = false;

        while ((rawLine = reader.ReadLine()) != null)
        {
            lineNumber++;
            string trimmed = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Ignorar líneas de comentario puro
            if (trimmed.StartsWith('\'') || trimmed.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
                continue;

            string clean = StripInlineComment(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(clean))
                continue;

            // 1. Verificar cierres de bloques
            if (inProcedure && EndProcedureRegex.IsMatch(clean))
            {
                inProcedure = false;
                continue;
            }

            if (inEnum && EndEnumRegex.IsMatch(clean))
            {
                inEnum = false;
                continue;
            }

            if (inType && EndTypeRegex.IsMatch(clean))
            {
                inType = false;
                continue;
            }

            // Si estamos dentro de un Type o Enum, ignoramos las definiciones de campos internos
            if (inType || inEnum)
                continue;

            // 2. Comprobar inicio de Procedimientos (Sub, Function, Property)
            // Descartar API 'Declare Sub' / 'Declare Function'
            if (!clean.Contains("Declare", StringComparison.OrdinalIgnoreCase))
            {
                var procMatch = ProcedureRegex.Match(clean);
                if (procMatch.Success)
                {
                    string scope = procMatch.Groups[1].Success ? procMatch.Groups[1].Value : "Public";
                    string kindStr = procMatch.Groups[2].Value.ToUpperInvariant().Replace(" ", "");
                    string name = procMatch.Groups[3].Value;
                    string sig = procMatch.Groups[4].Success ? procMatch.Groups[4].Value : "()";
                    string typeName = procMatch.Groups[5].Success ? procMatch.Groups[5].Value : "";

                    CodeMemberKind kind = CodeMemberKind.Sub;
                    if (kindStr.StartsWith("FUNCTION")) kind = CodeMemberKind.Function;
                    else if (kindStr.StartsWith("PROPERTYGET")) kind = CodeMemberKind.PropertyGet;
                    else if (kindStr.StartsWith("PROPERTYLET")) kind = CodeMemberKind.PropertyLet;
                    else if (kindStr.StartsWith("PROPERTYSET")) kind = CodeMemberKind.PropertySet;

                    members.Add(new CodeMemberSymbol
                    {
                        Name = name,
                        Kind = kind,
                        Scope = scope,
                        Signature = sig,
                        TypeName = typeName,
                        LineNumber = lineNumber,
                        SourceFilePath = sourceFilePath
                    });

                    inProcedure = true;
                    continue;
                }
            }

            // Si estamos dentro de un procedimiento, NO analizamos variables locales (Dim, Const locales)
            if (inProcedure)
                continue;

            // 3. Declaraciones a nivel de módulo (sólo fuera de procedimientos)

            // A. Eventos
            var eventMatch = EventRegex.Match(clean);
            if (eventMatch.Success)
            {
                string scope = eventMatch.Groups[1].Success ? eventMatch.Groups[1].Value : "Public";
                string name = eventMatch.Groups[2].Value;
                string sig = eventMatch.Groups[3].Success ? eventMatch.Groups[3].Value : "()";

                members.Add(new CodeMemberSymbol
                {
                    Name = name,
                    Kind = CodeMemberKind.Event,
                    Scope = scope,
                    Signature = sig,
                    LineNumber = lineNumber,
                    SourceFilePath = sourceFilePath
                });
                continue;
            }

            // B. Enums
            var enumMatch = EnumRegex.Match(clean);
            if (enumMatch.Success)
            {
                string scope = enumMatch.Groups[1].Success ? enumMatch.Groups[1].Value : "Public";
                string name = enumMatch.Groups[2].Value;

                members.Add(new CodeMemberSymbol
                {
                    Name = name,
                    Kind = CodeMemberKind.Enum,
                    Scope = scope,
                    LineNumber = lineNumber,
                    SourceFilePath = sourceFilePath
                });
                inEnum = true;
                continue;
            }

            // C. Types (User-defined Types)
            var typeMatch = TypeRegex.Match(clean);
            if (typeMatch.Success)
            {
                string scope = typeMatch.Groups[1].Success ? typeMatch.Groups[1].Value : "Public";
                string name = typeMatch.Groups[2].Value;

                members.Add(new CodeMemberSymbol
                {
                    Name = name,
                    Kind = CodeMemberKind.UserType,
                    Scope = scope,
                    LineNumber = lineNumber,
                    SourceFilePath = sourceFilePath
                });
                inType = true;
                continue;
            }

            // D. Constantes a nivel de módulo
            var constMatch = ConstRegex.Match(clean);
            if (constMatch.Success)
            {
                string scope = constMatch.Groups[1].Success ? constMatch.Groups[1].Value : "Private";
                string name = constMatch.Groups[2].Value;
                string typeName = constMatch.Groups[3].Success ? constMatch.Groups[3].Value : "";

                members.Add(new CodeMemberSymbol
                {
                    Name = name,
                    Kind = CodeMemberKind.Constant,
                    Scope = scope,
                    TypeName = typeName,
                    LineNumber = lineNumber,
                    SourceFilePath = sourceFilePath
                });
                continue;
            }

            // E. Variables a nivel de módulo (Dim, Private, Public, Global)
            var varMatch = VariableRegex.Match(clean);
            if (varMatch.Success)
            {
                string candidateName = varMatch.Groups[2].Value;
                if (!ReservedKeywordsNotVariables.Contains(candidateName))
                {
                    string scope = varMatch.Groups[1].Value;
                    string typeName = varMatch.Groups[3].Success ? varMatch.Groups[3].Value : "Variant";

                    members.Add(new CodeMemberSymbol
                    {
                        Name = candidateName,
                        Kind = CodeMemberKind.Variable,
                        Scope = scope,
                        TypeName = typeName,
                        LineNumber = lineNumber,
                        SourceFilePath = sourceFilePath
                    });
                }
            }
        }

        return members;
    }

    private static string StripInlineComment(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '\'' && !inQuotes)
            {
                return line[..i];
            }
        }
        return line;
    }
}
