using Microsoft.Extensions.Logging;
using Royale.Client.Platform;
using Royale.Diagnostics;
using ZLogger;

using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);
ILogger logger = loggerFactory.CreateLogger("Royale.Client");

try
{
    logger.ZLogInformation($"Client startup beginning.");
    SdlApplicationOptions options = SdlApplicationOptions.Parse(args);

    using var application = new SdlApplication(
        options,
        loggerFactory.CreateLogger<SdlApplication>());
    application.Run();
}
catch (Exception ex)
{
    logger.ZLogCritical(ex, $"Fatal client startup error.");
    throw;
}
