using System;
using System.Linq;
using ViBa7.ProjectModel.Designers;
using Xunit;

namespace ViBa7.Tests;

public class FormDesignerPropertyTests
{
    [Fact]
    public void ControlToolboxRegistry_GetAllTools_ContainsIntrinsicVB6Controls()
    {
        var tools = ControlToolboxRegistry.GetAllTools();
        Assert.NotEmpty(tools);

        var toolNames = tools.Select(t => t.TypeName).ToList();
        Assert.Contains(ControlToolboxRegistry.PointerTool, toolNames);
        Assert.Contains("VB.CommandButton", toolNames);
        Assert.Contains("VB.TextBox", toolNames);
        Assert.Contains("VB.Label", toolNames);
        Assert.Contains("VB.CheckBox", toolNames);
        Assert.Contains("VB.OptionButton", toolNames);
        Assert.Contains("VB.Frame", toolNames);
        Assert.Contains("VB.ListBox", toolNames);
        Assert.Contains("VB.ComboBox", toolNames);
        Assert.Contains("VB.Timer", toolNames);
    }

    [Fact]
    public void ControlToolboxRegistry_CreateControl_GeneratesSequentialNamesAndDefaults()
    {
        var model = new FormDesignModel { Name = "Form1" };

        var btn1 = ControlToolboxRegistry.CreateControl("VB.CommandButton", model, leftTwips: 120, topTwips: 240);
        model.Children.Add(btn1);

        Assert.Equal("Command1", btn1.Name);
        Assert.Equal("Command1", btn1.Caption);
        Assert.Equal(120, btn1.Left);
        Assert.Equal(240, btn1.Top);
        Assert.True(btn1.Width > 0);
        Assert.True(btn1.Height > 0);

        var btn2 = ControlToolboxRegistry.CreateControl("VB.CommandButton", model, leftTwips: 300, topTwips: 500);
        model.Children.Add(btn2);

        Assert.Equal("Command2", btn2.Name);
        Assert.Equal("Command2", btn2.Caption);

        var txt1 = ControlToolboxRegistry.CreateControl("VB.TextBox", model, leftTwips: 500, topTwips: 600);
        model.Children.Add(txt1);

        Assert.Equal("Text1", txt1.Name);
        Assert.Equal("Text1", txt1.Text);
    }

    [Fact]
    public void DesignerPropertyFactory_FormProperties_UpdatesModelCorrectly()
    {
        var model = new FormDesignModel { Name = "Form1", Caption = "Initial Form" };
        bool changedCalled = false;

        var props = DesignerPropertyFactory.GetPropertiesForForm(model, () => changedCalled = true);
        Assert.NotEmpty(props);

        var captionProp = props.First(p => p.Name == "Caption");
        Assert.Equal("Initial Form", captionProp.CurrentValue);

        bool success = captionProp.SetValue("New Form Caption");
        Assert.True(success);
        Assert.Equal("New Form Caption", model.Caption);
        Assert.True(changedCalled);

        var backColorProp = props.First(p => p.Name == "BackColor");
        backColorProp.SetValue("&H00FF0000&");
        Assert.Equal("&H00FF0000&", model.BackColorHex);
    }

    [Fact]
    public void DesignerPropertyFactory_ControlProperties_UpdatesAndValidatesValues()
    {
        var model = new FormDesignModel { Name = "Form1" };
        var btn = ControlToolboxRegistry.CreateControl("VB.CommandButton", model, 100, 200);
        model.Children.Add(btn);

        bool changed = false;
        var props = DesignerPropertyFactory.GetPropertiesForControl(btn, model, () => changed = true);

        // Modificar Name
        var nameProp = props.First(p => p.Name == "Name");
        Assert.Equal("Command1", nameProp.CurrentValue);
        Assert.True(nameProp.SetValue("cmdAceptar"));
        Assert.Equal("cmdAceptar", btn.Name);
        Assert.True(changed);

        // Modificar Left / Top
        var leftProp = props.First(p => p.Name == "Left");
        Assert.True(leftProp.SetValue("1440"));
        Assert.Equal(1440, btn.Left);

        // Modificar Enabled (booleano)
        var enabledProp = props.First(p => p.Name == "Enabled");
        Assert.True(enabledProp.SetValue("False"));
        Assert.False(btn.Enabled);

        // Validar no duplicidad de nombres
        // Como btn1 fue renombrado a 'cmdAceptar', el prefijo 'Command1' queda libre para btn2
        var btn2 = ControlToolboxRegistry.CreateControl("VB.CommandButton", model, 200, 300);
        Assert.Equal("Command1", btn2.Name);
        model.Children.Add(btn2);

        var props2 = DesignerPropertyFactory.GetPropertiesForControl(btn2, model, () => changed = true);
        var nameProp2 = props2.First(p => p.Name == "Name");

        // Intentar nombrar btn2 como cmdAceptar (ya existente) debe ser rechazado
        Assert.False(nameProp2.SetValue("cmdAceptar"));
        Assert.Equal("Command1", btn2.Name);
    }
}
