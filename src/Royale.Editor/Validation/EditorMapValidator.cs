using Royale.AssetPipeline.Processing;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Editor.Documents;
using Royale.Editor.Projects;
using Royale.Rendering.Meshes;
using Royale.Simulation.World;

namespace Royale.Editor.Validation;

public static class EditorMapValidator
{
    public static EditorMapValidationResult Validate(
        EditorMapDocument document,
        EditorProjectSession? projectSession)
    {
        ArgumentNullException.ThrowIfNull(document);

        var stages = new List<EditorMapValidationStage>();
        string? temporaryRoot = null;
        string clientAssetRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        string serverAssetRoot = clientAssetRoot;

        RunStage(stages, "Map schema and bounds", () => ValidateMapAndBounds(document.Map));

        if (projectSession is not null)
        {
            RunStage(stages, "Source asset manifest", () =>
                ModelAssetManifestLoader.LoadSource(
                    projectSession.Project.Paths.AssetManifest,
                    projectSession.Project.Paths.Sources,
                    requireAssets: false));

            temporaryRoot = Path.Combine(Path.GetTempPath(), "royale-editor-validation", Guid.NewGuid().ToString("N"));
            clientAssetRoot = Path.Combine(temporaryRoot, "client");
            serverAssetRoot = Path.Combine(temporaryRoot, "server");
            RunStage(stages, "Generated client assets", () => AssetPipelineProcessor.Build(
                projectSession.Project.Paths.AssetManifest,
                projectSession.Project.Paths.Sources,
                clientAssetRoot,
                AssetPipelineAudience.Client,
                requireAssets: false));
            RunStage(stages, "Generated server assets", () => AssetPipelineProcessor.Build(
                projectSession.Project.Paths.AssetManifest,
                projectSession.Project.Paths.Sources,
                serverAssetRoot,
                AssetPipelineAudience.Server,
                requireAssets: false));
        }
        else
        {
            RunStage(stages, "Packaged asset manifest", () =>
                ModelAssetManifestLoader.LoadGenerated(
                    Path.Combine(clientAssetRoot, ContentCatalog.ModelAssetManifestFileName)));
        }

        RunStage(stages, "Referenced render assets", () => ValidateRenderAssets(document.Map, clientAssetRoot));

        MapStaticCollisionWorld? collisionWorld = null;
        RunStage(stages, "Server collision world", () =>
        {
            collisionWorld = MapStaticCollisionWorld.Create(document.Map, new DirectoryInfo(serverAssetRoot));
        });

        if (collisionWorld is not null)
        {
            using (collisionWorld)
            {
                RunStage(stages, "Physical navigation graph", () =>
                    MapNavigationGraph.Create(document.Map, collisionWorld));
                RunStage(stages, "Runtime spawn points", () =>
                    ValidateSpawns(document.Map, collisionWorld));
            }
        }
        else
        {
            stages.Add(new EditorMapValidationStage(
                "Physical navigation graph",
                false,
                "Skipped because the server collision world could not be created."));
            stages.Add(new EditorMapValidationStage(
                "Runtime spawn points",
                false,
                "Skipped because the server collision world could not be created."));
        }

        var report = new EditorMapValidationReport(
            document.Revision,
            projectSession?.AssetManifestFingerprint,
            stages);
        return new EditorMapValidationResult(
            report,
            clientAssetRoot,
            serverAssetRoot,
            temporaryRoot);
    }

    private static void RunStage(List<EditorMapValidationStage> stages, string category, Action action)
    {
        try
        {
            action();
            stages.Add(new EditorMapValidationStage(category, true, "Successful."));
        }
        catch (Exception exception)
        {
            stages.Add(new EditorMapValidationStage(category, false, exception.Message));
        }
    }

    private static void ValidateMapAndBounds(GameMap map)
    {
        MapCatalog.Validate(map);
        ValidateFinite(map.SafeZone.Center, "safe-zone center");
        if (!Contains(map.WorldBounds, map.SafeZone.Center))
            throw new InvalidDataException("Safe-zone center must be inside worldBounds.");

        foreach (StaticBoxDefinition box in map.StaticBoxes)
        {
            ValidateFinite(box.Position, $"static box '{box.Id}' position");
            ValidateFinite(box.Size, $"static box '{box.Id}' size");
            ValidateFinite(box.RotationEuler, $"static box '{box.Id}' rotation");
            if (!Contains(map.WorldBounds, box.Position))
                throw new InvalidDataException($"Static box '{box.Id}' position must be inside worldBounds.");
        }
    }

    private static void ValidateRenderAssets(GameMap map, string clientAssetRoot)
    {
        StaticMeshAssetCache cache = StaticMeshAssetCache.LoadAssetRoot(clientAssetRoot);
        foreach (string assetId in map.StaticModels
            .Select(model => model.AssetId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            _ = cache.GetRequired(assetId);
        }
    }

    private static void ValidateSpawns(GameMap map, MapStaticCollisionWorld collisionWorld)
    {
        var reservations = new List<(MapSpawnPoint Spawn, SpawnReservation Reservation)>();
        foreach (MapSpawnPoint spawn in map.SpawnPoints)
        {
            if (!MapSpawnSelector.TrySelectSpawn(map, [spawn], collisionWorld, [], out _))
                throw new InvalidDataException($"Spawn point '{spawn.Id}' is obstructed and cannot be used at runtime.");

            float x = spawn.Position.X - map.SafeZone.Center.X;
            float z = spawn.Position.Z - map.SafeZone.Center.Z;
            float maximumDistance = map.SafeZone.Radius - SpawnSelectionSettings.Default.PlayerRadius;
            if (maximumDistance < 0.0f || (x * x) + (z * z) > maximumDistance * maximumDistance)
                throw new InvalidDataException($"Spawn point '{spawn.Id}' is outside the initial safe zone.");

            SpawnReservation reservation = MapSpawnSelector.CreateReservation(spawn);
            foreach ((MapSpawnPoint existingSpawn, SpawnReservation existingReservation) in reservations)
            {
                if (existingReservation.Overlaps(reservation))
                {
                    throw new InvalidDataException(
                        $"Spawn points '{existingSpawn.Id}' and '{spawn.Id}' overlap at runtime player size.");
                }
            }

            reservations.Add((spawn, reservation));
        }
    }

    private static void ValidateFinite(MapVector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException($"{name} components must be finite.");
    }

    private static bool Contains(MapBounds bounds, MapVector3 point) =>
        point.X >= bounds.Min.X && point.X <= bounds.Max.X &&
        point.Y >= bounds.Min.Y && point.Y <= bounds.Max.Y &&
        point.Z >= bounds.Min.Z && point.Z <= bounds.Max.Z;
}
