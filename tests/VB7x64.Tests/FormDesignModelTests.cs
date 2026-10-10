using System;
using System.IO;
using System.Linq;
using VB7x64.ProjectModel.Designers;
using Xunit;

namespace VB7x64.Tests;

public class FormDesignModelTests
{
    private const string SampleFormHeader = @"VERSION 5.00
Begin VB.Form frmMain 
   Caption         =   ""Main Application""
   ClientHeight    =   4500
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   6000
   LinkTopic       =   ""frmMain""
   ScaleHeight     =   4500
   ScaleWidth      =   6000
   StartUpPosition =   2  'CenterScreen
   Begin VB.Frame fraOptions 
      Caption         =   ""Opciones""
      Height          =   1815
      Left            =   240
      TabIndex        =   1
      Top             =   720
      Width           =   3495
      Begin VB.OptionButton optA 
         Caption         =   ""Opción A""
         Height          =   255
         Left            =   240
         TabIndex        =   2
         Top             =   360
         Width           =   1455
      End
      Begin VB.OptionButton optB 
         Caption         =   ""Opción B""
         Height          =   255
         Left            =   240
         TabIndex        =   3
         Top             =   840
         Width           =   1455
      End
   End
   Begin VB.CommandButton cmdOk 
      Caption         =   ""&Aceptar""
      Height          =   375
      Left            =   4200
      TabIndex        =   0
      Top             =   720
      Width           =   1455
   End
End
Attribute VB_Name = ""frmMain""
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False";

    [Fact]
    public void Parse_SimpleForm_ExtractsPropertiesAndControls()
    {
        var model = FormDesignParser.Parse(SampleFormHeader);

        Assert.Equal("VERSION 5.00", model.VersionHeader);
        Assert.Equal("VB.Form", model.RootType);
        Assert.Equal("frmMain", model.Name);
        Assert.Equal("Main Application", model.Caption);
        Assert.Equal(6000, model.ClientWidth);
        Assert.Equal(4500, model.ClientHeight);
        Assert.Equal(400.0, model.ClientWidthPixels); // 6000 / 15
        Assert.Equal(300.0, model.ClientHeightPixels); // 4500 / 15

        // Controles de primer nivel: fraOptions y cmdOk
        Assert.Equal(2, model.Children.Count);
        var cmd = model.Children.FirstOrDefault(c => c.Name == "cmdOk");
        Assert.NotNull(cmd);
        Assert.Equal("VB.CommandButton", cmd.TypeName);
        Assert.Equal("&Aceptar", cmd.Caption);
        Assert.Equal(4200, cmd.Left);
        Assert.Equal(280.0, cmd.LeftPixels); // 4200 / 15
        Assert.Equal(720, cmd.Top);
        Assert.Equal(48.0, cmd.TopPixels); // 720 / 15
        Assert.Equal(1455, cmd.Width);
        Assert.Equal(375, cmd.Height);
        Assert.Equal(0, cmd.TabIndex);

        // Atributos de módulo
        Assert.Contains(model.Attributes, a => a.Contains("Attribute VB_Name = \"frmMain\""));
    }

    [Fact]
    public void Parse_NestedControls_PreservesHierarchy()
    {
        var model = FormDesignParser.Parse(SampleFormHeader);

        var frame = model.Children.FirstOrDefault(c => c.Name == "fraOptions");
        Assert.NotNull(frame);
        Assert.True(frame.IsContainer);
        Assert.Equal(2, frame.Children.Count);

        var optA = frame.Children.FirstOrDefault(c => c.Name == "optA");
        var optB = frame.Children.FirstOrDefault(c => c.Name == "optB");
        Assert.NotNull(optA);
        Assert.NotNull(optB);

        Assert.Same(frame, optA.ParentControl);
        Assert.Same(frame, optB.ParentControl);

        // GetAllControls debe aplanar los 4 controles
        var allControls = model.GetAllControls();
        Assert.Equal(4, allControls.Count);
        Assert.Contains(allControls, c => c.Name == "fraOptions");
        Assert.Contains(allControls, c => c.Name == "optA");
        Assert.Contains(allControls, c => c.Name == "optB");
        Assert.Contains(allControls, c => c.Name == "cmdOk");
    }

    [Fact]
    public void Parse_FontBlock_ExtractsFontDescription()
    {
        string headerWithFont = @"Begin VB.Form Form1 
   Caption         =   ""Form With Font""
   ClientHeight    =   3195
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   4680
   BeginProperty Font 
      Name            =   ""Tahoma""
      Size            =   9.75
      Charset         =   0
      Weight          =   700
      Underline       =   -1  'True
      Italic          =   0   'False
      Strikethrough   =   0   'False
   EndProperty
   Begin VB.Label Label1 
      Caption         =   ""Texto""
      Height          =   255
      Left            =   240
      Top             =   240
      Width           =   1215
   End
End";

        var model = FormDesignParser.Parse(headerWithFont);

        Assert.NotNull(model.Font);
        Assert.Equal("Tahoma", model.Font.Name);
        Assert.Equal(9.75, model.Font.Size);
        Assert.Equal(700, model.Font.Weight);
        Assert.True(model.Font.IsBold);
        Assert.True(model.Font.Underline);
        Assert.False(model.Font.Italic);
    }

    [Fact]
    public void RoundTrip_SerializeAndReparse_MaintainsIntegrity()
    {
        var originalModel = FormDesignParser.Parse(SampleFormHeader);

        // Serializar de vuelta a texto
        string serialized = FormDesignSerializer.Serialize(originalModel);

        // Re-parsear el texto generado
        var reparsedModel = FormDesignParser.Parse(serialized);

        Assert.Equal(originalModel.Name, reparsedModel.Name);
        Assert.Equal(originalModel.Caption, reparsedModel.Caption);
        Assert.Equal(originalModel.ClientWidth, reparsedModel.ClientWidth);
        Assert.Equal(originalModel.ClientHeight, reparsedModel.ClientHeight);
        Assert.Equal(originalModel.Children.Count, reparsedModel.Children.Count);

        var origCmd = originalModel.FindControl("cmdOk");
        var reCmd = reparsedModel.FindControl("cmdOk");
        Assert.NotNull(origCmd);
        Assert.NotNull(reCmd);
        Assert.Equal(origCmd.Left, reCmd.Left);
        Assert.Equal(origCmd.Top, reCmd.Top);
        Assert.Equal(origCmd.Width, reCmd.Width);
        Assert.Equal(origCmd.Height, reCmd.Height);
        Assert.Equal(origCmd.Caption, reCmd.Caption);

        var origFrame = originalModel.FindControl("fraOptions");
        var reFrame = reparsedModel.FindControl("fraOptions");
        Assert.NotNull(origFrame);
        Assert.NotNull(reFrame);
        Assert.Equal(origFrame.Children.Count, reFrame.Children.Count);
    }

    [Fact]
    public void ModifyControl_SerializationReflectsChanges()
    {
        var model = FormDesignParser.Parse(SampleFormHeader);

        var cmd = model.FindControl("cmdOk");
        Assert.NotNull(cmd);

        // Mover y cambiar tamaño
        cmd.LeftPixels = 200.0; // 3000 twips
        cmd.TopPixels = 100.0;  // 1500 twips
        cmd.WidthPixels = 120.0; // 1800 twips
        cmd.HeightPixels = 30.0; // 450 twips
        cmd.Caption = "&Confirmar";

        string serialized = FormDesignSerializer.Serialize(model);
        var reparsed = FormDesignParser.Parse(serialized);
        var reparsedCmd = reparsed.FindControl("cmdOk");

        Assert.NotNull(reparsedCmd);
        Assert.Equal(3000, reparsedCmd.Left);
        Assert.Equal(1500, reparsedCmd.Top);
        Assert.Equal(1800, reparsedCmd.Width);
        Assert.Equal(450, reparsedCmd.Height);
        Assert.Equal("&Confirmar", reparsedCmd.Caption);
    }

    [Fact]
    public void GenerateUniqueName_CreatesSequentialNonCollidingNames()
    {
        var model = FormDesignParser.Parse(SampleFormHeader);

        string nextOpt = model.GenerateUniqueName("opt");
        // En el formulario ya existen optA y optB, por lo que opt1 está libre
        Assert.Equal("opt1", nextOpt);

        // Si agregamos Command1
        model.Children.Add(new FormDesignerControl { Name = "Command1", TypeName = "VB.CommandButton" });
        string nextCommand = model.GenerateUniqueName("Command");
        Assert.Equal("Command2", nextCommand);
    }

    [Fact]
    public void RemoveControl_RemovesFromParentOrRoot()
    {
        var model = FormDesignParser.Parse(SampleFormHeader);

        var optA = model.FindControl("optA");
        Assert.NotNull(optA);

        bool removedNested = model.RemoveControl(optA);
        Assert.True(removedNested);
        Assert.Null(model.FindControl("optA"));
        Assert.Equal(3, model.GetAllControls().Count);

        var cmdOk = model.FindControl("cmdOk");
        Assert.NotNull(cmdOk);
        bool removedTopLevel = model.RemoveControl(cmdOk);
        Assert.True(removedTopLevel);
        Assert.Null(model.FindControl("cmdOk"));
        Assert.Equal(2, model.GetAllControls().Count);
    }
}
