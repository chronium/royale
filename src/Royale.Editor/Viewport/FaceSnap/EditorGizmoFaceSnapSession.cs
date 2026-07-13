using System.Numerics;
using Royale.Editor.Documents;
using Royale.Simulation.World;

namespace Royale.Editor.Viewport.FaceSnap;

public sealed class EditorGizmoFaceSnapSession : IDisposable
{
    private readonly EditorEntityTransform original;
    private readonly EditorPickTarget bounds;
    private readonly MapStaticCollisionWorld collisionWorld;
    private readonly string? excludedContentId;
    private readonly bool boundsFollowRotation;
    private bool disposed;

    public EditorGizmoFaceSnapSession(
        EditorMapDocument document,
        EditorEntityIdentity identity,
        EditorPickTarget bounds,
        MapStaticCollisionWorld collisionWorld)
    {
        ArgumentNullException.ThrowIfNull(document);
        EditorEntityTransforms.ValidateIdentity(document, identity);
        if (bounds.Identity.EditorId != identity.EditorId)
            throw new ArgumentException("Gizmo face-snap bounds must belong to the selected entity.", nameof(bounds));

        this.bounds = bounds;
        this.collisionWorld = collisionWorld ?? throw new ArgumentNullException(nameof(collisionWorld));
        original = EditorEntityTransforms.Get(document, identity);
        Candidate = original;
        excludedContentId = identity.Kind is EditorEntityKind.StaticBox or EditorEntityKind.StaticModel
            ? EditorEntityTransforms.GetDisplayId(document, identity)
            : null;
        boundsFollowRotation = identity.Kind is EditorEntityKind.StaticBox or EditorEntityKind.StaticModel;
    }

    public MapStaticRayHit? Hit { get; private set; }

    public EditorEntityTransform Candidate { get; private set; }

    public void UpdateCandidate(EditorEntityTransform candidate)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Candidate = candidate;
    }

    public bool TrySnap(
        EditorRay ray,
        EditorTranslationConstraint constraint,
        EditorTransformOrientation orientation,
        out EditorEntityTransform snapped,
        float distance = 10000.0f)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        MapStaticRayHit? hit = collisionWorld.CastRayFiltered(
            ray.Origin,
            Vector3.Normalize(ray.Direction) * distance,
            excludedContentId);
        if (hit is null || !EditorConstrainedFaceSnapPlacement.TryCalculate(
            original,
            Candidate,
            bounds.LocalMinimum,
            bounds.LocalMaximum,
            hit.Value,
            constraint,
            orientation,
            boundsFollowRotation,
            out snapped))
        {
            Hit = null;
            snapped = Candidate;
            return false;
        }

        Hit = hit;
        return true;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        collisionWorld.Dispose();
        Hit = null;
        disposed = true;
    }
}
