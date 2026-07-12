using System.Text;
using System.Text.Json;

namespace Royale.Content.Maps;

public static class MapFileSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static byte[] Serialize(GameMap map)
    {
        MapCatalog.Validate(map);
        string json = JsonSerializer.Serialize(map, WriteOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        return new UTF8Encoding(false).GetBytes(json + "\n");
    }

    public static void Serialize(Stream destination, GameMap map)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(Serialize(map));
    }
}
