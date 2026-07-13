using System.Numerics;
using Royale.Editor.Documents;
using Royale.Simulation.World;

namespace Royale.Editor.Viewport.FaceSnap;

public sealed class EditorFaceSnapSession : IDisposable
{
    private readonly EditorMapDocument document;
    private readonly EditorEntityIdentity identity;
    private readonly EditorEntityTransform original;
    private readonly EditorPickTarget bounds;
    private readonly MapStaticCollisionWorld collisionWorld;
    private readonly string? excludedContentId;
    private readonly long documentRevision;
    private bool disposed;

    public EditorFaceSnapSession(
        EditorMapDocument document,
        EditorEntityIdentity identity,
        EditorPickTarget bounds,
        MapStaticCollisionWorld collisionWorld)
    {
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        EditorEntityTransforms.ValidateIdentity(document, identity);
        if (!EditorEntityTransforms.HasSpatialTransform(identity.Kind))
            throw new ArgumentException("Face snapping requires a spatial entity.", nameof(identity));
        if (bounds.Identity.EditorId != identity.EditorId)
            throw new ArgumentException("Face-snap bounds must belong to the selected entity.", nameof(bounds));
        this.identity = identity;
        this.bounds = bounds;
        this.collisionWorld = collisionWorld ?? throw new ArgumentNullException(nameof(collisionWorld));
        original = EditorEntityTransforms.Get(document, identity);
        documentRevision = document.Revision;
        excludedContentId = identity.Kind is EditorEntityKind.StaticBox or EditorEntityKind.StaticModel
            ? EditorEntityTransforms.GetDisplayId(document, identity)
            : null;
    }

    public Guid EditorId => identity.EditorId;

    public bool IsDocumentCurrent =>
        document.Revision == documentRevision && document.TryGetIdentity(identity.EditorId, out _);

    public bool HasPreview { get; private set; }

    public MapStaticRayHit? Hit { get; private set; }

    public bool TryPreview(EditorRay ray, EditorFaceSnapSettings settings, float distance = 10000.0f)
    {
        ThrowIfDisposed();
        if (!float.IsFinite(distance) || distance <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(distance));

        MapStaticRayHit? hit = collisionWorld.CastRayFiltered(
            ray.Origin,
            Vector3.Normalize(ray.Direction) * distance,
            excludedContentId);
        if (hit is null)
        {
            RestoreOriginal();
            Hit = null;
            HasPreview = false;
            return false;
        }

        EditorEntityTransform preview = EditorFaceSnapPlacement.Calculate(
            original,
            bounds.LocalMinimum,
            bounds.LocalMaximum,
            hit.Value,
            settings,
            identity.Kind is not EditorEntityKind.LootPoint and not EditorEntityKind.NavigationWaypoint,
            boundsFollowRotation: identity.Kind is EditorEntityKind.StaticBox or EditorEntityKind.StaticModel);
        EditorEntityTransforms.Set(document, identity, preview, validate: false);
        Hit = hit;
        HasPreview = true;
        return true;
    }

    public bool Commit()
    {
        ThrowIfDisposed();
        if (!HasPreview)
            return false;
        EditorEntityTransform after = EditorEntityTransforms.Get(document, identity);
        RestoreOriginal();
        if (!original.NearlyEquals(after))
            document.Execute(new SetEntityTransformCommand(identity.EditorId, original, after));
        DisposeWorld();
        return !original.NearlyEquals(after);
    }

    public void Cancel()
    {
        if (disposed)
            return;
        RestoreOriginal();
        DisposeWorld();
    }

    public void Dispose() => Cancel();

    private void RestoreOriginal()
    {
        if (document.TryGetIdentity(identity.EditorId, out EditorEntityIdentity current))
            EditorEntityTransforms.Set(document, current, original, validate: false);
    }

    private void DisposeWorld()
    {
        collisionWorld.Dispose();
        disposed = true;
        Hit = null;
        HasPreview = false;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
