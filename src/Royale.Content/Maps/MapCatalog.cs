using System.Text.Json;

namespace Royale.Content.Maps;

public static class MapCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string DefaultMapId => ContentCatalog.DefaultMapId;

    public static GameMap LoadDefault() => LoadById(DefaultMapId);

    public static GameMap LoadById(string mapId) => LoadById(mapId, AppContext.BaseDirectory);

    public static GameMap LoadById(string mapId, string contentRoot)
    {
        ValidateMapId(mapId);

        string path = GetMapPath(mapId, contentRoot);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Map '{mapId}' was not found at '{path}'.", path);

        try
        {
            using FileStream stream = File.OpenRead(path);
            GameMap? map = JsonSerializer.Deserialize<GameMap>(stream, JsonOptions);

            if (map is null)
                throw new InvalidDataException($"Map '{mapId}' at '{path}' did not contain a JSON object.");

            ValidateMap(mapId, path, map);
            return map;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Map '{mapId}' at '{path}' is not valid JSON map content.", ex);
        }
    }

    public static string GetMapPath(string mapId, string contentRoot)
    {
        ValidateMapId(mapId);

        if (string.IsNullOrWhiteSpace(contentRoot))
            throw new ArgumentException("Content root must be non-empty.", nameof(contentRoot));

        return Path.Combine(contentRoot, ContentCatalog.MapDirectoryName, mapId + ".json");
    }

    private static void ValidateMapId(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            throw new ArgumentException("Map id must be non-empty.", nameof(mapId));

        foreach (char character in mapId)
        {
            bool valid =
                character is >= 'a' and <= 'z' ||
                character is >= 'A' and <= 'Z' ||
                character is >= '0' and <= '9' ||
                character is '-' or '_';

            if (!valid)
                throw new ArgumentException($"Map id '{mapId}' must contain only ASCII letters, digits, '-' or '_'.", nameof(mapId));
        }
    }

    private static void ValidateMap(string requestedMapId, string path, GameMap map)
    {
        if (string.IsNullOrWhiteSpace(map.Id))
            throw InvalidMap(path, "id is required.");

        if (!string.Equals(map.Id, requestedMapId, StringComparison.Ordinal))
            throw InvalidMap(path, $"id '{map.Id}' does not match requested map id '{requestedMapId}'.");

        if (string.IsNullOrWhiteSpace(map.Name))
            throw InvalidMap(path, "name is required.");

        if (map.StaticBoxes.Count == 0)
            throw InvalidMap(path, "at least one static box is required.");

        var staticContentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (StaticBoxDefinition box in map.StaticBoxes)
        {
            if (string.IsNullOrWhiteSpace(box.Id))
                throw InvalidMap(path, "static box id is required.");
            if (!staticContentIds.Add(box.Id))
                throw InvalidMap(path, $"static content id '{box.Id}' must be unique.");

            if (box.Size.X <= 0.0f || box.Size.Y <= 0.0f || box.Size.Z <= 0.0f)
                throw InvalidMap(path, $"static box '{box.Id}' size components must be positive.");
        }

        foreach (StaticModelDefinition model in map.StaticModels)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
                throw InvalidMap(path, "static model id is required.");
            if (!staticContentIds.Add(model.Id))
                throw InvalidMap(path, $"static content id '{model.Id}' must be unique.");
            if (string.IsNullOrWhiteSpace(model.AssetId))
                throw InvalidMap(path, $"static model '{model.Id}' assetId is required.");
            if (!IsFinite(model.Position) || !IsFinite(model.RotationEuler) || !IsFinite(model.Scale))
                throw InvalidMap(path, $"static model '{model.Id}' transform components must be finite.");
            if (model.Scale.X == 0.0f || model.Scale.Y == 0.0f || model.Scale.Z == 0.0f)
                throw InvalidMap(path, $"static model '{model.Id}' scale components must be non-zero.");
            if (!Contains(map.WorldBounds, model.Position))
                throw InvalidMap(path, $"static model '{model.Id}' position must be inside worldBounds.");
        }

        if (map.SpawnPoints.Count == 0)
            throw InvalidMap(path, "at least one spawn point is required.");

        HashSet<string> spawnPointIds = new(StringComparer.Ordinal);
        foreach (MapSpawnPoint spawnPoint in map.SpawnPoints)
        {
            if (string.IsNullOrWhiteSpace(spawnPoint.Id))
                throw InvalidMap(path, "spawn point id is required.");

            if (!spawnPointIds.Add(spawnPoint.Id))
                throw InvalidMap(path, $"spawn point id '{spawnPoint.Id}' must be unique.");
        }

        if (map.WorldBounds.Min.X >= map.WorldBounds.Max.X ||
            map.WorldBounds.Min.Y >= map.WorldBounds.Max.Y ||
            map.WorldBounds.Min.Z >= map.WorldBounds.Max.Z)
        {
            throw InvalidMap(path, "worldBounds min must be less than max on every axis.");
        }

        foreach (MapSpawnPoint spawnPoint in map.SpawnPoints)
        {
            if (!Contains(map.WorldBounds, spawnPoint.Position))
                throw InvalidMap(path, $"spawn point '{spawnPoint.Id}' position must be inside worldBounds.");
        }

        if (map.SafeZone.Radius <= 0.0f)
            throw InvalidMap(path, "safeZone radius must be positive.");
    }

    private static bool Contains(MapBounds bounds, MapVector3 position) =>
        position.X >= bounds.Min.X &&
        position.Y >= bounds.Min.Y &&
        position.Z >= bounds.Min.Z &&
        position.X <= bounds.Max.X &&
        position.Y <= bounds.Max.Y &&
        position.Z <= bounds.Max.Z;

    private static bool IsFinite(MapVector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static InvalidDataException InvalidMap(string path, string message) =>
        new($"Map file '{path}' is invalid: {message}");
}
