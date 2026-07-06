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
        Matrix4x4.CreateScale(ToVector3(staticBox.Size)) *
        Matrix4x4.CreateFromYawPitchRoll(
            DegreesToRadians(staticBox.RotationEuler.Y),
            DegreesToRadians(staticBox.RotationEuler.X),
            DegreesToRadians(staticBox.RotationEuler.Z)) *
        Matrix4x4.CreateTranslation(ToVector3(staticBox.Position));

    private static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
