using System.Numerics;

namespace Royale.Content;

public static class MapStaticBoxTransforms
{
    public static Matrix4x4 CreateWorldMatrix(StaticBoxDefinition staticBox) =>
        Matrix4x4.CreateScale(ToVector3(staticBox.Size)) *
        Matrix4x4.CreateFromQuaternion(CreateRotation(staticBox)) *
        Matrix4x4.CreateTranslation(ToVector3(staticBox.Position));

    public static Quaternion CreateRotation(StaticBoxDefinition staticBox) =>
        Quaternion.CreateFromYawPitchRoll(
            DegreesToRadians(staticBox.RotationEuler.Y),
            DegreesToRadians(staticBox.RotationEuler.X),
            DegreesToRadians(staticBox.RotationEuler.Z));

    public static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
