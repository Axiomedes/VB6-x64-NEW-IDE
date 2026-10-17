using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.Vbp;
using Xunit;

namespace ViBa7.Tests;

public class Phase2UserControlTests : IDisposable
{
    private readonly string _tempDir;

    public Phase2UserControlTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VB7_Phase2Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Limpieza opcional
        }
    }

    [Fact]
    public void UserControlPropertyExtractor_Extracts_Public_Properties_And_Enums()
    {
        string ctlCode = @"
Option Explicit

Public Enum ButtonTypes
    [Windows 16-bit] = 1
    [Windows 32-bit] = 2
    [Windows XP] = 3
    [Mac] = 4
End Enum

Public Property Get BackColor() As OLE_COLOR
    BackColor = m_BackColor
End Property

Public Property Let BackColor(ByVal theCol As OLE_COLOR)
    m_BackColor = theCol
End Property

Public Property Get ButtonType() As ButtonTypes
    ButtonType = m_ButtonType
End Property

Public Property Let ButtonType(ByVal NewValue As ButtonTypes)
    m_ButtonType = NewValue
End Property

Public Property Get Caption() As String
    Caption = m_Caption
End Property

Public Property Let Caption(ByVal NewValue As String)
    m_Caption = NewValue
End Property

Public Property Get CheckBoxBehaviour() As Boolean
    CheckBoxBehaviour = m_CheckBox
End Property

Public Property Let CheckBoxBehaviour(ByVal NewValue As Boolean)
    m_CheckBox = NewValue
End Property

Private Sub UserControl_ReadProperties(PropBag As PropertyBag)
    m_ButtonType = PropBag.ReadProperty(""ButtonType"", 2)
    m_Caption = PropBag.ReadProperty(""Caption"", ""Boton1"")
    m_BackColor = PropBag.ReadProperty(""BackColor"", &H8000000F&)
    m_CheckBox = PropBag.ReadProperty(""CheckBoxBehaviour"", False)
End Sub
";

        var properties = UserControlPropertyExtractor.ExtractProperties(ctlCode);

        Assert.NotNull(properties);
        Assert.Equal(4, properties.Count);

        // 1. BackColor (OLE_COLOR -> ColorHex)
        var backColorProp = properties.FirstOrDefault(p => p.Name == "BackColor");
        Assert.NotNull(backColorProp);
        Assert.Equal("OLE_COLOR", backColorProp.TypeName);
        Assert.Equal(DesignerPropertyType.ColorHex, backColorProp.InferredType);
        Assert.Equal("&H8000000F&", backColorProp.DefaultValue);

        // 2. ButtonType (Enum ButtonTypes -> EnumChoice con 4 opciones)
        var btnTypeProp = properties.FirstOrDefault(p => p.Name == "ButtonType");
        Assert.NotNull(btnTypeProp);
        Assert.Equal("ButtonTypes", btnTypeProp.TypeName);
        Assert.Equal(DesignerPropertyType.EnumChoice, btnTypeProp.InferredType);
        Assert.Equal("2", btnTypeProp.DefaultValue);
        Assert.Equal(4, btnTypeProp.Choices.Count);
        Assert.Contains(btnTypeProp.Choices, c => c.RawValue == "1" && c.DisplayText.Contains("Windows 16-bit"));
        Assert.Contains(btnTypeProp.Choices, c => c.RawValue == "2" && c.DisplayText.Contains("Windows 32-bit"));
        Assert.Contains(btnTypeProp.Choices, c => c.RawValue == "3" && c.DisplayText.Contains("Windows XP"));
        Assert.Contains(btnTypeProp.Choices, c => c.RawValue == "4" && c.DisplayText.Contains("Mac"));

        // 3. Caption (String -> Text)
        var captionProp = properties.FirstOrDefault(p => p.Name == "Caption");
        Assert.NotNull(captionProp);
        Assert.Equal("String", captionProp.TypeName);
        Assert.Equal(DesignerPropertyType.Text, captionProp.InferredType);
        Assert.Equal("Boton1", captionProp.DefaultValue);

        // 4. CheckBoxBehaviour (Boolean -> Boolean)
        var checkProp = properties.FirstOrDefault(p => p.Name == "CheckBoxBehaviour");
        Assert.NotNull(checkProp);
        Assert.Equal("Boolean", checkProp.TypeName);
        Assert.Equal(DesignerPropertyType.Boolean, checkProp.InferredType);
        Assert.Equal("False", checkProp.DefaultValue);
    }

    [Fact]
    public void FormDesignParser_Parses_ControlContainer_And_Nested_Controls()
    {
        string ctlSource = @"VERSION 5.00
Begin VB.UserControl PanelBox 
   ClientHeight    =   3495
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   4890
   ControlContainer=   -1  'True
   DefaultCancel   =   -1  'True
   Begin VB.PictureBox pClose 
      Height          =   300
      Left            =   4485
      Top             =   120
      Width           =   300
   End
End
";
        var ucModel = FormDesignParser.Parse(ctlSource);
        Assert.True(ucModel.IsUserControl);
        Assert.True(ucModel.ControlContainer);
        Assert.Single(ucModel.Children);
        Assert.Equal("pClose", ucModel.Children[0].Name);

        // Formulario que contiene una instancia de PanelBox y controles anidados dentro de él
        string frmSource = @"VERSION 5.00
Begin VB.Form Form1 
   Caption         =   ""Test Form""
   ClientHeight    =   4000
   ClientWidth     =   6000
   Begin Proyecto1.PanelBox Panel1 
      Height          =   2500
      Left            =   300
      Top             =   300
      Width           =   4000
      ControlContainer=   -1  'True
      Begin VB.CommandButton cmdAceptar 
         Caption         =   ""Aceptar""
         Height          =   495
         Left            =   500
         Top             =   600
         Width           =   1215
      End
      Begin VB.Label lblTitulo 
         Caption         =   ""Encabezado""
         Height          =   300
         Left            =   500
         Top             =   1200
         Width           =   1500
      End
   End
End
";
        var frmModel = FormDesignParser.Parse(frmSource);
        Assert.Single(frmModel.Children);

        var panel1 = frmModel.Children[0];
        Assert.Equal("Panel1", panel1.Name);
        Assert.True(panel1.ControlContainer);
        Assert.True(panel1.IsContainer);
        Assert.Equal(2, panel1.Children.Count);

        var cmd = panel1.Children[0];
        Assert.Equal("cmdAceptar", cmd.Name);
        Assert.Same(panel1, cmd.ParentControl);

        var lbl = panel1.Children[1];
        Assert.Equal("lblTitulo", lbl.Name);
        Assert.Same(panel1, lbl.ParentControl);
    }

    [Fact]
    public void FormDesignerControl_IsContainer_Uses_Resolver()
    {
        var ctrl = new FormDesignerControl
        {
            TypeName = "MiProyecto.CustomContainer",
            Name = "Contenedor1"
        };

        // Sin resolver configurado
        Assert.False(ctrl.IsContainer);

        // Con resolver que identifica el control como contenedor
        FormDesignerControl.IsUserControlContainerResolver = typeName =>
            string.Equals(typeName, "MiProyecto.CustomContainer", StringComparison.OrdinalIgnoreCase);

        try
        {
            Assert.True(ctrl.IsContainer);
        }
        finally
        {
            FormDesignerControl.IsUserControlContainerResolver = null;
        }
    }

    [Fact]
    public void DesignerPropertyFactory_Integrates_Exposed_And_Custom_Properties()
    {
        var exposedList = new List<UserControlExposedProperty>
        {
            new UserControlExposedProperty
            {
                Name = "ThemeColor",
                TypeName = "OLE_COLOR",
                DefaultValue = "&H00FF0000&"
            },
            new UserControlExposedProperty
            {
                Name = "RoundedCorners",
                TypeName = "Boolean",
                DefaultValue = "True"
            }
        };

        DesignerPropertyFactory.UserControlPropertiesResolver = typeName =>
            typeName.EndsWith("CustomButton", StringComparison.OrdinalIgnoreCase) ? exposedList : null;

        try
        {
            var ctrl = new FormDesignerControl
            {
                TypeName = "MiLibreria.CustomButton",
                Name = "btnGuardar",
                Caption = "Guardar",
                Width = 2000,
                Height = 600
            };
            // Añadir una propiedad personalizada ya persistida en el .frm
            ctrl.CustomProperties.Add(new FormPropertyItem("SpecialTag", "VIP_USER"));

            bool modified = false;
            var propList = DesignerPropertyFactory.CreateForControl(ctrl, () => modified = true);

            // Verificar que contiene las propiedades expuestas del UserControl
            var themeProp = propList.FirstOrDefault(p => p.Name == "ThemeColor");
            Assert.NotNull(themeProp);
            Assert.Equal("UserControl", themeProp.Category);
            Assert.Equal(DesignerPropertyType.ColorHex, themeProp.Type);
            Assert.Equal("&H00FF0000&", themeProp.CurrentValue);

            var roundProp = propList.FirstOrDefault(p => p.Name == "RoundedCorners");
            Assert.NotNull(roundProp);
            Assert.Equal(DesignerPropertyType.Boolean, roundProp.Type);
            Assert.Equal("True", roundProp.CurrentValue);

            // Verificar que contiene la propiedad Custom/OLE adicional
            var tagProp = propList.FirstOrDefault(p => p.Name == "SpecialTag");
            Assert.NotNull(tagProp);
            Assert.Equal("Custom / OLE", tagProp.Category);
            Assert.Equal("VIP_USER", tagProp.CurrentValue);

            // Modificar el valor de ThemeColor a través del PropertyGrid
            bool setOk = themeProp.SetValue("&H0000FF00&");
            Assert.True(setOk);
            Assert.True(modified);

            // Comprobar que se guardó en CustomProperties del control para serialización en .frm
            var savedCustom = ctrl.CustomProperties.FirstOrDefault(p => p.Name == "ThemeColor");
            Assert.NotNull(savedCustom);
            Assert.Equal("&H0000FF00&", savedCustom.Value);
        }
        finally
        {
            DesignerPropertyFactory.UserControlPropertiesResolver = null;
        }
    }

    [Fact]
    public void FormDesignSerializer_Serializes_Nested_UserControl_Hierarchy()
    {
        var model = new FormDesignModel
        {
            Name = "frmPrueba",
            Caption = "Prueba de Contenedores"
        };

        var panel = new FormDesignerControl
        {
            TypeName = "Proyecto1.PanelBox",
            Name = "pnlArea",
            Left = 100,
            Top = 100,
            Width = 3000,
            Height = 2000,
            ControlContainer = true
        };
        model.Children.Add(panel);

        var btn = new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "cmdAccion",
            Caption = "Púlsame",
            Left = 200,
            Top = 300,
            Width = 1200,
            Height = 450,
            ParentControl = panel
        };
        panel.Children.Add(btn);

        string serialized = FormDesignSerializer.Serialize(model);

        Assert.Contains("Begin Proyecto1.PanelBox pnlArea", serialized);
        Assert.Contains("Begin VB.CommandButton cmdAccion", serialized);
        Assert.Contains("Caption         =   \"Púlsame\"", serialized);

        // Debe parsear de vuelta preservando la relación padre-hijo
        var roundTripModel = FormDesignParser.Parse(serialized);
        var parsedPanel = roundTripModel.FindControl("pnlArea");
        Assert.NotNull(parsedPanel);
        Assert.Single(parsedPanel.Children);
        Assert.Equal("cmdAccion", parsedPanel.Children[0].Name);
        Assert.Same(parsedPanel, parsedPanel.Children[0].ParentControl);
    }
}
