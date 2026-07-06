using System.Numerics;
using Royale.Content;

namespace Royale.Client.Rendering.Meshes;

public static class MapStaticMeshScene
{
    private const string CrateSmokeBatchDebugName = "kenney-prototype-kit/crate";
    private const string CrateSmokeInstanceDebugName = "crate-smoke";

    public static IReadOnlyList<StaticMeshInstance> CreateInstances(GameMap map) =>
        map.StaticBoxes
            .Select(staticBox => new StaticMeshInstance(CreateTransform(staticBox), staticBox.Id))
            .ToArray();

    public static StaticMeshScene CreateScene(GameMap map, StaticMeshGeometry crateSmokeGeometry)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(crateSmokeGeometry);

        return new StaticMeshScene(
            CreateInstances(map),
            [
                new StaticMeshRenderBatch(
                    CrateSmokeBatchDebugName,
                    crateSmokeGeometry,
                    [CreateCrateSmokeInstance()]),
            ]);
    }

    public static Matrix4x4 CreateTransform(StaticBoxDefinition staticBox) =>
        MapStaticBoxTransforms.CreateWorldMatrix(staticBox);

    public static StaticMeshInstance CreateCrateSmokeInstance() =>
        new(
            Matrix4x4.CreateScale(1.25f) *
            Matrix4x4.CreateFromYawPitchRoll(MathF.PI / 7.0f, 0.0f, 0.0f) *
            Matrix4x4.CreateTranslation(1.75f, 0.0f, -1.35f),
            CrateSmokeInstanceDebugName);
}
