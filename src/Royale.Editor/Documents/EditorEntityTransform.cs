using System.Numerics;
using Royale.Content.Maps;

namespace Royale.Editor.Documents;

[Flags]
public enum EditorTransformCapabilities
{
    None = 0,
    Translate = 1,
    Rotate = 2,
    Scale = 4,
}

public readonly record struct EditorEntityTransform(
    Vector3 Position,
    Vector3 RotationDegrees,
    Vector3 ScaleOrSize)
{
    public Matrix4x4 CreateMatrix() =>
        Matrix4x4.CreateScale(ScaleOrSize) *
        Matrix4x4.CreateFromYawPitchRoll(
            DegreesToRadians(RotationDegrees.Y),
            DegreesToRadians(RotationDegrees.X),
            DegreesToRadians(RotationDegrees.Z)) *
        Matrix4x4.CreateTranslation(Position);

    public bool IsFinite =>
        IsFiniteVector(Position) && IsFiniteVector(RotationDegrees) && IsFiniteVector(ScaleOrSize);

    public bool NearlyEquals(EditorEntityTransform other, float epsilon = 0.0001f) =>
        Vector3.DistanceSquared(Position, other.Position) <= epsilon * epsilon &&
        Vector3.DistanceSquared(RotationDegrees, other.RotationDegrees) <= epsilon * epsilon &&
        Vector3.DistanceSquared(ScaleOrSize, other.ScaleOrSize) <= epsilon * epsilon;

    public static EditorEntityTransform FromMatrix(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            throw new ArgumentException("The matrix cannot be decomposed into a transform.", nameof(matrix));

        rotation = Quaternion.Normalize(rotation);
        float pitch = MathF.Asin(Math.Clamp(2.0f * (rotation.W * rotation.X - rotation.Z * rotation.Y), -1.0f, 1.0f));
        float yaw = MathF.Atan2(
            2.0f * (rotation.W * rotation.Y + rotation.X * rotation.Z),
            1.0f - 2.0f * (rotation.X * rotation.X + rotation.Y * rotation.Y));
        float roll = MathF.Atan2(
            2.0f * (rotation.W * rotation.Z + rotation.X * rotation.Y),
            1.0f - 2.0f * (rotation.X * rotation.X + rotation.Z * rotation.Z));

        return new EditorEntityTransform(
            translation,
            new Vector3(RadiansToDegrees(pitch), RadiansToDegrees(yaw), RadiansToDegrees(roll)),
            scale);
    }

    internal static MapVector3 ToMap(Vector3 value) => new(value.X, value.Y, value.Z);

    private static bool IsFiniteVector(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
    private static float RadiansToDegrees(float radians) => radians * 180.0f / MathF.PI;
}
