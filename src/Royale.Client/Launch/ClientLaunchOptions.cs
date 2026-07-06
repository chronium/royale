using System.Globalization;
using System.Numerics;
using Royale.Client.Presentation;
using Royale.Content;
using Royale.Protocol;

namespace Royale.Client.Launch;

public sealed record ClientLaunchOptions(
    ClientLaunchMode Mode,
    string? ConnectHost,
    int Port,
    string MapId,
    ClientCameraMode CameraMode,
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
        null,
        null,
        null,
        0);

    public static ClientLaunchOptions Parse(IReadOnlyList<string> args)
    {
        bool offlineRequested = false;
        string? connectHost = null;
        int port = ProtocolConstants.DefaultPort;
        string mapId = ContentCatalog.DefaultMapId;
        ClientCameraMode cameraMode = ClientCameraMode.Gameplay;
        Vector3? cameraPosition = null;
        Vector3? cameraLookAt = null;
        string? screenshotPath = null;
        int screenshotAfterFrames = 0;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--offline":
                    offlineRequested = true;
                    break;

                case "--connect":
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

        if (offlineRequested && connectHost is not null)
            throw new ArgumentException("--offline cannot be combined with --connect.");

        if (cameraMode != ClientCameraMode.Freecam && (cameraPosition is not null || cameraLookAt is not null))
            throw new ArgumentException("--camera-position and --camera-look-at require --camera-mode freecam.");

        if (cameraPosition is Vector3 position && cameraLookAt is Vector3 lookAt && position == lookAt)
            throw new ArgumentException("--camera-look-at must differ from --camera-position.");

        if (screenshotPath is not null && screenshotAfterFrames == 0)
            screenshotAfterFrames = 1;

        if (screenshotPath is null && screenshotAfterFrames != 0)
            throw new ArgumentException("--screenshot-after-frames requires --screenshot.");

        return new ClientLaunchOptions(
            connectHost is null ? ClientLaunchMode.Offline : ClientLaunchMode.Connect,
            connectHost,
            port,
            mapId,
            cameraMode,
            cameraPosition,
            cameraLookAt,
            screenshotPath,
            screenshotAfterFrames);
    }

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
}
