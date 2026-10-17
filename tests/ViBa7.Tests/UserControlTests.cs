using System;
using System.IO;
using System.Linq;
using System.Text;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.Documents;
using ViBa7.ProjectModel.Execution;
using ViBa7.ProjectModel.Vbp;
using Xunit;

namespace ViBa7.Tests;

public class UserControlTests : IDisposable
{
    private readonly string _tempDir;

    static UserControlTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public UserControlTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ViBa7_UCTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void UserControl_ParseHeader_DetectsUserControlAndChildControls()
    {
        string ctlHeader = @"VERSION 5.00
Begin VB.UserControl MiUserControl 
   ClientHeight    =   3600
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   4800
   ScaleHeight     =   3600
   ScaleWidth      =   4800
   Begin VB.CommandButton cmdAction 
      Caption         =   ""Acción""
      Height          =   495
      Left            =   240
      TabIndex        =   0
      Top             =   240
      Width           =   1455
   End
   Begin VB.TextBox txtInput 
      Height          =   375
      Left            =   240
      TabIndex        =   1
      Text            =   ""Valor Inicial""
      Top             =   960
      Width           =   2895
   End
End
";

        var model = FormDesignParser.Parse(ctlHeader);

        Assert.True(model.IsUserControl);
        Assert.Equal("MiUserControl", model.Name);
        Assert.Equal(4800, model.ClientWidth);
        Assert.Equal(3600, model.ClientHeight);
        Assert.Equal(2, model.Children.Count);

        var btn = model.Children[0];
        Assert.Equal("VB.CommandButton", btn.TypeName);
        Assert.Equal("cmdAction", btn.Name);
        Assert.Equal("Acción", btn.Caption);
        Assert.Equal(240, btn.Left);
        Assert.Equal(240, btn.Top);

        var txt = model.Children[1];
        Assert.Equal("VB.TextBox", txt.TypeName);
        Assert.Equal("txtInput", txt.Name);
        Assert.Equal("Valor Inicial", txt.Text);
        Assert.Equal(240, txt.Left);
        Assert.Equal(960, txt.Top);
    }

    [Fact]
    public void UserControl_ParseExtentProperties_ConvertsHiMetricToTwips()
    {
        string ctlHeader = @"VERSION 5.00
Begin VB.UserControl ExtentCtrl 
   _ExtentX        =   4233
   _ExtentY        =   2117
End
";

        var model = FormDesignParser.Parse(ctlHeader);

        Assert.True(model.IsUserControl);
        Assert.Equal("ExtentCtrl", model.Name);
        // 4233 * 0.567 ≈ 2400 twips
        Assert.Equal(2400, model.ClientWidth);
        // 2117 * 0.567 ≈ 1200 twips
        Assert.Equal(1200, model.ClientHeight);
    }

    [Fact]
    public void UserControl_Serialize_PreservesUserControlHeader()
    {
        var model = new FormDesignModel
        {
            Name = "ClockUC",
            RootType = "VB.UserControl",
            ClientWidth = 3200,
            ClientHeight = 1600
        };

        model.Children.Add(new FormDesignerControl
        {
            TypeName = "VB.Label",
            Name = "lblTime",
            Caption = "12:00:00",
            Left = 100,
            Top = 150,
            Width = 2000,
            Height = 400
        });

        string serialized = FormDesignSerializer.Serialize(model);

        Assert.Contains("Begin VB.UserControl ClockUC", serialized);
        Assert.Contains("Begin VB.Label lblTime", serialized);
        Assert.Contains("Caption", serialized);
        Assert.Contains("12:00:00", serialized);
        Assert.Contains("\r\nEnd\r\n", serialized);
        Assert.Contains("Attribute VB_Name = \"ClockUC\"", serialized);
    }

    [Fact]
    public void ControlToolboxRegistry_RegisterAndUnregisterUserControls()
    {
        var project = new VB6Project
        {
            Name = "SuperApp"
        };

        project.Items.Add(new ProjectItem
        {
            Type = ProjectItemType.UserControl,
            Name = "BotonEspecial",
            RelativePath = "BotonEspecial.ctl",
            FileExists = true
        });

        ControlToolboxRegistry.RegisterUserControlTools(project);

        var tool = ControlToolboxRegistry.GetAllTools().FirstOrDefault(t => t.TypeName == "SuperApp.BotonEspecial");
        Assert.NotNull(tool);
        Assert.Equal("BotonEspecial", tool.DisplayName);
        Assert.Equal("🎛️", tool.Glyph);
        Assert.Contains("Control de Usuario", tool.ToolTip);

        var dummyModel = new FormDesignModel { Name = "Form1" };
        var created = ControlToolboxRegistry.CreateControl("SuperApp.BotonEspecial", dummyModel, 120, 240, 2400, 1200);
        Assert.Equal("SuperApp.BotonEspecial", created.TypeName);
        Assert.Equal(120, created.Left);
        Assert.Equal(240, created.Top);

        ControlToolboxRegistry.UnregisterUserControlTools();
        var toolAfter = ControlToolboxRegistry.GetAllTools().FirstOrDefault(t => t.TypeName == "SuperApp.BotonEspecial");
        Assert.Null(toolAfter);
    }

    [Fact]
    public void VB6Project_FindUserControl_AndCacheResolvesModel()
    {
        string ctlPath = Path.Combine(_tempDir, "GaugeUC.ctl");
        string ctlContent = @"VERSION 5.00
Begin VB.UserControl GaugeUC 
   ClientHeight    =   2400
   ClientWidth     =   3600
   Begin VB.Label lblGauge 
      Caption         =   ""Gauge""
      Height          =   300
      Left            =   100
      Top             =   100
      Width           =   1500
   End
End
Attribute VB_Name = ""GaugeUC""
";
        File.WriteAllText(ctlPath, ctlContent, Encoding.GetEncoding(1252));

        var project = new VB6Project
        {
            Name = "DashboardProj"
        };

        project.Items.Add(new ProjectItem
        {
            Type = ProjectItemType.UserControl,
            Name = "GaugeUC",
            RelativePath = "GaugeUC.ctl",
            AbsolutePath = ctlPath,
            FileExists = true
        });

        // Búsqueda por nombre simple
        var foundSimple = project.FindUserControl("GaugeUC");
        Assert.NotNull(foundSimple);
        Assert.Equal("GaugeUC", foundSimple.Name);

        // Búsqueda por nombre calificado
        var foundQualified = project.FindUserControl("DashboardProj.GaugeUC");
        Assert.NotNull(foundQualified);
        Assert.Equal("GaugeUC", foundQualified.Name);

        // Carga y parseo del modelo
        var model = project.GetUserControlDesignModel("DashboardProj.GaugeUC");
        Assert.NotNull(model);
        Assert.True(model.IsUserControl);
        Assert.Equal("GaugeUC", model.Name);
        Assert.Single(model.Children);
        Assert.Equal("lblGauge", model.Children[0].Name);

        // Segunda llamada recupera de caché
        var cachedModel = project.GetUserControlDesignModel("GaugeUC");
        Assert.Same(model, cachedModel);

        // Invalidar caché
        project.InvalidateUserControlCache();
        var newModel = project.GetUserControlDesignModel("GaugeUC");
        Assert.NotNull(newModel);
        Assert.NotSame(model, newModel);
    }

    [Fact]
    public void FormWithEmbeddedUserControl_ParsesAndIdentifiesControl()
    {
        string frmHeader = @"VERSION 5.00
Begin VB.Form FormMain 
   Caption         =   ""Ventana Anfitriona""
   ClientHeight    =   5000
   ClientWidth     =   7000
   Begin DashboardProj.GaugeUC gaugeSpeed 
      Height          =   2400
      Left            =   400
      TabIndex        =   0
      Top             =   500
      Width           =   3600
   End
End
";

        var model = FormDesignParser.Parse(frmHeader);

        Assert.False(model.IsUserControl);
        Assert.Equal("FormMain", model.Name);
        Assert.Single(model.Children);

        var ucCtrl = model.Children[0];
        Assert.Equal("DashboardProj.GaugeUC", ucCtrl.TypeName);
        Assert.Equal("gaugeSpeed", ucCtrl.Name);
        Assert.Equal(400, ucCtrl.Left);
        Assert.Equal(500, ucCtrl.Top);
        Assert.Equal(3600, ucCtrl.Width);
        Assert.Equal(2400, ucCtrl.Height);
    }

    [Fact]
    public void LiveExecutionHost_ExecutesUserControlProcedures_AndLifecycle()
    {
        var ctx = new VbExecutionContext();
        ctx.ModuleVariables["InitializedCount"] = VbValue.FromLong(0);
        var host = new LiveExecutionHost(ctx);

        string ucCode = @"
Sub UserControl_Initialize()
    InitializedCount = 42
    Debug.Print ""UserControl Initialized: 42""
End Sub
";

        host.LoadSourceCode(ucCode, "MiControl");

        string debugOutput = string.Empty;
        ctx.OnDebugPrint += msg => debugOutput = msg;

        // Disparar inicialización del UserControl
        var sig = host.InvokeProcedure("UserControl_Initialize", "MiControl");
        Assert.Equal(FlowSignal.Normal, sig);

        var val = ctx.GetVariable("InitializedCount");
        Assert.Equal(42, val.AsLong());
        Assert.Contains("42", debugOutput);

        // Evaluar expresión interactiva
        var res = host.EvaluateImmediate("? InitializedCount * 2");
        Assert.Equal(84.0, res.AsDouble());
    }
}
