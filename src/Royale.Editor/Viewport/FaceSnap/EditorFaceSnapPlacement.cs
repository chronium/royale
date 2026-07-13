using System.Numerics;
using Royale.Editor.Documents;
using Royale.Simulation.World;

namespace Royale.Editor.Viewport.FaceSnap;

public static class EditorFaceSnapPlacement
{
    public static EditorEntityTransform Calculate(
        EditorEntityTransform original,
        Vector3 localMinimum,
        Vector3 localMaximum,
        MapStaticRayHit hit,
        EditorFaceSnapSettings settings,
        bool alignmentSupported,
        bool boundsFollowRotation = true)
    {
        Vector3 normal = Vector3.Normalize(hit.Normal);
        Quaternion rotation = GetRotation(original);
        if (settings.AlignmentEnabled && alignmentSupported)
        {
            Vector3 currentAxis = Vector3.Transform(settings.GetLocalAxis(), rotation);
            Quaternion delta = RotationBetween(currentAxis, normal);
            rotation = Quaternion.Normalize(Quaternion.Concatenate(rotation, delta));
        }

        float minimumProjection = CalculateMinimumProjection(
            original.ScaleOrSize,
            rotation,
            localMinimum,
            localMaximum,
            normal,
            boundsFollowRotation);

        Vector3 position = hit.Point - normal * minimumProjection;
        Vector3 rotationDegrees = EditorEntityTransform.FromMatrix(
            Matrix4x4.CreateFromQuaternion(rotation)).RotationDegrees;
        return original with { Position = position, RotationDegrees = rotationDegrees };
    }

    public static float CalculateMinimumProjection(
        EditorEntityTransform transform,
        Vector3 localMinimum,
        Vector3 localMaximum,
        Vector3 normal,
        bool boundsFollowRotation)
    {
        Quaternion rotation = GetRotation(transform);
        return CalculateMinimumProjection(
            transform.ScaleOrSize,
            rotation,
            localMinimum,
            localMaximum,
            normal,
            boundsFollowRotation);
    }

    private static float CalculateMinimumProjection(
        Vector3 scale,
        Quaternion rotation,
        Vector3 localMinimum,
        Vector3 localMaximum,
        Vector3 normal,
        bool boundsFollowRotation)
    {
        float minimumProjection = float.PositiveInfinity;
        for (int index = 0; index < 8; index++)
        {
            var corner = new Vector3(
                (index & 1) == 0 ? localMinimum.X : localMaximum.X,
                (index & 2) == 0 ? localMinimum.Y : localMaximum.Y,
                (index & 4) == 0 ? localMinimum.Z : localMaximum.Z);
            Vector3 scaled = corner * scale;
            Vector3 offset = boundsFollowRotation ? Vector3.Transform(scaled, rotation) : scaled;
            minimumProjection = MathF.Min(minimumProjection, Vector3.Dot(offset, normal));
        }
        return minimumProjection;
    }

    private static Quaternion GetRotation(EditorEntityTransform transform)
    {
        Matrix4x4 matrix = (transform with
        {
            Position = Vector3.Zero,
            ScaleOrSize = Vector3.One,
        }).CreateMatrix();
        if (!Matrix4x4.Decompose(matrix, out _, out Quaternion rotation, out _))
            throw new InvalidOperationException("The selected entity rotation could not be decomposed.");
        return Quaternion.Normalize(rotation);
    }

    private static Quaternion RotationBetween(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float dot = Math.Clamp(Vector3.Dot(from, to), -1.0f, 1.0f);
        if (dot > 0.999999f)
            return Quaternion.Identity;
        if (dot < -0.999999f)
        {
            Vector3 basis = MathF.Abs(from.X) < MathF.Abs(from.Y)
                ? (MathF.Abs(from.X) < MathF.Abs(from.Z) ? Vector3.UnitX : Vector3.UnitZ)
                : (MathF.Abs(from.Y) < MathF.Abs(from.Z) ? Vector3.UnitY : Vector3.UnitZ);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(from, basis)), MathF.PI);
        }

        Vector3 axis = Vector3.Normalize(Vector3.Cross(from, to));
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(dot));
    }
}
