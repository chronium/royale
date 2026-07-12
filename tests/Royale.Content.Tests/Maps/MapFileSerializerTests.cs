using System.Text;
using Royale.Content.Maps;

namespace Royale.Content.Tests.Maps;

public sealed class MapFileSerializerTests
{
    [Fact]
    public void LoadsCommentsAndTrailingCommasAndSerializesDeterministically()
    {
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Royale.Content", "Maps", "graybox.json"));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        string json = File.ReadAllText(source); json = "// comment\n" + json.TrimEnd().TrimEnd('}') + ",\n}\n"; File.WriteAllText(path, json);
        GameMap map = MapCatalog.LoadFile(path); byte[] first = MapFileSerializer.Serialize(map); byte[] second = MapFileSerializer.Serialize(map);
        Assert.Equal(first, second); string canonical = WriteTemporary(first); GameMap reloaded = MapCatalog.LoadFile(canonical); Assert.Equal(map.Id, reloaded.Id); Assert.Equal(map.Name, reloaded.Name); Assert.Equal(map.StaticBoxes.Count, reloaded.StaticBoxes.Count); File.Delete(canonical); File.Delete(path);
    }

    private static string WriteTemporary(byte[] bytes) { string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"); File.WriteAllBytes(path, bytes); return path; }
}
