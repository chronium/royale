using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Royale.Client.Presentation;
using Royale.Rendering;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

namespace Royale.Client.Launch;

public sealed record ClientLaunchOptions(
    ClientLaunchMode Mode,
    string? ConnectHost,
    int Port,
    string MapId,
    ClientCameraMode CameraMode,
    RenderViewMode RenderViewMode,
    bool TelemetryVisible,
    Vector3? CameraPosition,
    Vector3? CameraLookAt,
    string? ScreenshotPath,
    int ScreenshotAfterFrames)
{
    public static ClientLaunchOptions Default { get; } = new(
        ClientLaunchMode.Offline,
        null,
        ProtocolConstants.DefaultPort,
        ContentCatalog.DefaultMapId,
        ClientCameraMode.Gameplay,
        RenderViewMode.WorldAndDebug,
        true,
        null,
        null,
        null,
        0);

    public static ClientLaunchOptions Parse(IReadOnlyList<string> args)
    {
        string? configPath = FindConfigPath(args);
        ClientLaunchProfile? profile = configPath is null ? null : LoadProfile(configPath);
        bool offlineRequested = false;
        bool connectRequested = false;
        ClientLaunchMode mode = profile?.Mode is null
            ? ClientLaunchMode.Offline
            : ParseLaunchMode(profile.Mode);
        string? connectHost = profile?.ConnectHost;
        int port = profile?.Port ?? ProtocolConstants.DefaultPort;
        string mapId = profile?.MapId ?? ContentCatalog.DefaultMapId;
        ClientCameraMode cameraMode = profile?.CameraMode is null
            ? ClientCameraMode.Gameplay
            : ParseCameraMode(profile.CameraMode);
        RenderViewMode renderViewMode = RenderViewMode.WorldAndDebug;
        bool telemetryVisible = true;
        Vector3? cameraPosition = profile?.CameraPosition is null
            ? null
            : ParseVector3(profile.CameraPosition, "cameraPosition");
        Vector3? cameraLookAt = profile?.CameraLookAt is null
            ? null
            : ParseVector3(profile.CameraLookAt, "cameraLookAt");
        string? screenshotPath = profile?.ScreenshotPath;
        int screenshotAfterFrames = profile?.ScreenshotAfterFrames ?? 0;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--config":
                    index++;
                    break;

                case "--offline":
                    offlineRequested = true;
                    mode = ClientLaunchMode.Offline;
                    connectHost = null;
                    break;

                case "--connect":
                    connectRequested = true;
                    mode = ClientLaunchMode.Connect;
                    connectHost = ReadRequiredValue(args, ref index, "--connect");
                    break;

                case "--port":
                    port = ParsePort(ReadRequiredValue(args, ref index, "--port"));
                    break;

                case "--map":
                    mapId = ReadRequiredValue(args, ref index, "--map");
                    break;

                case "--camera-mode":
                    cameraMode = ParseCameraMode(ReadRequiredValue(args, ref index, "--camera-mode"));
                    break;

                case "--render-view":
                    renderViewMode = ParseRenderViewMode(ReadRequiredValue(args, ref index, "--render-view"));
                    break;

                case "--hide-telemetry":
                    telemetryVisible = false;
                    break;

                case "--camera-position":
                    cameraPosition = ParseVector3(ReadRequiredValue(args, ref index, "--camera-position"), "--camera-position");
                    break;

                case "--camera-look-at":
                    cameraLookAt = ParseVector3(ReadRequiredValue(args, ref index, "--camera-look-at"), "--camera-look-at");
                    break;

                case "--screenshot":
                    screenshotPath = ReadRequiredValue(args, ref index, "--screenshot");
                    break;

                case "--screenshot-after-frames":
                    string rawFrameCount = ReadRequiredValue(args, ref index, "--screenshot-after-frames");

                    if (!int.TryParse(rawFrameCount, NumberStyles.None, CultureInfo.InvariantCulture, out screenshotAfterFrames) || screenshotAfterFrames < 1)
                        throw new ArgumentException("--screenshot-after-frames must be a positive integer.");

                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        if (offlineRequested && connectRequested)
            throw new ArgumentException("--offline cannot be combined with --connect.");

        if (mode == ClientLaunchMode.Connect && string.IsNullOrWhiteSpace(connectHost))
            throw new ArgumentException("Connect mode requires connectHost or --connect <host>.");

        if (mode == ClientLaunchMode.Offline && connectHost is not null)
            throw new ArgumentException("Offline mode forbids connectHost; use --offline to clear a configured host.");

        if (port is < 1 or > 65535)
            throw new ArgumentException("port must be an integer between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(mapId))
            throw new ArgumentException("mapId must be a non-empty string.");

        if (screenshotPath is not null && string.IsNullOrWhiteSpace(screenshotPath))
            throw new ArgumentException("screenshotPath must be a non-empty string or null.");

        if (screenshotAfterFrames < 0)
            throw new ArgumentException("screenshotAfterFrames must be a positive integer or null.");

        if (cameraMode != ClientCameraMode.Freecam && (cameraPosition is not null || cameraLookAt is not null))
            throw new ArgumentException("--camera-position and --camera-look-at require --camera-mode freecam.");

        if (cameraPosition is Vector3 position && cameraLookAt is Vector3 lookAt && position == lookAt)
            throw new ArgumentException("--camera-look-at must differ from --camera-position.");

        if (screenshotPath is not null && screenshotAfterFrames == 0)
            screenshotAfterFrames = 1;

        if (screenshotPath is null && screenshotAfterFrames != 0)
            throw new ArgumentException("--screenshot-after-frames requires --screenshot.");

        return new ClientLaunchOptions(
            mode,
            connectHost,
            port,
            mapId,
            cameraMode,
            renderViewMode,
            telemetryVisible,
            cameraPosition,
            cameraLookAt,
            screenshotPath,
            screenshotAfterFrames);
    }

    private static RenderViewMode ParseRenderViewMode(string value) => value switch
    {
        "normal" => RenderViewMode.Normal,
        "world-and-debug" => RenderViewMode.WorldAndDebug,
        "debug-only" => RenderViewMode.DebugOnly,
        "collision-solids" => RenderViewMode.CollisionSolids,
        _ => throw new ArgumentException("--render-view must be normal, world-and-debug, debug-only, or collision-solids."),
    };

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

    private static ClientLaunchProfile LoadProfile(string configPath)
    {
        string fullPath = Path.GetFullPath(configPath, Environment.CurrentDirectory);

        if (!File.Exists(fullPath))
            throw new ArgumentException($"Configuration file '{fullPath}' does not exist.");

        try
        {
            string json = File.ReadAllText(fullPath);
            RejectNullProperties(json, fullPath, ["mode", "port", "mapId", "cameraMode"]);
            return JsonSerializer.Deserialize<ClientLaunchProfile>(json, JsonOptions)
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

    private static ClientLaunchMode ParseLaunchMode(string rawMode) =>
        rawMode switch
        {
            "offline" => ClientLaunchMode.Offline,
            "connect" => ClientLaunchMode.Connect,
            _ => throw new ArgumentException("mode must be 'offline' or 'connect'."),
        };

    private static ClientCameraMode ParseCameraMode(string rawCameraMode) =>
        rawCameraMode switch
        {
            "gameplay" => ClientCameraMode.Gameplay,
            "freecam" => ClientCameraMode.Freecam,
            _ => throw new ArgumentException("--camera-mode must be 'gameplay' or 'freecam'."),
        };

    private static Vector3 ParseVector3(string rawVector, string optionName)
    {
        string[] parts = rawVector.Split(',', StringSplitOptions.None);

        if (parts.Length != 3)
            throw new ArgumentException($"{optionName} must contain exactly three comma-separated finite floats.");

        float x = ParseVectorComponent(parts[0], optionName);
        float y = ParseVectorComponent(parts[1], optionName);
        float z = ParseVectorComponent(parts[2], optionName);
        return new Vector3(x, y, z);
    }

    private static float ParseVectorComponent(string rawValue, string optionName)
    {
        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
            throw new ArgumentException($"{optionName} must contain exactly three comma-separated finite floats.");

        return value;
    }

    private static int ParsePort(string rawPort)
    {
        if (!int.TryParse(rawPort, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
            throw new ArgumentException("--port must be an integer between 1 and 65535.");

        return port;
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

    private sealed class ClientLaunchProfile
    {
        public string? Mode { get; init; }

        public string? ConnectHost { get; init; }

        public int? Port { get; init; }

        public string? MapId { get; init; }

        public string? CameraMode { get; init; }

        public string? CameraPosition { get; init; }

        public string? CameraLookAt { get; init; }

        public string? ScreenshotPath { get; init; }

        public int? ScreenshotAfterFrames { get; init; }
    }
}
