using VB7x64.ProjectModel.Analysis;
using Xunit;

namespace VB7x64.Tests;

public class DiffEngineTests
{
    [Fact]
    public void Compare_IdenticalTexts_ReturnsOnlyUnchangedLines()
    {
        string text = "Line 1\r\nLine 2\r\nLine 3";
        var result = DiffEngine.Compare(text, text);

        Assert.False(result.Summary.HasDifferences);
        Assert.Equal(0, result.Summary.TotalChanges);
        Assert.Equal(3, result.Summary.UnchangedCount);
        Assert.All(result.Lines, l => Assert.Equal(DiffLineType.Unchanged, l.Type));
    }

    [Fact]
    public void Compare_InsertedAndDeletedLines_DetectedCorrectly()
    {
        string oldText = "Line 1\r\nLine 2\r\nLine 3";
        string newText = "Line 1\r\nLine 2\r\nLine 2.5\r\nLine 3";

        var result = DiffEngine.Compare(oldText, newText);

        Assert.True(result.Summary.HasDifferences);
        Assert.Equal(1, result.Summary.InsertedCount);
        Assert.Equal(0, result.Summary.DeletedCount);
        Assert.Contains(result.Lines, l => l.Type == DiffLineType.Inserted && l.Text == "Line 2.5");
    }

    [Fact]
    public void Compare_ModifiedLines_PairedAppropriately()
    {
        string oldText = "Dim a As Integer\r\nDim b As String";
        string newText = "Dim a As Long\r\nDim b As String";

        var result = DiffEngine.Compare(oldText, newText);

        Assert.True(result.Summary.HasDifferences);
        Assert.Equal(1, result.Summary.ModifiedCount);
        var modLine = Assert.Single(result.Lines, l => l.Type == DiffLineType.Modified);
        Assert.Equal("Dim a As Long", modLine.Text);
        Assert.Equal("Dim a As Integer", modLine.OldText);
    }
}
