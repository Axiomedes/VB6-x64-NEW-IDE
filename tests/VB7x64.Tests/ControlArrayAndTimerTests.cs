using System;
using System.Linq;
using System.Text.RegularExpressions;
using VB7x64.ProjectModel.Designers;
using VB7x64.ProjectModel.Execution;
using Xunit;

namespace VB7x64.Tests;

public class ControlArrayAndTimerTests
{
    [Fact]
    public void ControlArray_MultipleControlsWithSameNameAndDifferentIndex_AreAllowed()
    {
        var model = new FormDesignModel { Name = "Form1" };

        var btn0 = new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "cmdAction",
            Index = 0,
            Caption = "Opción 0"
        };
        var btn1 = new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "cmdAction",
            Index = 1,
            Caption = "Opción 1"
        };

        model.Children.Add(btn0);
        model.Children.Add(btn1);

        var array = model.GetControlArray("cmdAction");
        Assert.Equal(2, array.Count);
        Assert.Equal(0, array[0].Index);
        Assert.Equal(1, array[1].Index);

        var found0 = model.FindControl("cmdAction", 0);
        var found1 = model.FindControl("cmdAction", 1);
        Assert.NotNull(found0);
        Assert.NotNull(found1);
        Assert.Equal("Opción 0", found0.Caption);
        Assert.Equal("Opción 1", found1.Caption);
    }

    [Fact]
    public void ControlArray_SerializesAndParses_IndexCorrectly()
    {
        var model = new FormDesignModel { Name = "Form1" };

        model.Children.Add(new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "btnArray",
            Index = 0,
            Caption = "Primero"
        });

        model.Children.Add(new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "btnArray",
            Index = 1,
            Caption = "Segundo"
        });

        string serialized = FormDesignSerializer.Serialize(model);
        Assert.Contains("Index           =   0", serialized);
        Assert.Contains("Index           =   1", serialized);

        var reparsed = FormDesignParser.Parse(serialized);
        var reparsedArray = reparsed.GetControlArray("btnArray");
        Assert.Equal(2, reparsedArray.Count);
        Assert.Equal(0, reparsedArray[0].Index);
        Assert.Equal(1, reparsedArray[1].Index);
    }

    [Fact]
    public void TimerControl_OnlyHasEssentialPropertiesAndInterval()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var timer = new FormDesignerControl
        {
            TypeName = "VB.Timer",
            Name = "Timer1",
            Interval = 500,
            Enabled = true
        };
        model.Children.Add(timer);

        bool modelChanged = false;
        var props = DesignerPropertyFactory.GetPropertiesForControl(
            timer,
            model,
            () => modelChanged = true);

        // Debe contener Name, Index, Interval, Enabled, Left, Top, Tag
        var propNames = props.Select(p => p.Name).ToList();
        Assert.Contains("Name", propNames);
        Assert.Contains("Index", propNames);
        Assert.Contains("Interval", propNames);
        Assert.Contains("Enabled", propNames);
        Assert.Contains("Left", propNames);
        Assert.Contains("Top", propNames);
        Assert.Contains("Tag", propNames);

        // NO debe contener propiedades visuales innecesarias para un control no visible
        Assert.DoesNotContain("Width", propNames);
        Assert.DoesNotContain("Height", propNames);
        Assert.DoesNotContain("Visible", propNames);
        Assert.DoesNotContain("TabIndex", propNames);
        Assert.DoesNotContain("TabStop", propNames);
        Assert.DoesNotContain("BackColor", propNames);
        Assert.DoesNotContain("ForeColor", propNames);
        Assert.DoesNotContain("Font", propNames);
        Assert.DoesNotContain("Caption", propNames);
        Assert.DoesNotContain("Text", propNames);

        // Modificar Interval a través de la propiedad
        var intervalItem = props.First(p => p.Name == "Interval");
        Assert.Equal("500", intervalItem.CurrentValue);
        Assert.True(intervalItem.SetValue("1000"));
        Assert.Equal(1000, timer.Interval);
        Assert.True(modelChanged);
    }

    [Fact]
    public void TimerControl_SerializesAndParsesIntervalCorrectly()
    {
        var model = new FormDesignModel { Name = "Form1" };
        model.Children.Add(new FormDesignerControl
        {
            TypeName = "VB.Timer",
            Name = "Timer1",
            Interval = 250,
            Enabled = true
        });

        string serialized = FormDesignSerializer.Serialize(model);
        Assert.Contains("Interval        =   250", serialized);

        var reparsed = FormDesignParser.Parse(serialized);
        var reparsedTimer = reparsed.FindControl("Timer1");
        Assert.NotNull(reparsedTimer);
        Assert.Equal(250, reparsedTimer.Interval);
    }

    [Fact]
    public void RenameControlInCode_RenamesEventSubHeadersAndReferences()
    {
        string originalCode = @"
Private Sub Command1_Click()
    Command1.Caption = ""Pulsado""
    Call Actualizar(Command1)
End Sub

Private Sub Command1_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    Command1.ToolTipText = ""Ayuda""
End Sub
";
        string oldName = "Command1";
        string newName = "btnAceptar";

        // 1. Renombrar cabeceras de eventos
        string patternEvents = @"(?i)\b((?:(?:Private|Public|Friend|Static)\s+)?Sub\s+)" +
                               Regex.Escape(oldName) +
                               @"_([a-zA-Z0-9_]+)";

        string updatedCode = Regex.Replace(
            originalCode,
            patternEvents,
            $"$1{newName}_$2");

        // 2. Renombrar referencias al control
        string patternRefs = @"(?i)\b" + Regex.Escape(oldName) +
                             @"(?=\.|\(|\s*=|\s*\)|\s*,|\s*\z|\s*\+)";

        updatedCode = Regex.Replace(
            updatedCode,
            patternRefs,
            newName);

        Assert.Contains("Private Sub btnAceptar_Click()", updatedCode);
        Assert.Contains("Private Sub btnAceptar_MouseMove(Button As Integer", updatedCode);
        Assert.Contains("btnAceptar.Caption = \"Pulsado\"", updatedCode);
        Assert.Contains("Call Actualizar(btnAceptar)", updatedCode);
        Assert.Contains("btnAceptar.ToolTipText = \"Ayuda\"", updatedCode);
        Assert.DoesNotContain("Command1", updatedCode);
    }

    [Fact]
    public void ControlArray_DoubleClickedSignature_UsesIndexAsInteger()
    {
        var ctrl = new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "btnArray",
            Index = 0
        };

        string parameters = ctrl.Index.HasValue ? "(Index As Integer)" : "()";
        string codeSnippet = $"Private Sub {ctrl.Name}_Click{parameters}";

        Assert.Equal("Private Sub btnArray_Click(Index As Integer)", codeSnippet);
    }

    [Fact]
    public void ControlArray_RenameConfirmation_CreatesArrayWhenAccepted_AndRejectsWhenDeclined()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var btn1 = new FormDesignerControl { TypeName = "VB.CommandButton", Name = "Command1" };
        var btn2 = new FormDesignerControl { TypeName = "VB.CommandButton", Name = "Command2" };
        model.Children.Add(btn1);
        model.Children.Add(btn2);

        // Sin confirmación o cuando el usuario elige 'No' -> debe rechazar
        var props2No = DesignerPropertyFactory.GetPropertiesForControl(
            btn2,
            model,
            () => { },
            confirmCreateControlArray: _ => false);
        var nameProp2No = props2No.First(p => p.Name == "Name");
        Assert.False(nameProp2No.SetValue("Command1"));
        Assert.Equal("Command2", btn2.Name);
        Assert.Null(btn1.Index);
        Assert.Null(btn2.Index);

        // Cuando el usuario elige 'Sí' -> debe convertir btn1 en Command1(0) y btn2 en Command1(1)
        var props2Yes = DesignerPropertyFactory.GetPropertiesForControl(
            btn2,
            model,
            () => { },
            confirmCreateControlArray: _ => true);
        var nameProp2Yes = props2Yes.First(p => p.Name == "Name");
        Assert.True(nameProp2Yes.SetValue("Command1"));
        Assert.Equal("Command1", btn2.Name);
        Assert.Equal(0, btn1.Index);
        Assert.Equal(1, btn2.Index);
    }

    [Fact]
    public void ControlArray_ClickEvent_ReceivesCorrectIndex()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        string msgReceived = "";
        ctx.MsgBoxHandler = (prompt, buttons, title) =>
        {
            msgReceived = prompt;
            return 1;
        };

        string code = @"
Private Sub Command1_Click(Index As Integer)
    MsgBox ""Pulsado: "" & Index
End Sub
";
        host.LoadSourceCode(code, "Form1");

        // Clic en índice 0
        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(0));
        Assert.Equal("Pulsado: 0", msgReceived);

        // Clic en índice 2
        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(2));
        Assert.Equal("Pulsado: 2", msgReceived);
    }

    [Fact]
    public void ControlArray_ClickEvent_SelectCaseIndex_ExecutesCorrectBranch()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        string branchResult = "";
        ctx.MsgBoxHandler = (prompt, buttons, title) =>
        {
            branchResult = prompt;
            return 1;
        };

        string code = @"
Private Sub Command1_Click(Index As Integer)
    Select Case Index
        Case 0
            MsgBox ""Boton Cero""
        Case 1
            MsgBox ""Boton Uno""
        Case 2
            MsgBox ""Boton Dos""
    End Select
End Sub
";
        host.LoadSourceCode(code, "Form1");

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(0));
        Assert.Equal("Boton Cero", branchResult);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(1));
        Assert.Equal("Boton Uno", branchResult);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(2));
        Assert.Equal("Boton Dos", branchResult);
    }

    [Fact]
    public void ControlArray_ClickEvent_ElseIfBranch_ExecutesCorrectBranch()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        string branchResult = "";
        ctx.MsgBoxHandler = (prompt, buttons, title) =>
        {
            branchResult = prompt;
            return 1;
        };

        string code = @"
Private Sub Command1_Click(Index As Integer)
    If Index = 0 Then
        MsgBox ""Primer Boton""
    ElseIf Index = 1 Then
        MsgBox ""Segundo Boton""
    ElseIf Index = 2 Then
        MsgBox ""Tercer Boton""
    Else
        MsgBox ""Otro""
    End If
End Sub
";
        host.LoadSourceCode(code, "Form1");

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(0));
        Assert.Equal("Primer Boton", branchResult);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(1));
        Assert.Equal("Segundo Boton", branchResult);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(2));
        Assert.Equal("Tercer Boton", branchResult);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(99));
        Assert.Equal("Otro", branchResult);
    }

    [Fact]
    public void ControlArray_IndexedPropertyAssignment_ModifiesTargetElement()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        var btn0 = new MockElementControl("Command1");
        btn0.SetProperty("Caption", VbValue.FromString("Original 0"));
        var btn1 = new MockElementControl("Command1");
        btn1.SetProperty("Caption", VbValue.FromString("Original 1"));
        var btn2 = new MockElementControl("Command1");
        btn2.SetProperty("Caption", VbValue.FromString("Original 2"));

        var arr = new LiveControlArray("Command1");
        arr.Add(0, btn0);
        arr.Add(1, btn1);
        arr.Add(2, btn2);

        ctx.SetVariable("Command1", VbValue.FromObject(arr));

        string code = @"
Private Sub Command1_Click(Index As Integer)
    Command1(Index).Caption = ""Pulsado: "" & Index
End Sub
";
        host.LoadSourceCode(code, "Form1");

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(1));
        Assert.Equal("Original 0", btn0.GetProperty("Caption").AsString());
        Assert.Equal("Pulsado: 1", btn1.GetProperty("Caption").AsString());
        Assert.Equal("Original 2", btn2.GetProperty("Caption").AsString());

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(2));
        Assert.Equal("Pulsado: 2", btn2.GetProperty("Caption").AsString());
    }

    [Fact]
    public void ControlArray_SelectCase_RangesAndIsComparisons()
    {
        var ctx = new VbExecutionContext();
        var host = new LiveExecutionHost(ctx);

        string result = "";
        ctx.MsgBoxHandler = (prompt, buttons, title) =>
        {
            result = prompt;
            return 1;
        };

        string code = @"
Private Sub Command1_Click(Index As Integer)
    Select Case Index
        Case 0 To 1
            MsgBox ""Rango 0 a 1""
        Case Is >= 2
            MsgBox ""Mayor o igual a 2""
        Case Else
            MsgBox ""Otro""
    End Select
End Sub
";
        host.LoadSourceCode(code, "Form1");

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(0));
        Assert.Equal("Rango 0 a 1", result);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(1));
        Assert.Equal("Rango 0 a 1", result);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(5));
        Assert.Equal("Mayor o igual a 2", result);

        host.InvokeProcedure("Command1_Click", "Form1", VbValue.FromLong(-1));
        Assert.Equal("Otro", result);
    }

    private class MockElementControl : ILiveControl
    {
        public string Name { get; }
        public string TypeName => "CommandButton";
        private readonly System.Collections.Generic.Dictionary<string, VbValue> _props = new(StringComparer.OrdinalIgnoreCase);

        public MockElementControl(string name) => Name = name;

        public VbValue GetProperty(string propertyName) =>
            _props.TryGetValue(propertyName, out var v) ? v : VbValue.Empty;

        public void SetProperty(string propertyName, VbValue value) =>
            _props[propertyName] = value;

        public VbValue InvokeMethod(string methodName, VbValue[] args) => VbValue.Empty;
    }
}

