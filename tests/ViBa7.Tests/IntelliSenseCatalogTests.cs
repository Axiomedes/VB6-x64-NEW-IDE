using System.Linq;
using ViBa7.ProjectModel.IntelliSense;
using Xunit;

namespace ViBa7.Tests;

public class IntelliSenseCatalogTests
{
    [Fact]
    public void VbRuntimeCatalog_ContainsCoreIntrinsicFunctions()
    {
        var midFunc = VbRuntimeCatalog.FindFunction("Mid");
        Assert.NotNull(midFunc);
        Assert.Equal("Mid", midFunc.Name);
        Assert.Equal(VbSymbolKind.IntrinsicFunction, midFunc.Kind);
        Assert.Equal("String", midFunc.ReturnType);
        Assert.True(midFunc.Parameters.Count >= 2);
        Assert.Equal("String", midFunc.Parameters[0].Name);
        Assert.Equal("Start", midFunc.Parameters[1].Name);
        Assert.False(midFunc.Parameters[0].IsOptional);
        Assert.True(midFunc.Parameters[2].IsOptional); // Length is optional

        var msgBox = VbRuntimeCatalog.FindFunction("MsgBox");
        Assert.NotNull(msgBox);
        Assert.Equal(VbSymbolKind.IntrinsicFunction, msgBox.Kind);
        Assert.Equal("VbMsgBoxResult", msgBox.ReturnType);
        Assert.Contains("Prompt", msgBox.Parameters.Select(p => p.Name));
    }

    [Fact]
    public void VbRuntimeCatalog_ContainsConstants_And_DataTypes()
    {
        var vbCrLf = VbRuntimeCatalog.FindConstant("vbCrLf");
        Assert.NotNull(vbCrLf);
        Assert.Equal(VbSymbolKind.Constant, vbCrLf.Kind);

        var vbOk = VbRuntimeCatalog.FindConstant("vbOKOnly");
        Assert.NotNull(vbOk);

        var intType = VbRuntimeCatalog.FindSymbol("Integer");
        Assert.NotNull(intType);
        Assert.Equal(VbSymbolKind.DataType, intType.Kind);

        var longType = VbRuntimeCatalog.FindSymbol("Long");
        Assert.NotNull(longType);

        var dimKw = VbRuntimeCatalog.FindSymbol("Dim");
        Assert.NotNull(dimKw);
        Assert.Equal(VbSymbolKind.Keyword, dimKw.Kind);
    }

    [Fact]
    public void VbControlMemberCatalog_ReturnsMembers_ForControlsAndForm()
    {
        // CommandButton
        var cmdMembers = VbControlMemberCatalog.GetMembersForControl("CommandButton");
        Assert.NotEmpty(cmdMembers);
        Assert.Contains(cmdMembers, m => m.Name == "Caption");
        Assert.Contains(cmdMembers, m => m.Name == "SetFocus" && m.Kind == VbSymbolKind.Method);
        Assert.Contains(cmdMembers, m => m.Name == "Click" && m.Kind == VbSymbolKind.Event);
        Assert.Contains(cmdMembers, m => m.Name == "Left" && m.Kind == VbSymbolKind.Property);

        // Normalize VB. prefix
        var cmdWithPrefix = VbControlMemberCatalog.GetMembersForControl("VB.CommandButton");
        Assert.Equal(cmdMembers.Count, cmdWithPrefix.Count);

        // TextBox
        var txtMembers = VbControlMemberCatalog.GetMembersForControl("TextBox");
        Assert.Contains(txtMembers, m => m.Name == "Text");
        Assert.Contains(txtMembers, m => m.Name == "SelStart");
        Assert.Contains(txtMembers, m => m.Name == "Change" && m.Kind == VbSymbolKind.Event);

        // Form
        var formMembers = VbControlMemberCatalog.GetFormMembers();
        Assert.NotEmpty(formMembers);
        Assert.Contains(formMembers, m => m.Name == "Caption");
        Assert.Contains(formMembers, m => m.Name == "Show" && m.Kind == VbSymbolKind.Method);
        Assert.Contains(formMembers, m => m.Name == "Load" && m.Kind == VbSymbolKind.Event);
        Assert.Contains(formMembers, m => m.Name == "Cls" && m.Kind == VbSymbolKind.Method);

        // FindMember
        var found = VbControlMemberCatalog.FindMember("VB.TextBox", "text");
        Assert.NotNull(found);
        Assert.Equal("Text", found.Name);
    }

    [Fact]
    public void VbSymbolItem_GeneratesSignaturesCorrectly()
    {
        var midFunc = VbRuntimeCatalog.FindFunction("Mid");
        Assert.NotNull(midFunc);
        Assert.StartsWith("Function Mid(", midFunc.Signature);
        Assert.EndsWith("As String", midFunc.Signature);

        var moveMethod = VbControlMemberCatalog.FindMember("CommandButton", "Move");
        Assert.NotNull(moveMethod);
        Assert.StartsWith("Sub Move(", moveMethod.Signature);
    }
}
