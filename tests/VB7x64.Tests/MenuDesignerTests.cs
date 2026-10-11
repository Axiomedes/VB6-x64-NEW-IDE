using System;
using System.Collections.Generic;
using System.Linq;
using VB7x64.ProjectModel.Designers;
using Xunit;

namespace VB7x64.Tests;

public class MenuDesignerTests
{
    private const string SampleFormWithMenu =
        "VERSION 5.00\r\n" +
        "Begin VB.Form Form1 \r\n" +
        "   Caption         =   \"Menu Test\"\r\n" +
        "   ClientHeight    =   3195\r\n" +
        "   ClientLeft      =   60\r\n" +
        "   ClientTop       =   345\r\n" +
        "   ClientWidth     =   4680\r\n" +
        "   Begin VB.CommandButton cmdTest \r\n" +
        "      Caption         =   \"Click\"\r\n" +
        "      Height          =   495\r\n" +
        "      Left            =   120\r\n" +
        "      Top             =   120\r\n" +
        "      Width           =   1215\r\n" +
        "   End\r\n" +
        "   Begin VB.Menu mnuFile \r\n" +
        "      Caption         =   \"&Archivo\"\r\n" +
        "      Begin VB.Menu mnuFileNew \r\n" +
        "         Caption         =   \"&Nuevo\"\r\n" +
        "         Shortcut        =   ^N\r\n" +
        "      End\r\n" +
        "      Begin VB.Menu mnuFileOpen \r\n" +
        "         Caption         =   \"&Abrir...\"\r\n" +
        "         Shortcut        =   ^O\r\n" +
        "      End\r\n" +
        "      Begin VB.Menu mnuFileSep1 \r\n" +
        "         Caption         =   \"-\"\r\n" +
        "      End\r\n" +
        "      Begin VB.Menu mnuFileExit \r\n" +
        "         Caption         =   \"&Salir\"\r\n" +
        "      End\r\n" +
        "   End\r\n" +
        "   Begin VB.Menu mnuEdit \r\n" +
        "      Caption         =   \"&Edición\"\r\n" +
        "      Begin VB.Menu mnuEditCut \r\n" +
        "         Caption         =   \"Cor&tar\"\r\n" +
        "         Shortcut        =   ^X\r\n" +
        "      End\r\n" +
        "      Begin VB.Menu mnuEditCopy \r\n" +
        "         Caption         =   \"&Copiar\"\r\n" +
        "         Shortcut        =   ^C\r\n" +
        "      End\r\n" +
        "   End\r\n" +
        "End\r\n" +
        "Attribute VB_Name = \"Form1\"\r\n" +
        "Attribute VB_GlobalNameSpace = False\r\n" +
        "Attribute VB_Creatable = False\r\n" +
        "Attribute VB_PredeclaredId = True\r\n" +
        "Attribute VB_Exposed = False\r\n";

    [Fact]
    public void Parse_FormWithMenus_BuildsHierarchyCorrectly()
    {
        var model = FormDesignParser.Parse(SampleFormWithMenu);

        Assert.Equal("Form1", model.Name);
        Assert.Single(model.NonMenuControls);
        Assert.Equal("cmdTest", model.NonMenuControls[0].Name);

        var topMenus = model.TopLevelMenus;
        Assert.Equal(2, topMenus.Count);

        var mnuFile = topMenus[0];
        Assert.Equal("mnuFile", mnuFile.Name);
        Assert.Equal("&Archivo", mnuFile.Caption);
        Assert.True(mnuFile.IsMenu);
        Assert.Equal(4, mnuFile.Children.Count);

        Assert.Equal("mnuFileNew", mnuFile.Children[0].Name);
        Assert.Equal("&Nuevo", mnuFile.Children[0].Caption);
        Assert.Equal("^N", mnuFile.Children[0].Shortcut);

        Assert.Equal("mnuFileSep1", mnuFile.Children[2].Name);
        Assert.Equal("-", mnuFile.Children[2].Caption);

        var mnuEdit = topMenus[1];
        Assert.Equal("mnuEdit", mnuEdit.Name);
        Assert.Equal(2, mnuEdit.Children.Count);
    }

    [Fact]
    public void Serialize_FormWithMenus_RoundTripsWithoutLoss()
    {
        var model = FormDesignParser.Parse(SampleFormWithMenu);
        string serialized = FormDesignSerializer.Serialize(model);

        var model2 = FormDesignParser.Parse(serialized);

        Assert.Equal(model.Name, model2.Name);
        Assert.Equal(model.TopLevelMenus.Count, model2.TopLevelMenus.Count);
        Assert.Equal(model.TopLevelMenus[0].Children.Count, model2.TopLevelMenus[0].Children.Count);
        Assert.Equal("^N", model2.TopLevelMenus[0].Children[0].Shortcut);
    }

    [Fact]
    public void FlattenAndReplaceMenus_PreservesLevelsAndHierarchy()
    {
        var model = FormDesignParser.Parse(SampleFormWithMenu);

        var flattened = model.GetFlattenedMenuItems();
        // mnuFile (0), mnuFileNew (1), mnuFileOpen (1), mnuFileSep1 (1), mnuFileExit (1), mnuEdit (0), mnuEditCut (1), mnuEditCopy (1)
        Assert.Equal(8, flattened.Count);
        Assert.Equal(0, flattened[0].Level);
        Assert.Equal(1, flattened[1].Level);
        Assert.Equal(0, flattened[5].Level);

        // Modificar e invertir
        var newFlattened = new List<(FormDesignerControl Control, int Level)>
        {
            (new FormDesignerControl { TypeName = "VB.Menu", Name = "mnuTools", Caption = "&Herramientas" }, 0),
            (new FormDesignerControl { TypeName = "VB.Menu", Name = "mnuToolsOptions", Caption = "&Opciones", Shortcut = "{F5}" }, 1)
        };

        model.ReplaceMenus(newFlattened);

        Assert.Single(model.TopLevelMenus);
        Assert.Equal("mnuTools", model.TopLevelMenus[0].Name);
        Assert.Single(model.TopLevelMenus[0].Children);
        Assert.Equal("mnuToolsOptions", model.TopLevelMenus[0].Children[0].Name);
        Assert.Equal("{F5}", model.TopLevelMenus[0].Children[0].Shortcut);
    }

    [Fact]
    public void VbMenuShortcutHelper_ConvertsBothWaysAccurately()
    {
        Assert.Equal("Ctrl+N", VbMenuShortcutHelper.ToDisplayText("^N"));
        Assert.Equal("^N", VbMenuShortcutHelper.ToVbCode("Ctrl+N"));

        Assert.Equal("F5", VbMenuShortcutHelper.ToDisplayText("{F5}"));
        Assert.Equal("{F5}", VbMenuShortcutHelper.ToVbCode("F5"));

        Assert.Equal("Shift+F1", VbMenuShortcutHelper.ToDisplayText("+{F1}"));
        Assert.Equal("+{F1}", VbMenuShortcutHelper.ToVbCode("Shift+F1"));

        Assert.Equal("Del", VbMenuShortcutHelper.ToDisplayText("{DEL}"));
        Assert.Equal("{DEL}", VbMenuShortcutHelper.ToVbCode("Del"));

        Assert.Equal("(None)", VbMenuShortcutHelper.ToDisplayText(string.Empty));
        Assert.Equal(string.Empty, VbMenuShortcutHelper.ToVbCode("(None)"));
    }
}
