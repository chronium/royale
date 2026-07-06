using Royale.Content;

namespace Royale.Client.Tests;

public sealed class MapContentTests
{
    [Fact]
    public void DefaultMapIdResolvesToGraybox()
    {
        Assert.Equal("graybox", ContentCatalog.DefaultMapId);
        Assert.Equal(ContentCatalog.DefaultMapId, MapCatalog.DefaultMapId);
    }

    [Fact]
    public void GrayboxMapIsDiscoverableThroughContentLoader()
    {
        GameMap map = MapCatalog.LoadDefault();

        Assert.Equal("graybox", map.Id);
        Assert.Equal("Gray-Box Test Arena", map.Name);
    }

    [Fact]
    public void GrayboxMapParsesSchemaPlaceholders()
    {
        GameMap map = MapCatalog.LoadById("graybox");

        Assert.NotEmpty(map.StaticBoxes);
        Assert.NotEmpty(map.SpawnPoints);
        Assert.NotEmpty(map.LootPoints);
        Assert.True(map.WorldBounds.Min.X < map.WorldBounds.Max.X);
        Assert.True(map.WorldBounds.Min.Y < map.WorldBounds.Max.Y);
        Assert.True(map.WorldBounds.Min.Z < map.WorldBounds.Max.Z);
        Assert.True(map.SafeZone.Radius > 0.0f);
    }

    [Fact]
    public void GrayboxMapContainsRequiredEnvironmentCategories()
    {
        GameMap map = MapCatalog.LoadDefault();
        string[] ids = map.StaticBoxes.Select(staticBox => staticBox.Id).ToArray();

        Assert.Contains(ids, id => id.Contains("ground", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.Contains("wall", StringComparison.Ordinal) || id.Contains("boundary", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.Contains("ramp", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.Contains("step", StringComparison.Ordinal) || id.Contains("platform", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.Contains("cover", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingMapIdFailsWithClearMessage()
    {
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => MapCatalog.LoadById("missing-map"));

        Assert.Contains("Map 'missing-map' was not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidMapIdFailsWithClearMessage()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => MapCatalog.LoadById("../graybox"));

        Assert.Contains("must contain only ASCII letters", exception.Message, StringComparison.Ordinal);
    }
}
