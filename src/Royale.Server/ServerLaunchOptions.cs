using System.Globalization;
using Royale.Content;
using Royale.Protocol;

namespace Royale.Server;

public sealed record ServerLaunchOptions(
    int Port,
    string MapId,
    int? RunTicks,
    int MinimumPlayers,
    int TargetPlayers,
    int WaitingSeconds,
    int PreparationSeconds)
{
    public static ServerLaunchOptions Default { get; } = new(
        ProtocolConstants.DefaultPort,
        ContentCatalog.DefaultMapId,
        RunTicks: null,
        MatchStartSettings.DefaultMinimumPlayers,
        MatchStartSettings.DefaultTargetPlayers,
        MatchStartSettings.DefaultWaitingSeconds,
        MatchStartSettings.DefaultPreparationSeconds);

    public static ServerLaunchOptions Parse(IReadOnlyList<string> args)
    {
        int port = ProtocolConstants.DefaultPort;
        string mapId = ContentCatalog.DefaultMapId;
        int? runTicks = null;
        int minimumPlayers = MatchStartSettings.DefaultMinimumPlayers;
        int targetPlayers = MatchStartSettings.DefaultTargetPlayers;
        int waitingSeconds = MatchStartSettings.DefaultWaitingSeconds;
        int preparationSeconds = MatchStartSettings.DefaultPreparationSeconds;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--port":
                    port = ParsePort(ReadRequiredValue(args, ref index, "--port"));
                    break;

                case "--map":
                    mapId = ReadRequiredValue(args, ref index, "--map");
                    break;

                case "--run-ticks":
                    runTicks = ParseRunTicks(ReadRequiredValue(args, ref index, "--run-ticks"));
                    break;

                case "--minimum-players":
                    minimumPlayers = ParseMinimumPlayers(
                        ReadRequiredValue(args, ref index, "--minimum-players"));
                    break;

                case "--target-players":
                    targetPlayers = ParsePlayerCount(
                        ReadRequiredValue(args, ref index, "--target-players"),
                        "--target-players");
                    break;

                case "--waiting-seconds":
                    waitingSeconds = ParseDurationSeconds(
                        ReadRequiredValue(args, ref index, "--waiting-seconds"),
                        "--waiting-seconds");
                    break;

                case "--preparation-seconds":
                    preparationSeconds = ParseDurationSeconds(
                        ReadRequiredValue(args, ref index, "--preparation-seconds"),
                        "--preparation-seconds");
                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        _ = new MatchStartSettings(minimumPlayers, targetPlayers, waitingSeconds, preparationSeconds);
        return new ServerLaunchOptions(
            port,
            mapId,
            runTicks,
            minimumPlayers,
            targetPlayers,
            waitingSeconds,
            preparationSeconds);
    }

    private static int ParsePort(string rawPort)
    {
        if (!int.TryParse(rawPort, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
            throw new ArgumentException("--port must be an integer between 1 and 65535.");

        return port;
    }

    private static int ParseRunTicks(string rawRunTicks)
    {
        if (!int.TryParse(rawRunTicks, NumberStyles.None, CultureInfo.InvariantCulture, out int runTicks) || runTicks < 1)
            throw new ArgumentException("--run-ticks must be a positive integer.");

        return runTicks;
    }

    private static int ParseMinimumPlayers(string rawMinimumPlayers)
        => ParsePlayerCount(rawMinimumPlayers, "--minimum-players");

    private static int ParsePlayerCount(string rawPlayerCount, string optionName)
    {
        if (!int.TryParse(
                rawPlayerCount,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int playerCount) ||
            playerCount is < 1 or > ProtocolConstants.MaxSnapshotPlayers)
        {
            throw new ArgumentException(
                $"{optionName} must be an integer between 1 and {ProtocolConstants.MaxSnapshotPlayers}.");
        }

        return playerCount;
    }

    private static int ParseDurationSeconds(string rawSeconds, string optionName)
    {
        if (!int.TryParse(rawSeconds, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) ||
            seconds < 1 ||
            seconds > int.MaxValue / Royale.Simulation.World.SimulationSettings.TickRateHz)
        {
            throw new ArgumentException($"{optionName} must be a positive number of seconds within the simulation tick range.");
        }

        return seconds;
    }

    private static string ReadRequiredValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{optionName} requires a value.");

        index++;
        string value = args[index];

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{optionName} requires a non-empty value.");

        return value;
    }
}
