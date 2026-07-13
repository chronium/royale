using Royale.Content.Maps;
using Royale.Content.Runtime;

namespace Royale.Content.Tests.Runtime;

public sealed class RuntimeContentSelectionTests
{
    [Fact]
    public void ExplicitMapFileLoadsItsOwnIdWhenMapWasNotExplicit()
    {
        string path = WriteMap("custom-map");
        try
        {
            RuntimeContentSelection selection = RuntimeContentSelection.Load(
                ContentCatalog.DefaultMapId,
                path,
                requireMapIdMatch: false,
                assetRoot: null);

            Assert.Equal("custom-map", selection.Map.Id);
            Assert.Equal(Path.Combine(AppContext.BaseDirectory, "assets"), selection.AssetRoot.FullName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExplicitMapAndMapFileRequireMatchingIds()
    {
        string path = WriteMap("custom-map");
        try
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                RuntimeContentSelection.Load(
                    "different-map",
                    path,
                    requireMapIdMatch: true,
                    assetRoot: null));
            Assert.Contains("does not match requested map id", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteMap(string id)
    {
        GameMap source = MapCatalog.LoadById(ContentCatalog.DefaultMapId);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllBytes(path, MapFileSerializer.Serialize(source with { Id = id }));
        return path;
    }
}
