using System;
using System.Linq;
using System.Text.RegularExpressions;
using VB7x64.ProjectModel.Designers;
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
}
