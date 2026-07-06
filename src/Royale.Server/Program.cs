using Microsoft.Extensions.Logging;
using Royale.Diagnostics;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation.World;
using ZLogger;

using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);
ILogger logger = loggerFactory.CreateLogger("Royale.Server");
using CancellationTokenSource shutdown = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

try
{
    ServerLaunchOptions options = ServerLaunchOptions.Parse(args);
    var descriptor = ServerDescriptor.Create();
    using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(options.MapId);
    string runMode = options.RunTicks is int runTicks
        ? $"finite {runTicks} ticks"
        : "until shutdown";

    logger.ZLogInformation(
        $"Royale server starting. Protocol {ProtocolConstants.Version}, port {options.Port}, map {simulation.MapId}, colliders {simulation.StaticColliderCount}, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}, run {runMode}.");

    ServerSimulationRunResult result = await ServerSimulationLoop.RunAsync(simulation, options, shutdown.Token);

    logger.ZLogInformation($"Royale server stopped after {result.TicksRun} ticks.");
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    logger.ZLogInformation($"Royale server shutdown requested.");
}
catch (Exception ex)
{
    logger.ZLogCritical(ex, $"Fatal server startup error.");
    throw;
}
