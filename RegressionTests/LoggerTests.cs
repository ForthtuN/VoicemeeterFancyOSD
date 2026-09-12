using AtgDev.Utils;
using Xunit;

namespace VoicemeeterFancyOsd.RegressionTests;

public sealed class LoggerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"vmfosd-logger-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Logger_DrainsConcurrentWritersBeforeDisposeCompletes()
    {
        Directory.CreateDirectory(root);
        Logger logger = new(root) { IsEnabled = true };

        const int writers = 8;
        const int messagesPerWriter = 100;
        Task[] tasks = Enumerable.Range(0, writers)
            .Select(writer => Task.Run(() =>
            {
                for (int i = 0; i < messagesPerWriter; i++)
                {
                    logger.Log($"writer={writer} message={i}");
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);
        await logger.DisposeAsync();

        string[] logFiles = Directory.GetFiles(root, "*.log", SearchOption.TopDirectoryOnly);
        Assert.Single(logFiles);
        Assert.Equal(writers * messagesPerWriter, File.ReadLines(logFiles[0]).Count());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
