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
}
