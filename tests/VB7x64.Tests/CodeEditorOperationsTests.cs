using VB7x64.ProjectModel.Advanced;
using Xunit;

namespace VB7x64.Tests;

public class CodeEditorOperationsTests
{
    [Fact]
    public void CommentLine_AddsApostropheAtIndentation()
    {
        string input = "    Dim x As Integer";
        string expected = "    ' Dim x As Integer";
        string result = CodeEditorOperations.CommentLine(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CommentLine_IgnoresEmptyOrWhitespace()
    {
        Assert.Equal("", CodeEditorOperations.CommentLine(""));
        Assert.Equal("   ", CodeEditorOperations.CommentLine("   "));
    }

    [Fact]
    public void UncommentLine_RemovesApostropheWithOrWithoutSpace()
    {
        Assert.Equal("    Dim x As Integer", CodeEditorOperations.UncommentLine("    ' Dim x As Integer"));
        Assert.Equal("    Dim x As Integer", CodeEditorOperations.UncommentLine("    'Dim x As Integer"));
        Assert.Equal("MsgBox 1", CodeEditorOperations.UncommentLine("Rem MsgBox 1"));
    }

    [Fact]
    public void SwitchAssignmentLine_SwapsValidSimpleAssignment()
    {
        string input = "txtNombre.Text = rs!Nombre";
        string expected = "rs!Nombre = txtNombre.Text";
        Assert.Equal(expected, CodeEditorOperations.SwitchAssignmentLine(input));
    }

    [Fact]
    public void SwitchAssignmentLine_PreservesIndentationAndComment()
    {
        string input = "    txtEdad.Text = rs!Edad ' Asignar edad";
        string expected = "    rs!Edad = txtEdad.Text ' Asignar edad";
        Assert.Equal(expected, CodeEditorOperations.SwitchAssignmentLine(input));
    }

    [Fact]
    public void SwitchAssignmentLine_HandlesSetAndLetKeywords()
    {
        string input = "Set m_Obj = CreateObject(\"Scripting.FileSystemObject\")";
        string expected = "Set CreateObject(\"Scripting.FileSystemObject\") = m_Obj";
        Assert.Equal(expected, CodeEditorOperations.SwitchAssignmentLine(input));
    }

    [Fact]
    public void SwitchAssignmentLine_IgnoresComparisonOperators()
    {
        string input = "If a <= b Then";
        Assert.Equal(input, CodeEditorOperations.SwitchAssignmentLine(input));

        string input2 = "If a <> b Then";
        Assert.Equal(input2, CodeEditorOperations.SwitchAssignmentLine(input2));
    }

    [Fact]
    public void SwitchAssignmentLine_IgnoresMultipleEqualsOnSameLine()
    {
        string input = "If a = 1 Then b = 2";
        // Tiene dos signos '=', no debe invertirse
        Assert.Equal(input, CodeEditorOperations.SwitchAssignmentLine(input));
    }

    [Fact]
    public void ChangeCase_TransformsAccurately()
    {
        string text = "hola mundo vb6";
        Assert.Equal("HOLA MUNDO VB6", CodeEditorOperations.ChangeCase(text, TextCaseMode.Upper));
        Assert.Equal("hola mundo vb6", CodeEditorOperations.ChangeCase(text, TextCaseMode.Lower));
        Assert.Equal("Hola Mundo Vb6", CodeEditorOperations.ChangeCase(text, TextCaseMode.Title));
    }
}
