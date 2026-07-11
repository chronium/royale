using System.Numerics;

namespace Royale.Content.Maps;

public static class MapStaticModelTransforms
{
    public static Matrix4x4 CreateWorldMatrix(StaticModelDefinition staticModel) =>
        Matrix4x4.CreateScale(MapStaticBoxTransforms.ToVector3(staticModel.Scale)) *
        Matrix4x4.CreateFromQuaternion(CreateRotation(staticModel)) *
        Matrix4x4.CreateTranslation(MapStaticBoxTransforms.ToVector3(staticModel.Position));

    public static Quaternion CreateRotation(StaticModelDefinition staticModel) =>
        Quaternion.CreateFromYawPitchRoll(
            DegreesToRadians(staticModel.RotationEuler.Y),
            DegreesToRadians(staticModel.RotationEuler.X),
            DegreesToRadians(staticModel.RotationEuler.Z));

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
