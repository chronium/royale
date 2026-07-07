using Microsoft.Extensions.Logging;
using ZLogger;

namespace Royale.Diagnostics;

public static class RoyaleLogging
{
    public static ILoggerFactory CreateConsoleLoggerFactory(LogLevel minimumLevel)
    {
        return CreateLoggerFactory(
            minimumLevel,
            logging => logging.AddZLoggerConsole(ConfigureZLoggerOptions));
    }

    internal static ILoggerFactory CreateStreamLoggerFactory(Stream stream, LogLevel minimumLevel)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return CreateLoggerFactory(
            minimumLevel,
            logging => logging.AddZLoggerStream(stream, ConfigureZLoggerOptions));
    }

    private static ILoggerFactory CreateLoggerFactory(
        LogLevel minimumLevel,
        Action<ILoggingBuilder> configureOutput)
    {
        return LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(minimumLevel);
            configureOutput(logging);
        });
    }

    internal static void ConfigureZLoggerOptions(ZLoggerOptions options)
    {
        options.UsePlainTextFormatter(formatter =>
        {
            formatter.SetPrefixFormatter(
                $"{0:utc-longdate} [{1:short}] {2}: ",
                (in MessageTemplate template, in LogInfo info) => template.Format(
                    info.Timestamp,
                    info.LogLevel,
                    info.Category));
        });
    }
}
