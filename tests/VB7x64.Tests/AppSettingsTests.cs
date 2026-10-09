using VB7x64.ProjectModel.Settings;
using Xunit;

namespace VB7x64.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AddRecentProject_PrependsAndAvoidsDuplicates()
    {
        var settings = new AppSettings();
        settings.AddRecentProject(@"C:\Proj1\App.vbp");
        settings.AddRecentProject(@"C:\Proj2\App.vbp");
        settings.AddRecentProject(@"C:\Proj1\App.vbp");

        Assert.Equal(2, settings.RecentProjects.Count);
        Assert.Equal(@"C:\Proj1\App.vbp", settings.RecentProjects[0]);
        Assert.Equal(@"C:\Proj2\App.vbp", settings.RecentProjects[1]);
    }

    [Fact]
    public void AddRecentFile_LimitsToMaxTen()
    {
        var settings = new AppSettings();
        for (int i = 1; i <= 15; i++)
        {
            settings.AddRecentFile($@"C:\Test\File{i}.bas");
        }

        Assert.Equal(10, settings.RecentFiles.Count);
        Assert.Equal(@"C:\Test\File15.bas", settings.RecentFiles[0]);
    }

    [Fact]
    public void ApplyThemePreset_UpdatesAllEditorColors()
    {
        var settings = new AppSettings();

        // Default Classic VB6
        Assert.Equal("#FFFFFF", settings.EditorBackgroundColor);
        Assert.Equal("#000000", settings.EditorForegroundColor);
        Assert.Equal("#0000FF", settings.KeywordColor);

        // Apply Dark Modern
        settings.ApplyThemePreset("Oscuro Moderno");
        Assert.Equal("Oscuro Moderno", settings.Theme);
        Assert.Equal("#1E1E1E", settings.EditorBackgroundColor);
        Assert.Equal("#D4D4D4", settings.EditorForegroundColor);
        Assert.Equal("#569CD6", settings.KeywordColor);
        Assert.Equal("#DCDCAA", settings.MethodNameColor);

        // Apply High Contrast
        settings.ApplyThemePreset("Alto Contraste");
        Assert.Equal("Alto Contraste", settings.Theme);
        Assert.Equal("#000000", settings.EditorBackgroundColor);
        Assert.Equal("#FFFFFF", settings.EditorForegroundColor);
        Assert.Equal("#00FFFF", settings.KeywordColor);

        // Reset to Classic VB6
        settings.ApplyThemePreset("Clásico VB6");
        Assert.Equal("Clásico VB6", settings.Theme);
        Assert.Equal("#FFFFFF", settings.EditorBackgroundColor);
        Assert.Equal("#000000", settings.EditorForegroundColor);
        Assert.Equal("#0000FF", settings.KeywordColor);
    }
}
