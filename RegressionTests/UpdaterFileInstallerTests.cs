namespace VoicemeeterFancyOsd.RegressionTests;

using VoicemeeterOsdProgram.Updater;
using Xunit;

public sealed class UpdaterFileInstallerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"vmfosd-installer-tests-{Guid.NewGuid():N}");

    [Fact]
    public void TryInstall_InstallsNestedFilesAndBacksUpExistingFiles()
    {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(Path.Combine(source, "assets", "icons"));
        Directory.CreateDirectory(Path.Combine(destination, "assets", "icons"));

        File.WriteAllText(Path.Combine(source, "app.exe"), "new-app");
        File.WriteAllText(Path.Combine(source, "assets", "icons", "icon.txt"), "new-icon");
        File.WriteAllText(Path.Combine(destination, "app.exe"), "old-app");
        File.WriteAllText(Path.Combine(destination, "assets", "icons", "icon.txt"), "old-icon");

        bool result = UpdaterFileInstaller.TryInstall(source, destination, ".backup", null);

        Assert.True(result);
        Assert.Equal("new-app", File.ReadAllText(Path.Combine(destination, "app.exe")));
        Assert.Equal("new-icon", File.ReadAllText(Path.Combine(destination, "assets", "icons", "icon.txt")));
        Assert.Equal("old-app", File.ReadAllText(Path.Combine(destination, ".backup", "app.exe")));
        Assert.Equal("old-icon", File.ReadAllText(Path.Combine(destination, ".backup", "assets", "icons", "icon.txt")));
    }

    [Fact]
    public void TryInstall_AllowsNullProgressForNonEmptyUpdate()
    {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "file.txt"), "content");

        bool result = UpdaterFileInstaller.TryInstall(source, destination, ".backup", null);

        Assert.True(result);
        Assert.Equal("content", File.ReadAllText(Path.Combine(destination, "file.txt")));
    }

    [Fact]
    public void TryInstall_PreservesEmptyDirectoriesFromRelease()
    {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(Path.Combine(source, "assets", "empty", "nested"));
        Directory.CreateDirectory(destination);

        bool result = UpdaterFileInstaller.TryInstall(source, destination, ".backup", null);

        Assert.True(result);
        Assert.True(Directory.Exists(Path.Combine(destination, "assets", "empty", "nested")));
    }

    [Fact]
    public void TryInstall_RollsBackFilesAlreadyReplacedWhenLaterFileFails()
    {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        File.WriteAllText(Path.Combine(source, "a.txt"), "new-a");
        File.WriteAllText(Path.Combine(source, "z.txt"), "new-z");
        File.WriteAllText(Path.Combine(destination, "a.txt"), "old-a");
        Directory.CreateDirectory(Path.Combine(destination, "z.txt"));

        bool result = UpdaterFileInstaller.TryInstall(source, destination, ".backup", null);

        Assert.False(result);
        Assert.Equal("old-a", File.ReadAllText(Path.Combine(destination, "a.txt")));
        Assert.True(Directory.Exists(Path.Combine(destination, "z.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
