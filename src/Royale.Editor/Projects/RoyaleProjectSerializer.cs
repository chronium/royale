using System.Text;
using System.Text.Json;

namespace Royale.Editor.Projects;

public static class RoyaleProjectSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static byte[] SerializeManifest(RoyaleProjectManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string json = JsonSerializer.Serialize(manifest, WriteOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        return new UTF8Encoding(false).GetBytes(json + "\n");
    }

    public static string GitIgnore => RoyaleProjectLayout.GitIgnoreContent;
}
