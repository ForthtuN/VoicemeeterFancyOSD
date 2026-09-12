using AtgDev.Utils.ZipFileExtensions;
using System.IO.Compression;
using Xunit;

namespace VoicemeeterFancyOsd.RegressionTests;

public sealed class ZipFileExtensionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"vmfosd-updater-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExtractToDirectoryAsync_RejectsParentTraversal()
    {
        Directory.CreateDirectory(root);
        string archivePath = Path.Combine(root, "update.zip");
        string destination = Path.Combine(root, "extract");
        string escapedPath = Path.Combine(root, "escaped.txt");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../escaped.txt");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("escape");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ZipFileExtension.ExtractToDirectoryAsync(archivePath, destination));

        Assert.False(File.Exists(escapedPath));
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_CreatesImplicitParentDirectories()
    {
        Directory.CreateDirectory(root);
        string archivePath = Path.Combine(root, "update.zip");
        string destination = Path.Combine(root, "extract");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("VoicemeeterFancyOSD/assets/file.txt");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("nested");
        }

        await ZipFileExtension.ExtractToDirectoryAsync(archivePath, destination);

        Assert.Equal(
            "nested",
            await File.ReadAllTextAsync(Path.Combine(destination, "VoicemeeterFancyOSD", "assets", "file.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
