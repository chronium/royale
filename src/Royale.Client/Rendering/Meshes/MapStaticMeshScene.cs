using System.Numerics;
using Royale.Content;

namespace Royale.Client.Rendering.Meshes;

public static class MapStaticMeshScene
{
    private const string CrateSmokeInstanceDebugName = "crate-smoke";

    public static IReadOnlyList<StaticMeshInstance> CreateInstances(GameMap map) =>
        map.StaticBoxes
            .Select(staticBox => new StaticMeshInstance(CreateTransform(staticBox), staticBox.Id))
            .ToArray();

    public static StaticMeshScene CreateScene(GameMap map, StaticMeshAsset crateAsset)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(crateAsset);

        StaticMeshInstance previewInstance = CreateCrateSmokeInstance();
        StaticMeshRenderBatch[] modelBatches = crateAsset.Primitives
            .Select(primitive => new StaticMeshRenderBatch(
                $"{crateAsset.Id}/{primitive.DebugName}",
                primitive.Geometry,
                [previewInstance],
                primitive.Material))
            .ToArray();

        return new StaticMeshScene(
            CreateInstances(map),
            modelBatches);
    }

    public static Matrix4x4 CreateTransform(StaticBoxDefinition staticBox) =>
        MapStaticBoxTransforms.CreateWorldMatrix(staticBox);

    public static StaticMeshInstance CreateCrateSmokeInstance() =>
        new(
            Matrix4x4.CreateScale(1.25f) *
            Matrix4x4.CreateFromYawPitchRoll(MathF.PI / 7.0f, 0.0f, 0.0f) *
            Matrix4x4.CreateTranslation(6.0f, 0.0f, 5.0f),
            CrateSmokeInstanceDebugName);
}
