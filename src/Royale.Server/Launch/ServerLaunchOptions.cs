using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Server.Match;

namespace Royale.Server.Launch;

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
        string? configPath = FindConfigPath(args);
        ServerLaunchProfile? profile = configPath is null ? null : LoadProfile(configPath);
        int port = profile?.Port ?? ProtocolConstants.DefaultPort;
        string mapId = profile?.MapId ?? ContentCatalog.DefaultMapId;
        int? runTicks = profile?.RunTicks;
        int minimumPlayers = profile?.MinimumPlayers ?? MatchStartSettings.DefaultMinimumPlayers;
        int targetPlayers = profile?.TargetPlayers ?? MatchStartSettings.DefaultTargetPlayers;
        int waitingSeconds = profile?.WaitingSeconds ?? MatchStartSettings.DefaultWaitingSeconds;
        int preparationSeconds = profile?.PreparationSeconds ?? MatchStartSettings.DefaultPreparationSeconds;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--config":
                    index++;
                    break;

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

        if (port is < 1 or > 65535)
            throw new ArgumentException("port must be an integer between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(mapId))
            throw new ArgumentException("mapId must be a non-empty string.");
        if (runTicks is <= 0)
            throw new ArgumentException("runTicks must be a positive integer or null.");

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

    private static string? FindConfigPath(IReadOnlyList<string> args)
    {
        string? configPath = null;

        for (int index = 0; index < args.Count; index++)
        {
            if (args[index] != "--config")
                continue;

            if (configPath is not null)
                throw new ArgumentException("--config may be supplied only once.");

            configPath = ReadRequiredValue(args, ref index, "--config");
        }

        return configPath;
    }

    private static ServerLaunchProfile LoadProfile(string configPath)
    {
        string fullPath = Path.GetFullPath(configPath, Environment.CurrentDirectory);

        if (!File.Exists(fullPath))
            throw new ArgumentException($"Configuration file '{fullPath}' does not exist.");

        try
        {
            string json = File.ReadAllText(fullPath);
            RejectNullProperties(
                json,
                fullPath,
                ["port", "mapId", "minimumPlayers", "targetPlayers", "waitingSeconds", "preparationSeconds"]);
            return JsonSerializer.Deserialize<ServerLaunchProfile>(json, JsonOptions)
                ?? throw new ArgumentException($"Configuration file '{fullPath}' must contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"Configuration file '{fullPath}' is malformed: {exception.Message}",
                exception);
        }
    }

    private static void RejectNullProperties(string json, string fullPath, IReadOnlyList<string> propertyNames)
    {
        using JsonDocument document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Configuration file '{fullPath}' must contain a JSON object.");

        foreach (string propertyName in propertyNames)
        {
            if (document.RootElement.TryGetProperty(propertyName, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Null)
            {
                throw new ArgumentException(
                    $"Configuration field '{propertyName}' in '{fullPath}' cannot be null; omit it to use the default.");
            }
        }
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

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

    private sealed class ServerLaunchProfile
    {
        public int? Port { get; init; }

        public string? MapId { get; init; }

        public int? RunTicks { get; init; }

        public int? MinimumPlayers { get; init; }

        public int? TargetPlayers { get; init; }

        public int? WaitingSeconds { get; init; }

        public int? PreparationSeconds { get; init; }
    }
}
