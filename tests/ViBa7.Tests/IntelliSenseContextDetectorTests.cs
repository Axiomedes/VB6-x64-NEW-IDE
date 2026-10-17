using System.Collections.Generic;
using System.Linq;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.IntelliSense;
using Xunit;

namespace ViBa7.Tests;

public class IntelliSenseContextDetectorTests
{
    private FormDesignModel CreateSampleForm()
    {
        var model = new FormDesignModel
        {
            Name = "Form1",
            Caption = "Mi Formulario"
        };
        model.Children.Add(new FormDesignerControl
        {
            Name = "Command1",
            TypeName = "VB.CommandButton"
        });
        model.Children.Add(new FormDesignerControl
        {
            Name = "Text1",
            TypeName = "VB.TextBox"
        });
        return model;
    }

    [Fact]
    public void DetectContext_MeDot_ReturnsFormMembersAndControls()
    {
        var form = CreateSampleForm();
        string code = "Sub Test()\n    Me.\nEnd Sub";
        int caret = code.IndexOf("Me.") + 3;

        var context = IntelliSenseContextDetector.DetectContext(code, caret, form);

        Assert.Equal(IntelliSenseContextKind.MemberAccess, context.Kind);
        Assert.Equal("Me", context.TargetExpression);
        Assert.Empty(context.Prefix);
        Assert.Contains(context.Candidates, c => c.Name == "Caption");
        Assert.Contains(context.Candidates, c => c.Name == "Show");
        Assert.Contains(context.Candidates, c => c.Name == "Command1" && c.Kind == VbSymbolKind.Control);
        Assert.Contains(context.Candidates, c => c.Name == "Text1" && c.Kind == VbSymbolKind.Control);
    }

    [Fact]
    public void DetectContext_ControlDot_FiltersByPrefix()
    {
        var form = CreateSampleForm();
        string code = "Sub Test()\n    Command1.Cap\nEnd Sub";
        int caret = code.IndexOf("Command1.Cap") + 12;

        var context = IntelliSenseContextDetector.DetectContext(code, caret, form);

        Assert.Equal(IntelliSenseContextKind.MemberAccess, context.Kind);
        Assert.Equal("Command1", context.TargetExpression);
        Assert.Equal("Cap", context.Prefix);
        Assert.Contains(context.Candidates, c => c.Name == "Caption");
        Assert.DoesNotContain(context.Candidates, c => c.Name == "Enabled");
    }

    [Fact]
    public void DetectContext_WithBlock_ResolvesTarget()
    {
        var form = CreateSampleForm();
        string code = @"Sub Test()
    With Command1
        .
    End With
End Sub";
        int dotPos = code.IndexOf("        .") + 9;

        var context = IntelliSenseContextDetector.DetectContext(code, dotPos, form);

        Assert.Equal(IntelliSenseContextKind.MemberAccess, context.Kind);
        Assert.Equal("Command1", context.TargetExpression);
        Assert.Contains(context.Candidates, c => c.Name == "Caption");
        Assert.Contains(context.Candidates, c => c.Name == "SetFocus");
    }

    [Fact]
    public void DetectContext_NestedWithBlock_ResolvesInnermostTarget()
    {
        var form = CreateSampleForm();
        string code = @"Sub Test()
    With Form1
        With Text1
            .
        End With
    End With
End Sub";
        int dotPos = code.IndexOf("            .") + 13;

        var context = IntelliSenseContextDetector.DetectContext(code, dotPos, form);

        Assert.Equal(IntelliSenseContextKind.MemberAccess, context.Kind);
        Assert.Equal("Text1", context.TargetExpression);
        Assert.Contains(context.Candidates, c => c.Name == "Text");
        Assert.Contains(context.Candidates, c => c.Name == "SelStart");
    }

    [Fact]
    public void DetectParameterInfo_FunctionCallWithParentheses()
    {
        string code = "x = Mid(\"hello, world\", ";
        int caret = code.Length;

        var context = IntelliSenseContextDetector.DetectContext(code, caret);

        Assert.Equal(IntelliSenseContextKind.ParameterInfo, context.Kind);
        Assert.Equal("Mid", context.FunctionOrMethodName);
        Assert.Equal(1, context.ParameterIndex);
        Assert.NotNull(context.InvokedSymbol);
        Assert.Equal("Start", context.InvokedSymbol.Parameters[1].Name);
    }

    [Fact]
    public void DetectParameterInfo_SubCallWithoutParentheses()
    {
        string code = "MsgBox \"Texto de prueba\", ";
        int caret = code.Length;

        var context = IntelliSenseContextDetector.DetectContext(code, caret);

        Assert.Equal(IntelliSenseContextKind.ParameterInfo, context.Kind);
        Assert.Equal("MsgBox", context.FunctionOrMethodName);
        Assert.Equal(1, context.ParameterIndex);
        Assert.NotNull(context.InvokedSymbol);
        Assert.Equal("Buttons", context.InvokedSymbol.Parameters[1].Name);
    }

    [Fact]
    public void DetectContext_InsideCommentOrString_ReturnsNone()
    {
        string codeComment = "    ' Me.Caption = \"test\"";
        int caretComment = codeComment.Length;
        var ctx1 = IntelliSenseContextDetector.DetectContext(codeComment, caretComment);
        Assert.Equal(IntelliSenseContextKind.None, ctx1.Kind);

        string codeString = "    s = \"Me.Caption = 123\"";
        int caretString = codeString.IndexOf("Me.") + 3;
        var ctx2 = IntelliSenseContextDetector.DetectContext(codeString, caretString);
        Assert.Equal(IntelliSenseContextKind.None, ctx2.Kind);
    }

    [Fact]
    public void DetectContext_GlobalSymbols_ExplicitTrigger()
    {
        var form = CreateSampleForm();
        string code = "Sub Test()\n    Dim counter As Integer\n    \nEnd Sub";
        int caret = code.IndexOf("    \nEnd Sub") + 4;

        var context = IntelliSenseContextDetector.DetectContext(code, caret, form, explicitTrigger: true);

        Assert.Equal(IntelliSenseContextKind.GlobalSymbols, context.Kind);
        Assert.Contains(context.Candidates, c => c.Name == "MsgBox");
        Assert.Contains(context.Candidates, c => c.Name == "vbCrLf");
        Assert.Contains(context.Candidates, c => c.Name == "Integer");
        Assert.Contains(context.Candidates, c => c.Name == "Command1");
        Assert.Contains(context.Candidates, c => c.Name == "counter");
    }
}
