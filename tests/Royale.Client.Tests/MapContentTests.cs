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
        Assert.NotEmpty(map.StaticModels);
        Assert.NotEmpty(map.SpawnPoints);
        Assert.NotEmpty(map.LootPoints);
        Assert.True(map.WorldBounds.Min.X < map.WorldBounds.Max.X);
        Assert.True(map.WorldBounds.Min.Y < map.WorldBounds.Max.Y);
        Assert.True(map.WorldBounds.Min.Z < map.WorldBounds.Max.Z);
        Assert.True(map.SafeZone.Radius > 0.0f);
    }

    [Fact]
    public void GrayboxDeclaresTheKenneyCrateAsMapOwnedContent()
    {
        StaticModelDefinition crate = Assert.Single(MapCatalog.LoadDefault().StaticModels);

        Assert.Equal("crate-south-east", crate.Id);
        Assert.Equal("kenney-crate", crate.AssetId);
        Assert.Equal(new MapVector3(6.0f, 0.0f, 5.0f), crate.Position);
        Assert.Equal(new MapVector3(1.25f, 1.25f, 1.25f), crate.Scale);
    }

    [Fact]
    public void GrayboxSpawnAndLootPointsHaveExpectedUniqueCounts()
    {
        GameMap map = MapCatalog.LoadDefault();
        string[] spawnIds = map.SpawnPoints.Select(spawnPoint => spawnPoint.Id).ToArray();
        string[] lootIds = map.LootPoints.Select(lootPoint => lootPoint.Id).ToArray();

        Assert.Equal(12, spawnIds.Length);
        Assert.All(spawnIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(spawnIds.Length, spawnIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(12, map.SpawnPoints.Select(spawnPoint => spawnPoint.Position).Distinct().Count());

        Assert.Equal(8, lootIds.Length);
        Assert.All(lootIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(lootIds.Length, lootIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(8, map.LootPoints.Select(lootPoint => lootPoint.Position).Distinct().Count());
    }

    [Fact]
    public void GrayboxSpawnPositionsAreInsideWorldBoundsAndInitialSafeZone()
    {
        GameMap map = MapCatalog.LoadDefault();

        Assert.All(map.SpawnPoints, spawnPoint =>
        {
            Assert.True(spawnPoint.Position.X >= map.WorldBounds.Min.X && spawnPoint.Position.X <= map.WorldBounds.Max.X);
            Assert.True(spawnPoint.Position.Y >= map.WorldBounds.Min.Y && spawnPoint.Position.Y <= map.WorldBounds.Max.Y);
            Assert.True(spawnPoint.Position.Z >= map.WorldBounds.Min.Z && spawnPoint.Position.Z <= map.WorldBounds.Max.Z);
            float deltaX = spawnPoint.Position.X - map.SafeZone.Center.X;
            float deltaZ = spawnPoint.Position.Z - map.SafeZone.Center.Z;
            Assert.True((deltaX * deltaX) + (deltaZ * deltaZ) <= map.SafeZone.Radius * map.SafeZone.Radius);
        });
    }

    [Fact]
    public void GrayboxSpawnRotationsFaceSafeZoneCenter()
    {
        GameMap map = MapCatalog.LoadDefault();

        Assert.All(map.SpawnPoints, spawnPoint =>
        {
            float yawRadians = spawnPoint.RotationEuler.Y * MathF.PI / 180.0f;
            float forwardX = MathF.Sin(yawRadians);
            float forwardZ = -MathF.Cos(yawRadians);
            float centerX = map.SafeZone.Center.X - spawnPoint.Position.X;
            float centerZ = map.SafeZone.Center.Z - spawnPoint.Position.Z;
            float inverseLength = 1.0f / MathF.Sqrt((centerX * centerX) + (centerZ * centerZ));
            float alignment = (forwardX * centerX * inverseLength) + (forwardZ * centerZ * inverseLength);

            Assert.True(alignment > 0.999f, $"Spawn '{spawnPoint.Id}' does not face the safe-zone center.");
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
        Assert.Contains(ids, id => id.StartsWith("north-", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.StartsWith("east-", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.StartsWith("south-", StringComparison.Ordinal));
        Assert.Contains(ids, id => id.StartsWith("west-", StringComparison.Ordinal));
        Assert.Equal(4, ids.Count(id => id.StartsWith("center-", StringComparison.Ordinal)));
        Assert.Equal(18, map.StaticBoxes.Count(staticBox =>
            staticBox.Id != "ground-main" && !staticBox.Id.StartsWith("boundary-", StringComparison.Ordinal)));
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

        Assert.Equal(40.0f, ground.Size.X);
        Assert.Equal(40.0f, ground.Size.Z);
        Assert.Equal(-19.9f, northWall.Position.Z);
        Assert.Equal(40.0f, northWall.Size.X);
        Assert.Equal(19.9f, eastWall.Position.X);
        Assert.Equal(40.0f, eastWall.Size.Z);
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

    [Fact]
    public void StaticModelRequiresUniqueIdAssetAndNonzeroFiniteScale()
    {
        InvalidDataException duplicate = LoadInvalidMap("duplicate-static-content", """
            {
              "id": "duplicate-static-content",
              "name": "Duplicate Static Content",
              "worldBounds": { "min": { "x": -2, "y": -1, "z": -2 }, "max": { "x": 2, "y": 2, "z": 2 } },
              "safeZone": { "center": { "x": 0, "y": 0, "z": 0 }, "radius": 1 },
              "spawnPoints": [{ "id": "spawn", "position": { "x": 0, "y": 0, "z": 0 } }],
              "staticModels": [{
                "id": "ground",
                "assetId": "crate",
                "position": { "x": 0, "y": 0, "z": 0 },
                "rotationEuler": { "x": 0, "y": 0, "z": 0 },
                "scale": { "x": 1, "y": 1, "z": 1 }
              }],
              "staticBoxes": [{
                "id": "ground",
                "position": { "x": 0, "y": -0.1, "z": 0 },
                "size": { "x": 2, "y": 0.2, "z": 2 }
              }]
            }
            """);
        Assert.Contains("static content id 'ground' must be unique", duplicate.Message, StringComparison.Ordinal);

        InvalidDataException zeroScale = LoadInvalidMap("zero-model-scale", """
            {
              "id": "zero-model-scale",
              "name": "Zero Model Scale",
              "worldBounds": { "min": { "x": -2, "y": -1, "z": -2 }, "max": { "x": 2, "y": 2, "z": 2 } },
              "safeZone": { "center": { "x": 0, "y": 0, "z": 0 }, "radius": 1 },
              "spawnPoints": [{ "id": "spawn", "position": { "x": 0, "y": 0, "z": 0 } }],
              "staticModels": [{
                "id": "crate",
                "assetId": "kenney-crate",
                "position": { "x": 0, "y": 0, "z": 0 },
                "rotationEuler": { "x": 0, "y": 0, "z": 0 },
                "scale": { "x": 0, "y": 1, "z": 1 }
              }],
              "staticBoxes": [{
                "id": "ground",
                "position": { "x": 0, "y": -0.1, "z": 0 },
                "size": { "x": 2, "y": 0.2, "z": 2 }
              }]
            }
            """);
        Assert.Contains("scale components must be non-zero", zeroScale.Message, StringComparison.Ordinal);
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
