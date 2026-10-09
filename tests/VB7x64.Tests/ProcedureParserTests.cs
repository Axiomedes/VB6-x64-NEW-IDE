using System.Linq;
using VB7x64.ProjectModel.Navigation;
using Xunit;

namespace VB7x64.Tests;

public class ProcedureParserTests
{
    [Fact]
    public void ParseProcedures_FindsSubsFunctionsAndProperties()
    {
        string code = @"Attribute VB_Name = ""ModDemo""
Option Explicit

' Comentario antes de Sub
Public Sub Saludar()
    MsgBox ""Hola""
End Sub

Private Function Sumar(ByVal a As Long, ByVal b As Long) As Long
    Sumar = a + b
End Function

Public Property Get Edad() As Integer
    Edad = 25
End Property

Public Property Let Edad(ByVal v As Integer)
End Property

' Sub comentado: no debe detectarse
' Sub NoEsSub()
";

        var procedures = ProcedureParser.ParseProcedures(code);

        Assert.Equal(4, procedures.Count);

        var subItem = procedures.First(p => p.Name == "Saludar");
        Assert.Equal(ProcedureKind.Sub, subItem.Kind);
        Assert.Equal("Public", subItem.Scope);

        var funcItem = procedures.First(p => p.Name == "Sumar");
        Assert.Equal(ProcedureKind.Function, funcItem.Kind);
        Assert.Equal("Private", funcItem.Scope);

        var propGet = procedures.First(p => p.Name == "Edad" && p.Kind == ProcedureKind.PropertyGet);
        Assert.NotNull(propGet);

        var propLet = procedures.First(p => p.Name == "Edad" && p.Kind == ProcedureKind.PropertyLet);
        Assert.NotNull(propLet);

        // Sub comentado no debe existir
        Assert.DoesNotContain(procedures, p => p.Name == "NoEsSub");
    }
}
