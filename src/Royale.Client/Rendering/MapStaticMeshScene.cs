using System.Numerics;
using Royale.Content;

namespace Royale.Client.Rendering;

public static class MapStaticMeshScene
{
    public static IReadOnlyList<StaticMeshInstance> CreateInstances(GameMap map) =>
        map.StaticBoxes
            .Select(staticBox => new StaticMeshInstance(CreateTransform(staticBox), staticBox.Id))
            .ToArray();

    public static Matrix4x4 CreateTransform(StaticBoxDefinition staticBox) =>
        MapStaticBoxTransforms.CreateWorldMatrix(staticBox);
}
