using System;
using System.Collections.Generic;
using VB7x64.ProjectModel.Execution;
using Xunit;

namespace VB7x64.Tests;

public class TimerAndMsgBoxTests
{
    [Fact]
    public void VbConstants_ProvidesIntrinsicConstants()
    {
        Assert.True(VbConstants.TryGetConstant("vbInformation", out var info));
        Assert.Equal(64, info.AsLong());

        Assert.True(VbConstants.TryGetConstant("vbCritical", out var crit));
        Assert.Equal(16, crit.AsLong());

        Assert.True(VbConstants.TryGetConstant("vbYesNo", out var yesNo));
        Assert.Equal(4, yesNo.AsLong());

        Assert.True(VbConstants.TryGetConstant("vbCrLf", out var crlf));
        Assert.Equal("\r\n", crlf.AsString());
    }

    [Fact]
    public void VbExpressionEvaluator_ResolvesBuiltInConstantsInExpressions()
    {
        var ctx = new VbExecutionContext();

        var val1 = VbExpressionEvaluator.Evaluate("vbInformation + vbYesNo", ctx);
        Assert.Equal(68, val1.AsLong());

        var val2 = VbExpressionEvaluator.Evaluate("\"Linea1\" & vbCrLf & \"Linea2\"", ctx);
        Assert.Equal("Linea1\r\nLinea2", val2.AsString());
    }

    [Fact]
    public void MsgBox_Statement_PassesOptionsAndTitleToHandler()
    {
        var ctx = new VbExecutionContext();
        string receivedPrompt = "";
        int receivedButtons = -1;
        string receivedTitle = "";

        ctx.MsgBoxHandler = (p, b, t) =>
        {
            receivedPrompt = p;
            receivedButtons = b;
            receivedTitle = t;
            return 1;
        };

        var interp = new VbStatementInterpreter(ctx);
        interp.ExecuteSingleStatement("MsgBox \"Operación completada\", vbInformation, \"Éxito\"");

        Assert.Equal("Operación completada", receivedPrompt);
        Assert.Equal(64, receivedButtons);
        Assert.Equal("Éxito", receivedTitle);
    }

    [Fact]
    public void MsgBox_WithParentheses_Statement_PassesOptionsCorrectly()
    {
        var ctx = new VbExecutionContext();
        string receivedPrompt = "";
        int receivedButtons = -1;
        string receivedTitle = "";

        ctx.MsgBoxHandler = (p, b, t) =>
        {
            receivedPrompt = p;
            receivedButtons = b;
            receivedTitle = t;
            return 1;
        };

        var interp = new VbStatementInterpreter(ctx);
        interp.ExecuteSingleStatement("MsgBox(\"¿Desea salir?\", vbQuestion + vbYesNo, \"Confirmación\")");

        Assert.Equal("¿Desea salir?", receivedPrompt);
        Assert.Equal(36, receivedButtons); // 32 (vbQuestion) + 4 (vbYesNo) = 36
        Assert.Equal("Confirmación", receivedTitle);
    }

    [Fact]
    public void MsgBox_AsFunction_EvaluatesAndReturnsUserResult()
    {
        var ctx = new VbExecutionContext();
        ctx.MsgBoxHandler = (p, b, t) => 6; // vbYes

        var interp = new VbStatementInterpreter(ctx);
        interp.ExecuteSingleStatement("Dim res As Integer");
        interp.ExecuteSingleStatement("res = MsgBox(\"¿Continuar?\", vbYesNo, \"Pregunta\")");

        var res = ctx.GetVariable("res");
        Assert.Equal(6, res.AsLong());
    }

    [Fact]
    public void ModuleDeclarations_VariablesPersistAcrossMultipleProcedureCalls()
    {
        var host = new LiveExecutionHost();

        string code = @"
Dim Contador As Long

Private Sub Timer1_Timer()
    Contador = Contador + 1
End Sub
";
        host.LoadSourceCode(code, "Form1");

        // Simular múltiples ticks del Timer
        host.InvokeProcedure("Timer1_Timer", "Form1");
        Assert.Equal(1, host.Context.GetVariable("Contador").AsLong());

        host.InvokeProcedure("Timer1_Timer", "Form1");
        Assert.Equal(2, host.Context.GetVariable("Contador").AsLong());

        host.InvokeProcedure("Timer1_Timer", "Form1");
        Assert.Equal(3, host.Context.GetVariable("Contador").AsLong());
    }

    [Fact]
    public void StaticVariables_PersistAcrossMultipleProcedureCalls()
    {
        var host = new LiveExecutionHost();

        string code = @"
Private Sub Timer1_Timer()
    Static Contador As Long
    Contador = Contador + 1
End Sub
";
        host.LoadSourceCode(code, "Form1");

        host.InvokeProcedure("Timer1_Timer", "Form1");
        string staticKey = VbExecutionContext.GetStaticVarKey("Timer1_Timer", "Contador");
        Assert.Equal(1, host.Context.StaticVariables[staticKey].AsLong());

        host.InvokeProcedure("Timer1_Timer", "Form1");
        Assert.Equal(2, host.Context.StaticVariables[staticKey].AsLong());

        host.InvokeProcedure("Timer1_Timer", "Form1");
        Assert.Equal(3, host.Context.StaticVariables[staticKey].AsLong());
    }
}
