using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VB7x64.ProjectModel.Navigation;
using Xunit;

namespace VB7x64.Tests;

public class NavigationFeaturesTests
{
    [Fact]
    public void ReferenceFinder_FindsDefinitionsAndUsages_ExcludesStringsAndComments()
    {
        string code = @"
Public Sub ProcessOrder(ByVal orderId As Long)
    ' This comment mentions ProcessOrder but is not code
    Dim msg As String
    msg = ""Calling ProcessOrder inside string""
    Call ProcessOrder(orderId + 1)
End Sub

Public Sub AnotherMethod()
    ProcessOrder 100
End Sub
";
        string fakeFile = @"C:\FakeProject\Module1.bas";
        var results = ReferenceFinder.FindReferences("ProcessOrder", [fakeFile], path => code);

        // Debería encontrar:
        // 1. Declaración en 'Public Sub ProcessOrder...'
        // 2. Llamada en 'Call ProcessOrder(orderId + 1)'
        // 3. Llamada en 'ProcessOrder 100'
        // Y NO encontrar dentro del comentario ni de la cadena entre comillas.
        Assert.Equal(3, results.Count);

        var decl = results.First(r => r.IsDeclaration);
        Assert.Equal(2, decl.LineNumber);
        Assert.Equal("Sub ProcessOrder", decl.ContainerName);

        var calls = results.Where(r => !r.IsDeclaration).ToList();
        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, c => c.LineNumber == 6 && c.ContainerName == "Sub ProcessOrder");
        Assert.Contains(calls, c => c.LineNumber == 10 && c.ContainerName == "Sub AnotherMethod");
    }

    [Fact]
    public void ReferenceFinder_IsCaseInsensitive()
    {
        string code = @"
Dim TotalCount As Long
TotalCount = totalcount + 1
TOTALCOUNT = 50
";
        string fakeFile = @"C:\FakeProject\Module1.bas";
        var results = ReferenceFinder.FindReferences("totalcount", [fakeFile], path => code);

        // Línea 1: Dim TotalCount
        // Línea 2: TotalCount = totalcount + 1 (2 ocurrencias: asignación y lectura)
        // Línea 3: TOTALCOUNT = 50
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void ReferenceFinder_ExtractCodeRanges_HandlesStringsAndComments()
    {
        string line = @"Dim x As String: x = ""Hello ' World"" ' Real comment here";
        var ranges = ReferenceFinder.ExtractCodeRanges(line);

        // El texto dentro de "Hello ' World" y después de ' Real comment no debe incluirse
        string combined = string.Concat(ranges.Select(r => line.Substring(r.Start, r.Length)));
        Assert.Contains("Dim x As String", combined);
        Assert.DoesNotContain("Real comment", combined);
    }

    [Fact]
    public void TaskScanner_FindsMultiplePrioritiesAndTags()
    {
        string code = @"
' TODO: Implement login form validation
Sub Main()
    ' FIXME: Fix buffer overrun when packet is large
    Dim a As Integer
    ' NOTE: Temporary workaround for legacy API
    ' BUG: Crash when clicking Cancel
    ' Normal comment without tag
End Sub
";
        string fakeFile = @"C:\FakeProject\Main.bas";
        var tasks = TaskScanner.ScanTasks([fakeFile], path => code);

        Assert.Equal(4, tasks.Count);

        // Verificar que BUG y FIXME tienen prioridad Alta
        var highTasks = tasks.Where(t => t.Priority == TaskPriority.High).ToList();
        Assert.Equal(2, highTasks.Count);
        Assert.Contains(highTasks, t => t.Tag == "BUG");
        Assert.Contains(highTasks, t => t.Tag == "FIXME");

        // Verificar que TODO tiene prioridad Normal
        var todo = tasks.FirstOrDefault(t => t.Tag == "TODO");
        Assert.NotNull(todo);
        Assert.Equal(TaskPriority.Normal, todo.Priority);
        Assert.Equal("Implement login form validation", todo.Description);

        // Verificar que NOTE tiene prioridad Baja
        var note = tasks.FirstOrDefault(t => t.Tag == "NOTE");
        Assert.NotNull(note);
        Assert.Equal(TaskPriority.Low, note.Priority);
    }

    [Fact]
    public void BookmarkManager_ToggleAndNavigateBookmarks()
    {
        var bm = BookmarkManager.Instance;
        string file = @"C:\FakeProject\Form1.frm";
        bm.ClearAllBookmarks();

        Assert.False(bm.HasBookmark(file, 10));

        // Toggle on
        bool added = bm.ToggleBookmark(file, 10);
        Assert.True(added);
        Assert.True(bm.HasBookmark(file, 10));

        // Add another bookmark at line 25
        bm.ToggleBookmark(file, 25);
        Assert.Equal(2, bm.GetBookmarks(file).Count);

        // Navigate Next
        var next = bm.GetNextBookmark(file, 10);
        Assert.NotNull(next);
        Assert.Equal(25, next.Value.Line);

        // Navigate Next wrap around
        var nextWrap = bm.GetNextBookmark(file, 25);
        Assert.NotNull(nextWrap);
        Assert.Equal(10, nextWrap.Value.Line);

        // Navigate Previous
        var prev = bm.GetPreviousBookmark(file, 25);
        Assert.NotNull(prev);
        Assert.Equal(10, prev.Value.Line);

        // Toggle off
        bool removed = !bm.ToggleBookmark(file, 10);
        Assert.True(removed);
        Assert.False(bm.HasBookmark(file, 10));

        bm.ClearAllBookmarks();
        Assert.Empty(bm.GetBookmarks(file));
    }

    [Fact]
    public void NavigationHistory_BackAndForwardStacks()
    {
        var hist = NavigationHistory.Instance;
        hist.Clear();

        string file1 = @"C:\Test\File1.bas";
        string file2 = @"C:\Test\File2.bas";

        hist.RecordNavigation(file1, 10);
        hist.RecordNavigation(file2, 50);

        Assert.True(hist.CanNavigateBack);
        Assert.False(hist.CanNavigateForward);

        // Navigate back from file2, line 50
        var backTarget = hist.NavigateBackward(file2, 50);
        Assert.NotNull(backTarget);
        Assert.Equal(file2, backTarget.FilePath);
        Assert.Equal(50, backTarget.LineNumber);

        var prevTarget = hist.NavigateBackward(file2, 50);
        Assert.NotNull(prevTarget);
        Assert.Equal(file1, prevTarget.FilePath);
        Assert.Equal(10, prevTarget.LineNumber);

        Assert.True(hist.CanNavigateForward);

        // Navigate forward
        var fwdTarget = hist.NavigateForward(file1, 10);
        Assert.NotNull(fwdTarget);
        Assert.Equal(file2, fwdTarget.FilePath);

        hist.Clear();
    }
}
