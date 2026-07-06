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
    public void GrayboxSpawnPointIdsArePresentAndUnique()
    {
        GameMap map = MapCatalog.LoadDefault();
        string[] ids = map.SpawnPoints.Select(spawnPoint => spawnPoint.Id).ToArray();

        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GrayboxSpawnPositionsAreInsideWorldBounds()
    {
        GameMap map = MapCatalog.LoadDefault();

        Assert.All(map.SpawnPoints, spawnPoint =>
        {
            Assert.True(spawnPoint.Position.X >= map.WorldBounds.Min.X && spawnPoint.Position.X <= map.WorldBounds.Max.X);
            Assert.True(spawnPoint.Position.Y >= map.WorldBounds.Min.Y && spawnPoint.Position.Y <= map.WorldBounds.Max.Y);
            Assert.True(spawnPoint.Position.Z >= map.WorldBounds.Min.Z && spawnPoint.Position.Z <= map.WorldBounds.Max.Z);
        });
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
    public void GrayboxMapUsesExpandedHorizontalFootprint()
    {
        GameMap map = MapCatalog.LoadDefault();
        StaticBoxDefinition ground = Assert.Single(map.StaticBoxes, staticBox => staticBox.Id == "ground-main");
        StaticBoxDefinition northWall = Assert.Single(map.StaticBoxes, staticBox => staticBox.Id == "boundary-north-wall");
        StaticBoxDefinition eastWall = Assert.Single(map.StaticBoxes, staticBox => staticBox.Id == "boundary-east-wall");

        Assert.Equal(-24.0f, map.WorldBounds.Min.X);
        Assert.Equal(-24.0f, map.WorldBounds.Min.Z);
        Assert.Equal(24.0f, map.WorldBounds.Max.X);
        Assert.Equal(24.0f, map.WorldBounds.Max.Z);
        Assert.Equal(20.0f, map.SafeZone.Radius);

        Assert.Equal(20.0f, ground.Size.X);
        Assert.Equal(20.0f, ground.Size.Z);
        Assert.Equal(-9.9f, northWall.Position.Z);
        Assert.Equal(20.0f, northWall.Size.X);
        Assert.Equal(9.9f, eastWall.Position.X);
        Assert.Equal(20.0f, eastWall.Size.Z);
    }

    [Fact]
    public void GrayboxRampClusterKeepsInternalSpacingAfterExpansion()
    {
        GameMap map = MapCatalog.LoadDefault();
        StaticBoxDefinition step = Assert.Single(map.StaticBoxes, staticBox => staticBox.Id == "step-low");
        StaticBoxDefinition ramp = Assert.Single(map.StaticBoxes, staticBox => staticBox.Id == "ramp-platform-approach");
        StaticBoxDefinition platform = Assert.Single(map.StaticBoxes, staticBox => staticBox.Id == "platform-high");

        Assert.Equal(-6.1f, ramp.Position.X);
        Assert.Equal(ramp.Position.X, step.Position.X);
        Assert.Equal(ramp.Position.X, platform.Position.X);
        Assert.Equal(-0.7f, step.Position.Z - ramp.Position.Z, precision: 5);
        Assert.Equal(0.9f, platform.Position.Z - ramp.Position.Z, precision: 5);
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

    [Fact]
    public void MapWithoutSpawnPointsFailsWithClearMessage()
    {
        InvalidDataException exception = LoadInvalidMap("missing-spawns", """
            {
              "id": "missing-spawns",
              "name": "Missing Spawns",
              "worldBounds": { "min": { "x": -1, "y": -1, "z": -1 }, "max": { "x": 1, "y": 2, "z": 1 } },
              "safeZone": { "center": { "x": 0, "y": 0, "z": 0 }, "radius": 1 },
              "spawnPoints": [],
              "lootPoints": [],
              "staticBoxes": [
                {
                  "id": "ground",
                  "position": { "x": 0, "y": -0.1, "z": 0 },
                  "size": { "x": 2, "y": 0.2, "z": 2 },
                  "rotationEuler": { "x": 0, "y": 0, "z": 0 }
                }
              ]
            }
            """);

        Assert.Contains("at least one spawn point is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapWithEmptySpawnPointIdFailsWithClearMessage()
    {
        InvalidDataException exception = LoadInvalidMap("empty-spawn-id", CreateMapJson("empty-spawn-id", """
            [
                { "id": "", "position": { "x": 0, "y": 0, "z": 0 }, "rotationEuler": { "x": 0, "y": 0, "z": 0 } }
            ]
            """));

        Assert.Contains("spawn point id is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapWithDuplicateSpawnPointIdsFailsWithClearMessage()
    {
        InvalidDataException exception = LoadInvalidMap("duplicate-spawn-id", CreateMapJson("duplicate-spawn-id", """
            [
                { "id": "spawn-a", "position": { "x": 0, "y": 0, "z": 0 }, "rotationEuler": { "x": 0, "y": 0, "z": 0 } },
                { "id": "spawn-a", "position": { "x": 0.5, "y": 0, "z": 0 }, "rotationEuler": { "x": 0, "y": 0, "z": 0 } }
            ]
            """));

        Assert.Contains("spawn point id 'spawn-a' must be unique", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapWithSpawnPointOutsideWorldBoundsFailsWithClearMessage()
    {
        InvalidDataException exception = LoadInvalidMap("spawn-outside-bounds", CreateMapJson("spawn-outside-bounds", """
            [
                { "id": "spawn-a", "position": { "x": 2, "y": 0, "z": 0 }, "rotationEuler": { "x": 0, "y": 0, "z": 0 } }
            ]
            """));

        Assert.Contains("spawn point 'spawn-a' position must be inside worldBounds", exception.Message, StringComparison.Ordinal);
    }

    private static InvalidDataException LoadInvalidMap(string mapId, string json)
    {
        string contentRoot = Path.Combine(Path.GetTempPath(), "royale-map-content-tests", Guid.NewGuid().ToString("N"));
        string mapDirectory = Path.Combine(contentRoot, ContentCatalog.MapDirectoryName);
        Directory.CreateDirectory(mapDirectory);
        File.WriteAllText(Path.Combine(mapDirectory, mapId + ".json"), json);

        try
        {
            return Assert.Throws<InvalidDataException>(() => MapCatalog.LoadById(mapId, contentRoot));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static string CreateMapJson(string mapId, string spawnPointsJson) =>
        $$"""
          {
            "id": "{{mapId}}",
            "name": "Invalid Spawn Test",
            "worldBounds": { "min": { "x": -1, "y": -1, "z": -1 }, "max": { "x": 1, "y": 2, "z": 1 } },
            "safeZone": { "center": { "x": 0, "y": 0, "z": 0 }, "radius": 1 },
            "spawnPoints": {{spawnPointsJson}},
            "lootPoints": [],
            "staticBoxes": [
              {
                "id": "ground",
                "position": { "x": 0, "y": -0.1, "z": 0 },
                "size": { "x": 2, "y": 0.2, "z": 2 },
                "rotationEuler": { "x": 0, "y": 0, "z": 0 }
              }
            ]
          }
          """;
}
