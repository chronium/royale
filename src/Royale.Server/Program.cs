using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Royale.Diagnostics;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation.World;
using ZLogger;

using RoyaleTelemetry telemetry = RoyaleTelemetry.CreateServer(LogLevel.Information);
ILogger logger = telemetry.LoggerFactory.CreateLogger("Royale.Server");
using CancellationTokenSource shutdown = new();
using Activity? serverRunActivity = RoyaleTelemetry.ServerActivitySource.StartActivity("royale.server.run");

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
    serverRunActivity?.SetTag("server.port", options.Port);
    serverRunActivity?.SetTag("server.map", runtime.MapId);
    serverRunActivity?.SetTag("server.tick_rate_hz", SimulationSettings.TickRateHz);
    serverRunActivity?.SetTag("server.headless", descriptor.IsHeadless);
    serverRunActivity?.SetTag("server.run_mode", options.RunTicks is null ? "until_shutdown" : "finite");

    logger.ZLogInformation(
        $"Royale server starting. Protocol {ProtocolConstants.Version}, port {options.Port}, map {runtime.MapId}, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}, run {runMode}, UDP listen enabled.");

    ServerSimulationRunResult result = await ServerSimulationLoop.RunAsync(runtime, options, shutdown.Token);
    serverRunActivity?.SetTag("server.ticks_run", result.TicksRun);

    logger.ZLogInformation($"Royale server stopped after {result.TicksRun} ticks.");
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    serverRunActivity?.SetStatus(ActivityStatusCode.Ok);
    logger.ZLogInformation($"Royale server shutdown requested.");
}
catch (Exception ex)
{
    serverRunActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    logger.ZLogCritical(ex, $"Fatal server startup error.");
    throw;
}
