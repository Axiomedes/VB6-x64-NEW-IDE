using System;
using System.Linq;
using ViBa7.ProjectModel.Analysis;
using Xunit;

namespace ViBa7.Tests;

public class CodeAnalyzerTests
{
    [Fact]
    public void AnalyzeText_DetectsMissingOptionExplicit()
    {
        string code = @"Sub DoSomething()
    x = 10
End Sub";

        var issues = CodeAnalyzer.AnalyzeText(code, "Module1.bas");

        Assert.Contains(issues, i => i.RuleId == "VB7_W003_NO_OPTION_EXPLICIT");
    }

    [Fact]
    public void AnalyzeText_DoesNotWarnWhenOptionExplicitPresent()
    {
        string code = @"Option Explicit

Sub DoSomething()
    Dim x As Integer
    x = 10
    Call DoSomethingElse(x)
End Sub

Private Sub DoSomethingElse(val As Integer)
    Dim y As Integer
    y = val * 2
    MsgBox y
End Sub";

        var issues = CodeAnalyzer.AnalyzeText(code, "Module1.bas");

        Assert.DoesNotContain(issues, i => i.RuleId == "VB7_W003_NO_OPTION_EXPLICIT");
    }

    [Fact]
    public void AnalyzeText_DetectsUnusedVariables()
    {
        string code = @"Option Explicit

Sub Calculate()
    Dim usedVar As Integer
    Dim orphanVar As String
    usedVar = 100
    MsgBox usedVar
End Sub";

        var issues = CodeAnalyzer.AnalyzeText(code, "Module1.bas");

        Assert.Contains(issues, i => i.RuleId == "VB7_W002_UNUSED_VAR" && i.Message.Contains("orphanVar"));
        Assert.DoesNotContain(issues, i => i.RuleId == "VB7_W002_UNUSED_VAR" && i.Message.Contains("usedVar"));
    }

    [Fact]
    public void AnalyzeText_DetectsDeadCode_PrivateUnusedProcedures()
    {
        string code = @"Option Explicit

Private Sub UnusedHelper()
    MsgBox ""I am never called!""
End Sub

Private Sub UsedHelper()
    MsgBox ""I am called!""
End Sub

Public Sub Main()
    Call UsedHelper()
End Sub

Private Sub Form_Load()
    ' Event handler, not dead code
End Sub";

        var issues = CodeAnalyzer.AnalyzeText(code, "frmMain.frm");

        // UnusedHelper debe ser advertido
        Assert.Contains(issues, i => i.RuleId == "VB7_W001_DEAD_CODE" && i.ContainerName == "UnusedHelper");
        // UsedHelper no debe ser advertido
        Assert.DoesNotContain(issues, i => i.RuleId == "VB7_W001_DEAD_CODE" && i.ContainerName == "UsedHelper");
        // Form_Load es un event handler estándar, no debe ser advertido como dead code
        Assert.DoesNotContain(issues, i => i.RuleId == "VB7_W001_DEAD_CODE" && i.ContainerName == "Form_Load");
    }

    [Fact]
    public void AnalyzeText_DetectsEmptyProcedures()
    {
        string code = @"Option Explicit

Sub DoNothing()
    ' solo comentarios
End Sub

Sub DoSomething()
    MsgBox ""Hello""
End Sub";

        var issues = CodeAnalyzer.AnalyzeText(code, "Module1.bas");

        Assert.Contains(issues, i => i.RuleId == "VB7_I001_EMPTY_PROC" && i.ContainerName == "DoNothing");
        Assert.DoesNotContain(issues, i => i.RuleId == "VB7_I001_EMPTY_PROC" && i.ContainerName == "DoSomething");
    }
}
