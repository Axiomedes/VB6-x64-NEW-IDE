using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ViBa7.ProjectModel.Navigation;

public static partial class ProcedureParser
{
    private static readonly Regex ProcedureRegex = new(
        @"^\s*(?:(Public|Private|Friend|Static)\s+)?(Sub|Function|Property\s+(?:Get|Let|Set))\s+([a-zA-Z0-9_]+)\s*(\(.*?\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<ProcedureSymbol> ParseProcedures(string text)
    {
        var symbols = new List<ProcedureSymbol>();
        if (string.IsNullOrWhiteSpace(text)) return symbols;

        using var reader = new StringReader(text);
        string? line;
        int lineNumber = 0;
        int currentOffset = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith('\'') && !trimmed.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase))
            {
                var match = ProcedureRegex.Match(line);
                if (match.Success)
                {
                    string scope = match.Groups[1].Success ? match.Groups[1].Value : "Public";
                    string kindStr = match.Groups[2].Value.ToUpperInvariant().Replace(" ", "");
                    string name = match.Groups[3].Value;
                    string sig = match.Groups[4].Success ? match.Groups[4].Value : "()";

                    ProcedureKind kind = ProcedureKind.Sub;
                    if (kindStr.StartsWith("FUNCTION")) kind = ProcedureKind.Function;
                    else if (kindStr.StartsWith("PROPERTYGET")) kind = ProcedureKind.PropertyGet;
                    else if (kindStr.StartsWith("PROPERTYLET")) kind = ProcedureKind.PropertyLet;
                    else if (kindStr.StartsWith("PROPERTYSET")) kind = ProcedureKind.PropertySet;

                    symbols.Add(new ProcedureSymbol
                    {
                        Name = name,
                        Kind = kind,
                        Scope = scope,
                        Signature = sig,
                        LineNumber = lineNumber,
                        Offset = currentOffset
                    });
                }
            }

            // Sumar longitud de línea más salto
            currentOffset += line.Length + 2; // Estimado CRLF
        }

        return symbols;
    }
}
