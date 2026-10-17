using ViBa7.ProjectModel.Analysis;
using Xunit;

namespace ViBa7.Tests;

public class ProjectStatisticsTests
{
    [Fact]
    public void AnalyzeText_CalculatesSlocCommentsAndProceduresAccurately()
    {
        string code = 
@"Option Explicit
' Este es un comentario
Dim x As Integer

' Comentario antes de Sub
Public Sub DoWork()
    x = 10
    ' Comentario interno
End Sub

Public Function Calculate() As Long
    Calculate = 100
End Function";

        var stat = ProjectStatistics.AnalyzeText(code, "Module1.bas");

        Assert.Equal("Module1.bas", stat.FileName);
        Assert.Equal(13, stat.TotalLines);
        Assert.Equal(3, stat.CommentLines);
        Assert.Equal(2, stat.BlankLines);
        Assert.Equal(8, stat.CodeLines); // 13 total - 3 comments - 2 blank = 8 SLOC
        Assert.Equal(2, stat.ProcedureCount); // DoWork y Calculate
        Assert.True(stat.CommentPercentage > 0);
    }

    [Fact]
    public void ProjectStatisticsSummary_AggregatesTotalsProperly()
    {
        var stat1 = new FileCodeStatistics
        {
            FileName = "Form1.frm",
            FileType = "Formulario (.frm)",
            TotalLines = 100,
            CodeLines = 70,
            CommentLines = 20,
            BlankLines = 10,
            ProcedureCount = 5
        };

        var stat2 = new FileCodeStatistics
        {
            FileName = "Module1.bas",
            FileType = "Módulo Estándar (.bas)",
            TotalLines = 50,
            CodeLines = 40,
            CommentLines = 5,
            BlankLines = 5,
            ProcedureCount = 3
        };

        var summary = new ProjectStatisticsSummary
        {
            ProjectName = "TestProject",
            Files = { stat1, stat2 }
        };

        Assert.Equal(2, summary.TotalFiles);
        Assert.Equal(150, summary.TotalLines);
        Assert.Equal(110, summary.TotalCodeLines);
        Assert.Equal(25, summary.TotalCommentLines);
        Assert.Equal(15, summary.TotalBlankLines);
        Assert.Equal(8, summary.TotalProcedures);
        Assert.Equal(1, summary.FormCount);
        Assert.Equal(1, summary.ModuleCount);
    }
}
