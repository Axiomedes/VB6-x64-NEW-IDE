using System;
using System.Collections.Generic;
using System.Linq;
using VB7x64.ProjectModel.Designers;
using Xunit;

namespace VB7x64.Tests;

public class HotkeyEngineTests
{
    private const string SampleFormHeader = @"Begin VB.Form frmMain 
   Caption         =   ""Main Form""
   Begin VB.CommandButton cmdSave 
      Caption         =   ""&Guardar""
   End
   Begin VB.CommandButton cmdCancel 
      Caption         =   ""&Cancelar""
   End
   Begin VB.CommandButton cmdClose 
      Caption         =   ""&Cerrar""
   End
   Begin VB.CommandButton cmdHelp 
      Caption         =   ""Ayuda""
   End
End";

    [Fact]
    public void ParseHotkeys_DetectsHotkeysAndConflicts()
    {
        var items = HotkeyEngine.ParseHotkeys(SampleFormHeader);

        Assert.Equal(4, items.Count);

        var save = items.First(i => i.ControlName == "cmdSave");
        var cancel = items.First(i => i.ControlName == "cmdCancel");
        var close = items.First(i => i.ControlName == "cmdClose");
        var help = items.First(i => i.ControlName == "cmdHelp");

        Assert.Equal('G', save.HotkeyChar);
        Assert.False(save.HasConflict);

        Assert.Equal('C', cancel.HotkeyChar);
        Assert.True(cancel.HasConflict); // 'C' está duplicada con cmdClose

        Assert.Equal('C', close.HotkeyChar);
        Assert.True(close.HasConflict);

        Assert.Null(help.HotkeyChar);
        Assert.False(help.HasConflict);
    }

    [Fact]
    public void GetAvailableLetters_ExcludesUsedHotkeys()
    {
        var items = HotkeyEngine.ParseHotkeys(SampleFormHeader);
        var available = HotkeyEngine.GetAvailableLetters(items);

        // 'G' y 'C' están usadas, no deben estar en libres
        Assert.DoesNotContain('G', available);
        Assert.DoesNotContain('C', available);
        // 'A', 'B', 'D' etc deben estar disponibles
        Assert.Contains('A', available);
        Assert.Contains('B', available);
    }

    [Fact]
    public void ApplyCaptions_ModifiesFormHeaderAccurately()
    {
        var items = HotkeyEngine.ParseHotkeys(SampleFormHeader);

        // Resolver conflicto cambiando el caption de cmdClose a Ce&rrar
        var close = items.First(i => i.ControlName == "cmdClose");
        close.NewCaption = "Ce&rrar";

        string result = HotkeyEngine.ApplyCaptions(SampleFormHeader, items);

        Assert.Contains("Caption         =   \"Ce&rrar\"", result);
        Assert.Contains("Caption         =   \"&Guardar\"", result);
        Assert.Contains("Caption         =   \"&Cancelar\"", result);

        // Al re-analizar, ya no debe haber conflicto entre cancel y close
        var reparsed = HotkeyEngine.ParseHotkeys(result);
        var newCancel = reparsed.First(i => i.ControlName == "cmdCancel");
        var newClose = reparsed.First(i => i.ControlName == "cmdClose");

        Assert.False(newCancel.HasConflict);
        Assert.False(newClose.HasConflict);
        Assert.Equal('R', newClose.HotkeyChar);
    }
}
