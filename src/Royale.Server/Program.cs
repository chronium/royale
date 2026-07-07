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
    using NetworkServerRuntime runtime = NetworkServerRuntime.Listen(options.MapId, options.Port);
    string runMode = options.RunTicks is int runTicks
        ? $"finite {runTicks} ticks"
        : "until shutdown";

    logger.ZLogInformation(
        $"Royale server starting. Protocol {ProtocolConstants.Version}, port {options.Port}, map {runtime.MapId}, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}, run {runMode}, UDP listen enabled.");

    ServerSimulationRunResult result = await ServerSimulationLoop.RunAsync(runtime, options, shutdown.Token);

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
