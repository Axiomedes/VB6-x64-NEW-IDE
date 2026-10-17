using System;
using System.Linq;
using ViBa7.ProjectModel.ErrorHandling;
using Xunit;

namespace ViBa7.Tests;

public class ErrorHandlingEngineTests
{
    private const string SampleProc =
@"Public Sub Calculate(ByVal x As Integer)
    Dim y As Integer
    y = x * 2
End Sub";

    [Fact]
    public void HasErrorHandling_DetectsPresenceAccurately()
    {
        Assert.False(ErrorHandlingEngine.HasErrorHandling(SampleProc));

        string withOnError =
@"Public Sub Test()
    On Error GoTo ErrorHandler
    x = 1
ErrorHandler:
End Sub";
        Assert.True(ErrorHandlingEngine.HasErrorHandling(withOnError));

        string withResumeNext =
@"Public Sub Test()
    On Error Resume Next
    x = 1
End Sub";
        Assert.True(ErrorHandlingEngine.HasErrorHandling(withResumeNext));
    }

    [Fact]
    public void ApplySchemeToProcedure_InsertsHeaderAndHandlerWithMacroExpansion()
    {
        var scheme = ErrorScheme.GetPredefinedSchemes().First(); // MsgBox scheme
        string result = ErrorHandlingEngine.ApplySchemeToProcedure(SampleProc, "Calculate", "Sub", "Module1", scheme);

        Assert.Contains("On Error GoTo ErrorHandler", result);
        Assert.Contains("Exit Sub", result);
        Assert.Contains("ErrorHandler:", result);
        Assert.Contains("en Module1.Calculate", result);
        Assert.EndsWith("End Sub", result.TrimEnd());
    }

    [Fact]
    public void RemoveErrorHandlerFromProcedure_RestoresCleanCode()
    {
        var scheme = ErrorScheme.GetPredefinedSchemes().First();
        string applied = ErrorHandlingEngine.ApplySchemeToProcedure(SampleProc, "Calculate", "Sub", "Module1", scheme);
        Assert.True(ErrorHandlingEngine.HasErrorHandling(applied));

        string cleaned = ErrorHandlingEngine.RemoveErrorHandlerFromProcedure(applied, "Sub");

        Assert.False(ErrorHandlingEngine.HasErrorHandling(cleaned));
        Assert.DoesNotContain("ErrorHandler:", cleaned);
        Assert.DoesNotContain("Exit Sub", cleaned);
        Assert.Contains("y = x * 2", cleaned);
    }

    [Fact]
    public void ApplyToModule_AppliesToAllProcedures()
    {
        string moduleCode =
@"Option Explicit

Public Sub First()
    x = 1
End Sub

Public Function Second() As Long
    Second = 2
End Function";

        var scheme = ErrorScheme.GetPredefinedSchemes().First();
        string result = ErrorHandlingEngine.ApplyToModule(moduleCode, "ModTest", scheme, overwriteExisting: false, out int count);

        Assert.Equal(2, count);
        Assert.Contains("Exit Sub", result);
        Assert.Contains("Exit Function", result);
        Assert.Contains("en ModTest.First", result);
        Assert.Contains("en ModTest.Second", result);
    }

    [Fact]
    public void RemoveFromModule_CleansAllProcedures()
    {
        string moduleCode =
@"Option Explicit

Public Sub First()
    x = 1
End Sub

Public Function Second() As Long
    Second = 2
End Function";

        var scheme = ErrorScheme.GetPredefinedSchemes().First();
        string withHandlers = ErrorHandlingEngine.ApplyToModule(moduleCode, "ModTest", scheme, overwriteExisting: false, out _);

        string cleaned = ErrorHandlingEngine.RemoveFromModule(withHandlers, out int removedCount);

        Assert.Equal(2, removedCount);
        Assert.DoesNotContain("On Error GoTo", cleaned);
        Assert.DoesNotContain("ErrorHandler:", cleaned);
    }
}
