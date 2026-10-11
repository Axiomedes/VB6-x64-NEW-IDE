using System;
using System.Collections.Generic;
using VB7x64.ProjectModel.Execution;
using Xunit;

namespace VB7x64.Tests;

public class LiveExecutionTests
{
    private class MockLiveControl : ILiveControl
    {
        public string Name { get; }
        public string TypeName { get; }
        private readonly Dictionary<string, VbValue> _props = new(StringComparer.OrdinalIgnoreCase);

        public MockLiveControl(string name, string typeName)
        {
            Name = name;
            TypeName = typeName;
        }

        public VbValue GetProperty(string propertyName)
        {
            return _props.TryGetValue(propertyName, out var val) ? val : VbValue.Empty;
        }

        public void SetProperty(string propertyName, VbValue value)
        {
            _props[propertyName] = value;
        }

        public VbValue InvokeMethod(string methodName, VbValue[] args)
        {
            if (methodName.Equals("Clear", StringComparison.OrdinalIgnoreCase))
            {
                _props.Clear();
                return VbValue.Empty;
            }
            return VbValue.Empty;
        }
    }

    private class MockLiveForm : MockLiveControl, ILiveForm
    {
        private readonly Dictionary<string, ILiveControl> _controls = new(StringComparer.OrdinalIgnoreCase);

        public MockLiveForm(string name) : base(name, "Form") { }

        public IReadOnlyDictionary<string, ILiveControl> Controls => _controls;

        public void AddControl(ILiveControl ctrl) => _controls[ctrl.Name] = ctrl;

        public ILiveControl? FindControl(string name) => _controls.TryGetValue(name, out var c) ? c : null;
    }

    [Fact]
    public void VbValue_ArithmeticAndCoercion_FollowsVb6Semantics()
    {
        var v1 = VbValue.FromString("100");
        var v2 = VbValue.FromLong(25);

        // Suma con coerción numérica ("100" + 25 = 125)
        var sum = v1 + v2;
        Assert.Equal(125.0, sum.AsDouble());

        // Concatenación forzada (&)
        var concat = VbValue.Concat(v1, v2);
        Assert.Equal("10025", concat.AsString());

        // Operadores de división entera y módulo
        var divInt = VbValue.IntegerDivide(VbValue.FromLong(17), VbValue.FromLong(5));
        Assert.Equal(3, divInt.AsLong());

        var mod = VbValue.Modulo(VbValue.FromLong(17), VbValue.FromLong(5));
        Assert.Equal(2, mod.AsLong());

        // Lógicos en VB6 (True = -1)
        Assert.Equal(-1, VbValue.True.AsLong());
        Assert.Equal(0, VbValue.False.AsLong());

        var notTrue = VbValue.VbNot(VbValue.True);
        Assert.Equal(VbValue.False, notTrue);
    }

    [Fact]
    public void VbRuntimeFunctions_ExecuteAccurately()
    {
        // Cadenas
        Assert.Equal("Basic", VbRuntimeFunctions.Mid(VbValue.FromString("Visual Basic"), VbValue.FromLong(8), VbValue.FromLong(5)).AsString());
        Assert.Equal("Visual", VbRuntimeFunctions.Left(VbValue.FromString("Visual Basic"), VbValue.FromLong(6)).AsString());
        Assert.Equal("Basic", VbRuntimeFunctions.Right(VbValue.FromString("Visual Basic"), VbValue.FromLong(5)).AsString());
        Assert.Equal(8, VbRuntimeFunctions.InStr(VbValue.FromString("Visual Basic"), VbValue.FromString("Basic")).AsLong());
        Assert.Equal("HOLA", VbRuntimeFunctions.UCase(VbValue.FromString("hola")).AsString());
        Assert.Equal(3, VbRuntimeFunctions.Len(VbValue.FromString("VB6")).AsLong());

        // Conversión
        Assert.Equal(123.45, VbRuntimeFunctions.Val(VbValue.FromString("  123.45 kg")).AsDouble());
        Assert.Equal(3.14, VbRuntimeFunctions.Round(VbValue.FromDouble(3.14159), VbValue.FromLong(2)).AsDouble());
    }

    [Fact]
    public void VbExpressionEvaluator_CalculatesComplexExpressions_WithPrecedence()
    {
        var ctx = new VbExecutionContext();
        ctx.SetVariable("a", VbValue.FromLong(5));
        ctx.SetVariable("b", VbValue.FromLong(10));

        // Precedencia básica: 5 + 10 * 2 = 25
        var val1 = VbExpressionEvaluator.Evaluate("a + b * 2", ctx);
        Assert.Equal(25.0, val1.AsDouble());

        // Paréntesis: (5 + 10) * 2 = 30
        var val2 = VbExpressionEvaluator.Evaluate("(a + b) * 2", ctx);
        Assert.Equal(30.0, val2.AsDouble());

        // Funciones intrínsecas: UCase("vb") & " " & (a * 2) = "VB 10"
        var val3 = VbExpressionEvaluator.Evaluate("UCase(\"vb\") & \" \" & (a * 2)", ctx);
        Assert.Equal("VB 10", val3.AsString());

        // Comparación relacional: (a < b) And (b = 10) = True
        var val4 = VbExpressionEvaluator.Evaluate("(a < b) And (b = 10)", ctx);
        Assert.True(val4.AsBoolean());
    }

    [Fact]
    public void LiveExecutionHost_ExecutesProcedures_AndModifiesControlsInLive()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        // Crear formulario simulado en vivo con un botón y un texto
        var form = new MockLiveForm("Form1");
        var txt = new MockLiveControl("Text1", "TextBox");
        txt.SetProperty("Text", VbValue.FromString("Inicial"));
        form.AddControl(txt);

        ctx.ActiveForms["Form1"] = form;

        // Código VB6 simulando el evento de un botón
        string code = @"
Sub Command1_Click()
    Dim i As Integer
    Dim suma As Long
    suma = 0
    For i = 1 To 5
        suma = suma + i
    Next i
    Text1.Text = ""Resultado: "" & suma
    Debug.Print ""Calculo terminado:"" & suma
End Sub
";

        host.LoadSourceCode(code, "Form1");

        string debugOutput = string.Empty;
        ctx.OnDebugPrint += msg => debugOutput = msg;

        // Invocar evento en caliente
        var sig = host.InvokeProcedure("Command1_Click", "Form1");

        Assert.Equal(FlowSignal.Normal, sig);
        Assert.Equal("Resultado: 15", txt.GetProperty("Text").AsString());
        Assert.Contains("15", debugOutput);
    }

    [Fact]
    public void LiveExecutionHost_EvaluatesImmediateWindowCommands()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        ctx.SetVariable("precio", VbValue.FromDouble(100.0));
        ctx.SetVariable("iva", VbValue.FromDouble(0.21));

        // Comando inmediato: `? precio * (1 + iva)`
        var res1 = host.EvaluateImmediate("? precio * (1 + iva)");
        Assert.Equal(121.0, res1.AsDouble());

        // Comando directo de asignación: `precio = 200`
        host.EvaluateImmediate("precio = 200");
        var res2 = host.EvaluateImmediate("? precio");
        Assert.Equal(200.0, res2.AsDouble());
    }

    [Fact]
    public void VbStatementInterpreter_HandlesIfElseAndLoops()
    {
        var ctx = new VbExecutionContext();
        var interp = new VbStatementInterpreter(ctx);

        // Bloque multilínea con If y Do While
        var lines = new List<string>
        {
            "Dim x As Integer",
            "Dim acumulador As Integer",
            "x = 1",
            "acumulador = 0",
            "Do While x <= 3",
            "    If x = 2 Then",
            "        acumulador = acumulador + 10",
            "    Else",
            "        acumulador = acumulador + 1",
            "    End If",
            "    x = x + 1",
            "Loop"
        };

        var sig = interp.ExecuteBlock(lines);

        Assert.Equal(FlowSignal.Normal, sig);
        // x=1: acumulador=1; x=2: acumulador=1+10=11; x=3: acumulador=11+1=12; fin bucle con x=4
        Assert.Equal(12, ctx.GetVariable("acumulador").AsLong());
        Assert.Equal(4, ctx.GetVariable("x").AsLong());
    }

    [Fact]
    public void VbStatementInterpreter_HandlesOnErrorResumeNext_AndErrObject()
    {
        var ctx = new VbExecutionContext();
        var interp = new VbStatementInterpreter(ctx);

        var lines = new List<string>
        {
            "On Error Resume Next",
            "Dim n As Integer",
            "n = 10 / 0",
            "Dim despues As Boolean",
            "despues = True"
        };

        var sig = interp.ExecuteBlock(lines);

        Assert.Equal(FlowSignal.Normal, sig);
        Assert.True(ctx.GetVariable("despues").AsBoolean());
    }

    [Fact]
    public void LiveExecutionHost_HandlesSubroutines_AndExitSub()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        string code = @"
Sub Calcular(param As Long)
    Dim resultado As Long
    resultado = param * 2
    If resultado > 50 Then
        Exit Sub
    End If
    resultado = resultado + 5
    param = resultado
End Sub
";
        host.LoadSourceCode(code, "Modulo1");

        // Caso 1: param = 10 -> resultado = 20 <= 50 -> resultado = 25
        var p1 = VbValue.FromLong(10);
        var sig1 = host.InvokeProcedure("Calcular", "Modulo1", p1);
        Assert.Equal(FlowSignal.Normal, sig1);

        // Caso 2: param = 30 -> resultado = 60 > 50 -> Exit Sub disparado
        var p2 = VbValue.FromLong(30);
        var sig2 = host.InvokeProcedure("Calcular", "Modulo1", p2);
        Assert.Equal(FlowSignal.ExitSub, sig2);
    }
}
