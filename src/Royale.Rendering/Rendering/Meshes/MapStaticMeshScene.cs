using System.Numerics;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;

namespace Royale.Rendering.Meshes;

public static class MapStaticMeshScene
{
    public static IReadOnlyList<StaticMeshInstance> CreateInstances(GameMap map) =>
        map.StaticBoxes
            .Select(staticBox => new StaticMeshInstance(CreateTransform(staticBox), staticBox.Id))
            .ToArray();

    public static StaticMeshScene CreateScene(
        GameMap map,
        IReadOnlyDictionary<string, StaticMeshAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(assets);

        var modelBatches = new List<StaticMeshRenderBatch>();
        foreach (IGrouping<string, StaticModelDefinition> assetInstances in
            map.StaticModels.GroupBy(model => model.AssetId, StringComparer.Ordinal))
        {
            if (!assets.TryGetValue(assetInstances.Key, out StaticMeshAsset? asset))
                throw new KeyNotFoundException($"Map '{map.Id}' references unloaded model asset '{assetInstances.Key}'.");

            StaticMeshInstance[] instances = assetInstances
                .Select(model => new StaticMeshInstance(CreateTransform(model), model.Id))
                .ToArray();
            foreach (StaticMeshPrimitive primitive in asset.Primitives)
            {
                modelBatches.Add(new StaticMeshRenderBatch(
                    $"{asset.Id}/{primitive.DebugName}",
                    primitive.Geometry,
                    instances,
                    primitive.Material));
            }
        }

        return new StaticMeshScene(
            CreateInstances(map),
            modelBatches);
    }

    public static Matrix4x4 CreateTransform(StaticBoxDefinition staticBox) =>
        MapStaticBoxTransforms.CreateWorldMatrix(staticBox);

    public static Matrix4x4 CreateTransform(StaticModelDefinition staticModel) =>
        MapStaticModelTransforms.CreateWorldMatrix(staticModel);
}
