using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Royale.Diagnostics.Logging;
using Royale.Diagnostics.Telemetry;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Server.Bots;
using Royale.Server.Launch;
using Royale.Server.Match;
using Royale.Server.Networking;
using Royale.Server.Observability;
using Royale.Server.Sessions;
using Royale.Server.Simulation;
using Royale.Simulation.World;
using Royale.Content.Runtime;
using ZLogger;

using RoyaleTelemetry telemetry = RoyaleTelemetry.CreateServer(LogLevel.Information);
ILogger logger = telemetry.LoggerFactory.CreateLogger("Royale.Server");
using ServerObservability observability = new(telemetry.LoggerFactory);
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
    RuntimeContentSelection content = RuntimeContentSelection.Load(
        options.MapId,
        options.MapFile,
        options.RequireMapIdMatch,
        options.AssetRoot);
    var descriptor = ServerDescriptor.Create();
    using NetworkServerRuntime runtime = NetworkServerRuntime.Listen(
        content.Map,
        content.AssetRoot,
        options.Port,
        new MatchStartSettings(
            options.MinimumPlayers,
            options.TargetPlayers,
            options.WaitingSeconds,
            options.PreparationSeconds),
        observability);
    Console.Out.WriteLine($"ROYALE_SERVER_READY map={runtime.MapId} port={options.Port}");
    Console.Out.Flush();
    string runMode = options.RunTicks is int runTicks
        ? $"finite {runTicks} ticks"
        : "until shutdown";
    serverRunActivity?.SetTag("server.port", options.Port);
    serverRunActivity?.SetTag("server.map", runtime.MapId);
    serverRunActivity?.SetTag("server.tick_rate_hz", SimulationSettings.TickRateHz);
    serverRunActivity?.SetTag("server.headless", descriptor.IsHeadless);
    serverRunActivity?.SetTag("server.run_mode", options.RunTicks is null ? "until_shutdown" : "finite");
    serverRunActivity?.SetTag("server.minimum_players", options.MinimumPlayers);
    serverRunActivity?.SetTag("server.target_players", options.TargetPlayers);
    serverRunActivity?.SetTag("server.waiting_seconds", options.WaitingSeconds);
    serverRunActivity?.SetTag("server.preparation_seconds", options.PreparationSeconds);

    logger.ZLogInformation(
        $"Royale server starting. Protocol {ProtocolConstants.Version}, port {options.Port}, map {runtime.MapId}, minimum players {options.MinimumPlayers}, target players {options.TargetPlayers}, waiting {options.WaitingSeconds} seconds, preparation {options.PreparationSeconds} seconds, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}, run {runMode}, UDP listen enabled.");

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
