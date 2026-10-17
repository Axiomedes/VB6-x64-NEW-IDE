using System;
using System.Collections.Generic;
using ViBa7.ProjectModel.Execution;
using Xunit;

namespace ViBa7.Tests;

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

    [Fact]
    public void MsgBox_WithSpaceBeforeParentheses_Statement_PassesOptions()
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
        // "MsgBox ("Hola", vbCritical, "Alerta")" con espacio antes del paréntesis
        interp.ExecuteSingleStatement("MsgBox (\"Hola\", vbCritical, \"Alerta\")");

        Assert.Equal("Hola", receivedPrompt);
        Assert.Equal(16, receivedButtons);
        Assert.Equal("Alerta", receivedTitle);
    }

    [Fact]
    public void MsgBox_UppercaseStatement_And_UppercaseFunctionAssignment()
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
            return 6; // vbYes
        };

        var interp = new VbStatementInterpreter(ctx);

        // Caso 1: MSGBOX opcion1, opcion2, opcion3 (sin paréntesis, sin retorno)
        interp.ExecuteSingleStatement("MSGBOX \"Guardar archivo\", vbInformation, \"IDE\"");
        Assert.Equal("Guardar archivo", receivedPrompt);
        Assert.Equal(64, receivedButtons);
        Assert.Equal("IDE", receivedTitle);

        // Caso 2: val = MSGBOX(opcion1, opcion2, opcion3) (con paréntesis, con retorno)
        interp.ExecuteSingleStatement("Dim val As Long");
        interp.ExecuteSingleStatement("val = MSGBOX(\"¿Confirmar?\", vbYesNo, \"Confirmación\")");
        Assert.Equal("¿Confirmar?", receivedPrompt);
        Assert.Equal(4, receivedButtons);
        Assert.Equal("Confirmación", receivedTitle);
        Assert.Equal(6, ctx.GetVariable("val").AsLong());
    }

    [Fact]
    public void IIf_Function_EvaluatesConditionsCorrectly()
    {
        var ctx = new VbExecutionContext();
        ctx.SetVariable("a", VbValue.FromLong(10));

        var res1 = VbExpressionEvaluator.Evaluate("IIf(a > 5, \"Mayor\", \"Menor\")", ctx);
        Assert.Equal("Mayor", res1.AsString());

        var res2 = VbExpressionEvaluator.Evaluate("IIf(a = 0, 100, 200)", ctx);
        Assert.Equal(200, res2.AsLong());
    }

    [Fact]
    public void IntelliSense_ProvidesEnumConstants_ForMsgBoxButtonsParameter()
    {
        var constants = ViBa7.ProjectModel.IntelliSense.VbRuntimeCatalog.GetConstantsForType("VbMsgBoxStyle");
        Assert.NotEmpty(constants);
        Assert.Contains(constants, c => c.Name.Equals("vbOKOnly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(constants, c => c.Name.Equals("vbCritical", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(constants, c => c.Name.Equals("vbInformation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(constants, c => c.Name.Equals("vbYesNo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(constants, c => c.Name.Equals("vbYesNoCancel", StringComparison.OrdinalIgnoreCase));
    }
}
