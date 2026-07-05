using System.Text;
using Microsoft.Extensions.Logging;
using Royale.Diagnostics;
using ZLogger;

namespace Royale.Diagnostics.Tests;

public sealed class RoyaleLoggingTests
{
    [Fact]
    public void ConsoleLoggerFactoryCanBeCreatedAndDisposed()
    {
        using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);

        ILogger logger = loggerFactory.CreateLogger("Royale.Diagnostics.Tests");

        Assert.NotNull(logger);
    }

    [Fact]
    public void MinimumLevelFiltersEntries()
    {
        using var stream = new NonClosingMemoryStream();
        ILoggerFactory loggerFactory = RoyaleLogging.CreateStreamLoggerFactory(stream, LogLevel.Warning);
        ILogger logger = loggerFactory.CreateLogger("Royale.Tests.Filter");

        logger.ZLogInformation($"filtered message");
        logger.ZLogWarning($"visible message");
        loggerFactory.Dispose();

        string output = ReadOutput(stream);

        Assert.DoesNotContain("filtered message", output, StringComparison.Ordinal);
        Assert.Contains("visible message", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EmittedLineIncludesTimestampLevelCategoryAndMessage()
    {
        using var stream = new NonClosingMemoryStream();
        ILoggerFactory loggerFactory = RoyaleLogging.CreateStreamLoggerFactory(stream, LogLevel.Trace);
        ILogger logger = loggerFactory.CreateLogger("Royale.Tests.Shape");

        logger.ZLogInformation($"startup complete");
        loggerFactory.Dispose();

        string output = ReadOutput(stream);
        string line = Assert.Single(SplitLines(output));

        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[INF\] Royale\.Tests\.Shape: startup complete$", line);
    }

    [Fact]
    public void RepeatedLogCallsProduceOneLinePerEntry()
    {
        using var stream = new NonClosingMemoryStream();
        ILoggerFactory loggerFactory = RoyaleLogging.CreateStreamLoggerFactory(stream, LogLevel.Information);
        ILogger logger = loggerFactory.CreateLogger("Royale.Tests.Lines");

        logger.ZLogInformation($"first");
        logger.ZLogInformation($"second");
        logger.ZLogInformation($"third");
        loggerFactory.Dispose();

        string[] lines = SplitLines(ReadOutput(stream));

        Assert.Equal(3, lines.Length);
        Assert.EndsWith("first", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("second", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("third", lines[2], StringComparison.Ordinal);
    }

    private static string ReadOutput(MemoryStream stream)
    {
        stream.Position = 0;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string[] SplitLines(string output)
    {
        return output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class NonClosingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            Flush();
        }
    }
}
