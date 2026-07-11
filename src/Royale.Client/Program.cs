using Microsoft.Extensions.Logging;
using Royale.Client.Launch;
using Royale.Client.Platform;
using Royale.Diagnostics.Logging;
using Royale.Diagnostics.Telemetry;
using ZLogger;

using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);
ILogger logger = loggerFactory.CreateLogger("Royale.Client");

try
{
    ClientLaunchOptions options = ClientLaunchOptions.Parse(args);
    logger.ZLogInformation($"Client startup beginning. Mode {options.Mode}, map {options.MapId}, port {options.Port}.");

    if (options.Mode == ClientLaunchMode.Connect)
        logger.ZLogInformation($"Client remote endpoint selected: {options.ConnectHost}:{options.Port}. Networking transport will connect over UDP.");

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
