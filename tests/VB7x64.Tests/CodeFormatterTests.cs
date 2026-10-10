using System;
using VB7x64.ProjectModel.Formatting;
using Xunit;

namespace VB7x64.Tests;

public class CodeFormatterTests
{
    [Fact]
    public void FormatCode_MultiLineIf_IndentsCorrectly()
    {
        string unformatted = 
@"Sub Test()
If a = 1 Then
b = 2
ElseIf a = 2 Then
b = 3
Else
b = 4
End If
End Sub";

        string formatted = CodeFormatter.FormatCode(unformatted);

        string expected = 
@"Sub Test()
    If a = 1 Then
        b = 2
    ElseIf a = 2 Then
        b = 3
    Else
        b = 4
    End If
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), formatted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatCode_SingleLineIf_DoesNotIndentSubsequentLines()
    {
        string unformatted = 
@"Sub Test()
If a = 1 Then Exit Sub
b = 2
End Sub";

        string formatted = CodeFormatter.FormatCode(unformatted);

        string expected = 
@"Sub Test()
    If a = 1 Then Exit Sub
    b = 2
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), formatted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatCode_SelectCase_IndentsCaseAndBody()
    {
        string unformatted = 
@"Sub Test()
Select Case x
Case 1
y = 10
Case 2
y = 20
Case Else
y = 30
End Select
End Sub";

        string formatted = CodeFormatter.FormatCode(unformatted);

        string expected = 
@"Sub Test()
    Select Case x
        Case 1
            y = 10
        Case 2
            y = 20
        Case Else
            y = 30
    End Select
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), formatted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatCode_ForAndDoLoops_IndentsCorrectly()
    {
        string unformatted = 
@"Sub Loops()
For i = 1 To 10
Do While flag
x = x + 1
Loop
Next i
End Sub";

        string formatted = CodeFormatter.FormatCode(unformatted);

        string expected = 
@"Sub Loops()
    For i = 1 To 10
        Do While flag
            x = x + 1
        Loop
    Next i
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), formatted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatCode_CollapsesExcessiveBlankLines()
    {
        string unformatted = 
@"Sub Test()
    a = 1



    b = 2
End Sub";

        string formatted = CodeFormatter.FormatCode(unformatted, new CodeFormatterOptions { MaxConsecutiveBlankLines = 1 });

        string expected = 
@"Sub Test()
    a = 1

    b = 2
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), formatted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatCode_EnsuresBlankLineBetweenProcedures()
    {
        string unformatted = 
@"Sub First()
End Sub
Sub Second()
End Sub";

        string formatted = CodeFormatter.FormatCode(unformatted, new CodeFormatterOptions { EnsureBlankLineBetweenProcedures = true });

        string expected = 
@"Sub First()
End Sub

Sub Second()
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), formatted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void AlignAsInDeclarations_AlignsConsecutiveDimStatements()
    {
        string code = 
@"Sub Test()
    Dim x As Integer
    Dim longVariableName As String
    Dim flag As Boolean
End Sub";

        string aligned = CodeFormatter.AlignAsInDeclarations(code);

        string expected = 
@"Sub Test()
    Dim x                As Integer
    Dim longVariableName As String
    Dim flag             As Boolean
End Sub";

        Assert.Equal(expected.Replace("\r\n", "\n"), aligned.Replace("\r\n", "\n"));
    }
}
