using System;
using ViBa7.ProjectModel.ErrorHandling;
using Xunit;

namespace ViBa7.Tests;

public class TracingEngineTests
{
    private const string SampleProc =
@"Public Sub DoProcess()
    x = 100
End Sub";

    [Fact]
    public void InjectTracesIntoProcedure_InsertsEntryAndExitTraces()
    {
        var opts = new TracingOptions();
        string result = TracingEngine.InjectTracesIntoProcedure(SampleProc, "DoProcess", "Sub", "Module1", opts);

        Assert.True(TracingEngine.HasInjectedTraces(result));
        Assert.Contains("--> Entrando a Module1.DoProcess", result);
        Assert.Contains("<-- Saliendo de Module1.DoProcess", result);
        Assert.Contains(TracingOptions.DefaultTag, result);
    }

    [Fact]
    public void RemoveTracesFromProcedure_RemovesInjectedLinesSafely()
    {
        var opts = new TracingOptions();
        string injected = TracingEngine.InjectTracesIntoProcedure(SampleProc, "DoProcess", "Sub", "Module1", opts);
        Assert.True(TracingEngine.HasInjectedTraces(injected));

        string cleaned = TracingEngine.RemoveTracesFromProcedure(injected, opts.TraceTag);

        Assert.False(TracingEngine.HasInjectedTraces(cleaned));
        Assert.DoesNotContain("--> Entrando", cleaned);
        Assert.DoesNotContain("<-- Saliendo", cleaned);
        Assert.Contains("x = 100", cleaned);
    }

    [Fact]
    public void InjectAndRemoveFromModule_ProcessesAllProcedures()
    {
        string moduleCode =
@"Option Explicit

Public Sub RoutineOne()
    a = 1
End Sub

Public Sub RoutineTwo()
    b = 2
End Sub";

        var opts = new TracingOptions();
        string injected = TracingEngine.InjectTracesIntoModule(moduleCode, "Mod1", opts, out int injectedCount);

        Assert.Equal(2, injectedCount);
        Assert.Contains("--> Entrando a Mod1.RoutineOne", injected);
        Assert.Contains("--> Entrando a Mod1.RoutineTwo", injected);

        string cleaned = TracingEngine.RemoveTracesFromModule(injected, opts.TraceTag, out int cleanedCount);

        Assert.Equal(2, cleanedCount);
        Assert.DoesNotContain("--> Entrando", cleaned);
        Assert.Contains("a = 1", cleaned);
        Assert.Contains("b = 2", cleaned);
    }
}
