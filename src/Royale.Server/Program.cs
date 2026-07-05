using Microsoft.Extensions.Logging;
using Royale.Content;
using Royale.Diagnostics;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation;
using ZLogger;

var descriptor = ServerDescriptor.Create();
using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);
ILogger logger = loggerFactory.CreateLogger("Royale.Server");

logger.ZLogInformation(
    $"Royale server skeleton ready. Protocol {ProtocolConstants.Version}, map {ContentCatalog.DefaultMapId}, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}.");
