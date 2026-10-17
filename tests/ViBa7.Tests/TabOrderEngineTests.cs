using System;
using System.Collections.Generic;
using System.Linq;
using ViBa7.ProjectModel.Designers;
using Xunit;

namespace ViBa7.Tests;

public class TabOrderEngineTests
{
    private const string SampleFormHeader = @"Begin VB.Form frmMain 
   Caption         =   ""Main Form""
   ClientHeight    =   3195
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   4680
   Begin VB.CommandButton cmdCancel 
      Caption         =   ""&Cancel""
      Height          =   375
      Left            =   2400
      TabIndex        =   2
      Top             =   1200
      Width           =   1095
   End
   Begin VB.TextBox txtName 
      Height          =   375
      Left            =   360
      TabIndex        =   0
      Top             =   360
      Width           =   3135
   End
   Begin VB.CommandButton cmdOk 
      Caption         =   ""&OK""
      Height          =   375
      Left            =   360
      TabIndex        =   1
      Top             =   1200
      Width           =   1095
   End
   Begin VB.Label lblName 
      Caption         =   ""&Name:""
      Height          =   255
      Left            =   360
      Top             =   120
      Width           =   975
   End
End";

    [Fact]
    public void ParseControls_ExtractsControlsCorrectly()
    {
        var controls = TabOrderEngine.ParseControls(SampleFormHeader);

        Assert.NotEmpty(controls);
        // Debe contener txtName, cmdOk, cmdCancel, lblName (pero no frmMain)
        Assert.Contains(controls, c => c.Name == "txtName");
        Assert.Contains(controls, c => c.Name == "cmdOk");
        Assert.Contains(controls, c => c.Name == "cmdCancel");
        Assert.Contains(controls, c => c.Name == "lblName");
        Assert.DoesNotContain(controls, c => c.Name == "frmMain");
    }

    [Fact]
    public void AutoOrderByGeography_SortsTopToBottomLeftToRight()
    {
        var controls = TabOrderEngine.ParseControls(SampleFormHeader);

        // Auto ordenar geográficamente
        var ordered = TabOrderEngine.AutoOrderByGeography(controls, rowTolerance: 150);

        // lblName (Top 120) -> txtName (Top 360) -> cmdOk (Top 1200, Left 360) -> cmdCancel (Top 1200, Left 2400)
        Assert.Equal("lblName", ordered[0].Name);
        Assert.Equal("txtName", ordered[1].Name);
        Assert.Equal("cmdOk", ordered[2].Name);
        Assert.Equal("cmdCancel", ordered[3].Name);
    }

    [Fact]
    public void ApplyTabOrder_UpdatesTabIndexInFormHeader()
    {
        var controls = TabOrderEngine.ParseControls(SampleFormHeader);
        var ordered = TabOrderEngine.AutoOrderByGeography(controls);

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].TabIndex = i;
        }

        string result = TabOrderEngine.ApplyTabOrder(SampleFormHeader, ordered);

        Assert.Contains("TabIndex        =   0", result);
        Assert.Contains("TabIndex        =   1", result);
        Assert.Contains("TabIndex        =   2", result);
        Assert.Contains("TabIndex        =   3", result);

        // Re-parsear y validar que el orden de TabIndex coincide
        var reparsed = TabOrderEngine.ParseControls(result);
        var lblName = reparsed.First(c => c.Name == "lblName");
        var txtName = reparsed.First(c => c.Name == "txtName");
        var cmdOk = reparsed.First(c => c.Name == "cmdOk");
        var cmdCancel = reparsed.First(c => c.Name == "cmdCancel");

        Assert.Equal(0, lblName.TabIndex);
        Assert.Equal(1, txtName.TabIndex);
        Assert.Equal(2, cmdOk.TabIndex);
        Assert.Equal(3, cmdCancel.TabIndex);
    }
}
