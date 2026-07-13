using System.Numerics;
using Royale.Editor.Documents;
using Royale.Simulation.World;

namespace Royale.Editor.Viewport.FaceSnap;

public static class EditorConstrainedFaceSnapPlacement
{
    private const float MinimumReachableNormalLengthSquared = 0.000001f;

    public static bool TryCalculate(
        EditorEntityTransform original,
        EditorEntityTransform candidate,
        Vector3 localMinimum,
        Vector3 localMaximum,
        MapStaticRayHit hit,
        EditorTranslationConstraint constraint,
        EditorTransformOrientation orientation,
        bool boundsFollowRotation,
        out EditorEntityTransform result)
    {
        result = candidate;
        if (constraint == EditorTranslationConstraint.None)
            return false;

        Vector3 normal = Vector3.Normalize(hit.Normal);
        (Vector3 x, Vector3 y, Vector3 z) =
            EditorTranslationConstraintResolver.CreateBasis(original, orientation);
        Vector3 allowedNormal = Vector3.Zero;
        Vector3 allowedMovement = Vector3.Zero;
        Vector3 candidateMovement = candidate.Position - original.Position;
        AddAllowed(EditorTranslationConstraint.X, x);
        AddAllowed(EditorTranslationConstraint.Y, y);
        AddAllowed(EditorTranslationConstraint.Z, z);

        float reachableLengthSquared = allowedNormal.LengthSquared();
        if (reachableLengthSquared < MinimumReachableNormalLengthSquared)
            return false;

        Vector3 constrainedPosition = original.Position + allowedMovement;
        float minimumProjection = EditorFaceSnapPlacement.CalculateMinimumProjection(
            original,
            localMinimum,
            localMaximum,
            normal,
            boundsFollowRotation);
        float targetOriginProjection = Vector3.Dot(hit.Point, normal) - minimumProjection;
        float error = targetOriginProjection - Vector3.Dot(constrainedPosition, normal);
        Vector3 snappedPosition = constrainedPosition + allowedNormal * (error / reachableLengthSquared);
        if (!IsFinite(snappedPosition))
            return false;

        result = original with { Position = snappedPosition };
        return true;

        void AddAllowed(EditorTranslationConstraint axis, Vector3 basis)
        {
            if (!constraint.HasFlag(axis))
                return;
            allowedNormal += basis * Vector3.Dot(normal, basis);
            allowedMovement += basis * Vector3.Dot(candidateMovement, basis);
        }
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
}
