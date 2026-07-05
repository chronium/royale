using System.Globalization;
using Royale.Content;
using Royale.Protocol;

namespace Royale.Server;

public sealed record ServerLaunchOptions(int Port, string MapId)
{
    public static ServerLaunchOptions Default { get; } = new(
        ProtocolConstants.DefaultPort,
        ContentCatalog.DefaultMapId);

    public static ServerLaunchOptions Parse(IReadOnlyList<string> args)
    {
        int port = ProtocolConstants.DefaultPort;
        string mapId = ContentCatalog.DefaultMapId;

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

                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        return new ServerLaunchOptions(port, mapId);
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
