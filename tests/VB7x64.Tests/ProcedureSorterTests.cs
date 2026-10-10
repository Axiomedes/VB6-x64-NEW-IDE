using System.Collections.Generic;
using System.Linq;
using VB7x64.ProjectModel.Formatting;
using Xunit;

namespace VB7x64.Tests;

public class ProcedureSorterTests
{
    private const string SampleCode = 
@"Option Explicit
Dim mCount As Long

' Comentario de Zeta
Public Sub Zeta()
    MsgBox ""Zeta""
End Sub

Private Function Alpha() As Integer
    Alpha = 42
End Function

Public Property Get Beta() As String
    Beta = ""Beta""
End Property

Public Property Let Beta(value As String)
    ' Set value
End Property";

    [Fact]
    public void ParseProcedures_ExtractsHeaderAndProceduresCorrectly()
    {
        var (header, procs) = ProcedureSorter.ParseProcedures(SampleCode);

        Assert.Contains("Option Explicit", header);
        Assert.Contains("Dim mCount As Long", header);
        Assert.DoesNotContain("Public Sub Zeta", header);

        Assert.Equal(4, procs.Count);
        Assert.Equal("Zeta", procs[0].Name);
        Assert.Equal("Alpha", procs[1].Name);
        Assert.Equal("Beta", procs[2].Name);
        Assert.Equal("Beta", procs[3].Name);

        Assert.Contains("' Comentario de Zeta", procs[0].LeadingComments);
    }

    [Fact]
    public void SortProcedures_AlphabeticalAsc_OrdersAlphabetically()
    {
        var (_, procs) = ProcedureSorter.ParseProcedures(SampleCode);
        var sorted = ProcedureSorter.SortProcedures(procs, new ProcedureSortOptions
        {
            Order = ProcedureSortOrder.AlphabeticalAsc
        });

        var names = sorted.Select(p => p.Name).ToList();
        Assert.Equal(new[] { "Alpha", "Beta", "Beta", "Zeta" }, names);
    }

    [Fact]
    public void SortProcedures_AlphabeticalDesc_OrdersZToA()
    {
        var (_, procs) = ProcedureSorter.ParseProcedures(SampleCode);
        var sorted = ProcedureSorter.SortProcedures(procs, new ProcedureSortOptions
        {
            Order = ProcedureSortOrder.AlphabeticalDesc
        });

        var names = sorted.Select(p => p.Name).ToList();
        Assert.Equal(new[] { "Zeta", "Beta", "Beta", "Alpha" }, names);
    }

    [Fact]
    public void SortProcedures_ByType_GroupsPropertiesThenFunctionsThenSubs()
    {
        var (_, procs) = ProcedureSorter.ParseProcedures(SampleCode);
        var sorted = ProcedureSorter.SortProcedures(procs, new ProcedureSortOptions
        {
            Order = ProcedureSortOrder.ByType
        });

        Assert.StartsWith("Property", sorted[0].Kind);
        Assert.StartsWith("Property", sorted[1].Kind);
        Assert.Equal("Function", sorted[2].Kind);
        Assert.Equal("Sub", sorted[3].Kind);
    }

    [Fact]
    public void SortProcedures_ByScope_GroupsPublicBeforePrivate()
    {
        var (_, procs) = ProcedureSorter.ParseProcedures(SampleCode);
        var sorted = ProcedureSorter.SortProcedures(procs, new ProcedureSortOptions
        {
            Order = ProcedureSortOrder.ByScope
        });

        // Alpha es Private, los demás Public
        Assert.Equal("Public", sorted[0].Scope);
        Assert.Equal("Public", sorted[1].Scope);
        Assert.Equal("Public", sorted[2].Scope);
        Assert.Equal("Private", sorted[3].Scope);
        Assert.Equal("Alpha", sorted[3].Name);
    }

    [Fact]
    public void GenerateSortedCode_ProducesCleanCodeWithDeclarations()
    {
        var (header, procs) = ProcedureSorter.ParseProcedures(SampleCode);
        var sorted = ProcedureSorter.SortProcedures(procs, new ProcedureSortOptions
        {
            Order = ProcedureSortOrder.AlphabeticalAsc,
            PreserveComments = true
        });

        string fullCode = ProcedureSorter.GenerateSortedCode(header, sorted, preserveComments: true);

        Assert.StartsWith("Option Explicit", fullCode.TrimStart());
        int alphaIdx = fullCode.IndexOf("Function Alpha");
        int zetaIdx = fullCode.IndexOf("Sub Zeta");
        Assert.True(alphaIdx < zetaIdx);
        Assert.Contains("' Comentario de Zeta", fullCode);
    }
}
