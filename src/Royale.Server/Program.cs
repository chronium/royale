using Microsoft.Extensions.Logging;
using Royale.Diagnostics;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation;
using ZLogger;

using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);
ILogger logger = loggerFactory.CreateLogger("Royale.Server");

try
{
    ServerLaunchOptions options = ServerLaunchOptions.Parse(args);
    var descriptor = ServerDescriptor.Create();

    logger.ZLogInformation(
        $"Royale server skeleton ready. Protocol {ProtocolConstants.Version}, map {options.MapId}, port {options.Port}, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}.");
}
catch (Exception ex)
{
    logger.ZLogCritical(ex, $"Fatal server startup error.");
    throw;
}
