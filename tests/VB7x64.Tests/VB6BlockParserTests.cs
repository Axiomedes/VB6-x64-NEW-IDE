using VB7x64.ProjectModel.Advanced;
using Xunit;

namespace VB7x64.Tests;

public class VB6BlockParserTests
{
    [Fact]
    public void ParseBlocks_DetectsNestedSubAndIfBlocks()
    {
        string code = """
            Public Sub Test()
                Dim x As Integer
                If x > 10 Then
                    MsgBox "Mayor"
                End If
            End Sub
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        Assert.Single(blocks);
        var subBlock = blocks[0];
        Assert.Equal("Sub", subBlock.BlockType);
        Assert.Equal(1, subBlock.StartLine);
        Assert.Equal(6, subBlock.EndLine);

        Assert.Single(subBlock.Children);
        var ifBlock = subBlock.Children[0];
        Assert.Equal("If", ifBlock.BlockType);
        Assert.Equal(3, ifBlock.StartLine);
        Assert.Equal(5, ifBlock.EndLine);
    }

    [Fact]
    public void ParseBlocks_IgnoresSingleLineIf()
    {
        string code = """
            Public Sub Test()
                If x > 10 Then MsgBox "Single line"
                Dim y As Integer
            End Sub
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        Assert.Single(blocks);
        var subBlock = blocks[0];
        Assert.Empty(subBlock.Children);
    }

    [Fact]
    public void ParseBlocks_DetectsSelectCaseAndForNext()
    {
        string code = """
            Public Function Calculate() As Long
                Select Case status
                    Case 1
                        For i = 1 To 10
                            total = total + i
                        Next i
                End Select
            End Function
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        Assert.Single(blocks);
        var fnBlock = blocks[0];
        Assert.Equal("Function", fnBlock.BlockType);

        Assert.Single(fnBlock.Children);
        var selBlock = fnBlock.Children[0];
        Assert.Equal("Select", selBlock.BlockType);

        Assert.Single(selBlock.Children);
        var forBlock = selBlock.Children[0];
        Assert.Equal("For", forBlock.BlockType);
    }

    [Fact]
    public void ParseBlocks_DetectsIntermediateLines()
    {
        string code = """
            Public Sub Test()
                If x > 10 Then
                    MsgBox 1
                ElseIf x > 5 Then
                    MsgBox 2
                Else
                    MsgBox 3
                End If
            End Sub
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        Assert.Single(blocks);
        var ifBlock = blocks[0].Children[0];
        Assert.Equal(2, ifBlock.IntermediateLines.Count);
        Assert.Equal(4, ifBlock.IntermediateLines[0]); // ElseIf
        Assert.Equal(6, ifBlock.IntermediateLines[1]); // Else
    }

    [Fact]
    public void ParseBlocks_IgnoresDeclarationsAndTypeEnum()
    {
        string code = """
            Option Explicit
            Private Type Customer
                ID As Long
                Name As String
            End Type
            Private Enum Status
                Pending = 0
                Active = 1
            End Enum
            Private Declare Sub Sleep Lib "kernel32" (ByVal ms As Long)

            Public Sub DoWork()
                If True Then
                    ' code
                End If
            End Sub
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        // Solo debe retornar el procedimiento 'DoWork', nada de Type, Enum o declaraciones
        Assert.Single(blocks);
        Assert.Equal("Sub", blocks[0].BlockType);
        Assert.Equal(12, blocks[0].StartLine);
        Assert.Equal(16, blocks[0].EndLine);
    }

    [Fact]
    public void ParseBlocks_DiscardsUnclosedProcedureAtEOF()
    {
        string code = """
            Public Sub Incomplete()
                Dim x As Integer
                If x > 0 Then
                    MsgBox "Test"
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        // No debe retornar ningún bloque incompleto que se extienda indefinidamente
        Assert.Empty(blocks);
    }

    [Fact]
    public void ParseBlocks_SupportsMultilineIfWithUnderscore()
    {
        string code = """
            Public Sub Test()
                If x > 10 And _
                   y > 20 Then
                    MsgBox "Multi-line If"
                End If
            End Sub
            """;

        var blocks = VB6BlockParser.ParseBlocks(code);

        Assert.Single(blocks);
        Assert.Single(blocks[0].Children);
        Assert.Equal("If", blocks[0].Children[0].BlockType);
        Assert.Equal(2, blocks[0].Children[0].StartLine);
        Assert.Equal(5, blocks[0].Children[0].EndLine);
    }

    [Fact]
    public void ParseBlocks_SupportsTabIndentation()
    {
        string code = "Public Sub Test()\r\n\tIf x > 0 Then\r\n\t\tMsgBox \"Tab\"\r\n\tEnd If\r\nEnd Sub";

        var blocks = VB6BlockParser.ParseBlocks(code);

        Assert.Single(blocks);
        Assert.Single(blocks[0].Children);
        var ifBlock = blocks[0].Children[0];
        Assert.Equal("If", ifBlock.BlockType);
        Assert.Equal(1, ifBlock.StartColumn);
    }
}
