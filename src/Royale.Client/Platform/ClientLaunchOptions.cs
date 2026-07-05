using System.Globalization;
using Royale.Content;
using Royale.Protocol;

namespace Royale.Client.Platform;

public sealed record ClientLaunchOptions(
    ClientLaunchMode Mode,
    string? ConnectHost,
    int Port,
    string MapId,
    string? ScreenshotPath,
    int ScreenshotAfterFrames)
{
    public static ClientLaunchOptions Default { get; } = new(
        ClientLaunchMode.Offline,
        null,
        ProtocolConstants.DefaultPort,
        ContentCatalog.DefaultMapId,
        null,
        0);

    public static ClientLaunchOptions Parse(IReadOnlyList<string> args)
    {
        bool offlineRequested = false;
        string? connectHost = null;
        int port = ProtocolConstants.DefaultPort;
        string mapId = ContentCatalog.DefaultMapId;
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

        if (screenshotPath is not null && screenshotAfterFrames == 0)
            screenshotAfterFrames = 1;

        if (screenshotPath is null && screenshotAfterFrames != 0)
            throw new ArgumentException("--screenshot-after-frames requires --screenshot.");

        return new ClientLaunchOptions(
            connectHost is null ? ClientLaunchMode.Offline : ClientLaunchMode.Connect,
            connectHost,
            port,
            mapId,
            screenshotPath,
            screenshotAfterFrames);
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
