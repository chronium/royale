using System.Diagnostics;
using Royale.Simulation.World;

namespace Royale.Server;

public static class ServerSimulationLoop
{
    public const int MaxCatchUpTicksPerCycle = 5;

    public static Task<ServerSimulationRunResult> RunAsync(
        HeadlessServerSimulation simulation,
        ServerLaunchOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(options);

        if (options.RunTicks is int finiteTickCount)
            return RunFiniteAsync(simulation, finiteTickCount, cancellationToken);

        return RunUntilCancelledAsync(simulation, cancellationToken);
    }

    private static Task<ServerSimulationRunResult> RunFiniteAsync(
        HeadlessServerSimulation simulation,
        int runTicks,
        CancellationToken cancellationToken)
    {
        for (int tick = 0; tick < runTicks; tick++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            simulation.Step();
        }

        return Task.FromResult(new ServerSimulationRunResult((ulong)runTicks));
    }

    private static async Task<ServerSimulationRunResult> RunUntilCancelledAsync(
        HeadlessServerSimulation simulation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        long previousElapsedTicks = stopwatch.ElapsedTicks;
        double accumulatedSeconds = 0.0d;
        double fixedDeltaSeconds = SimulationSettings.FixedDeltaSeconds;
        ulong ticksRun = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            long elapsedTicks = stopwatch.ElapsedTicks;
            accumulatedSeconds += (elapsedTicks - previousElapsedTicks) / (double)Stopwatch.Frequency;
            previousElapsedTicks = elapsedTicks;

            int catchUpTicks = 0;
            while (accumulatedSeconds >= fixedDeltaSeconds && catchUpTicks < MaxCatchUpTicksPerCycle)
            {
                simulation.Step();
                ticksRun++;
                catchUpTicks++;
                accumulatedSeconds -= fixedDeltaSeconds;
            }

            if (catchUpTicks == MaxCatchUpTicksPerCycle)
                accumulatedSeconds = Math.Min(accumulatedSeconds, fixedDeltaSeconds);

            if (catchUpTicks == 0)
            {
                int delayMilliseconds = Math.Max(1, (int)((fixedDeltaSeconds - accumulatedSeconds) * 1000.0d));

                try
                {
                    await Task.Delay(delayMilliseconds, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            else
            {
                await Task.Yield();
            }
        }

        return new ServerSimulationRunResult(ticksRun);
    }
}

public sealed record ServerSimulationRunResult(ulong TicksRun);
