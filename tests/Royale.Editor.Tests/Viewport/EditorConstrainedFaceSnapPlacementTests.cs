using System.Numerics;
using Royale.Editor.Documents;
using Royale.Editor.Viewport;
using Royale.Editor.Viewport.FaceSnap;
using Royale.Simulation.World;

namespace Royale.Editor.Tests.Viewport;

public sealed class EditorConstrainedFaceSnapPlacementTests
{
    [Fact]
    public void WorldAxisSnapMovesOnlyDraggedAxisAndMakesExactContact()
    {
        var original = new EditorEntityTransform(new Vector3(1, 2, 3), Vector3.Zero, new Vector3(2, 4, 6));
        var candidate = original with { Position = new Vector3(8, 7, 9) };

        bool snapped = EditorConstrainedFaceSnapPlacement.TryCalculate(
            original,
            candidate,
            new Vector3(-0.5f),
            new Vector3(0.5f),
            Hit(new Vector3(10, 0, 0), -Vector3.UnitX),
            EditorTranslationConstraint.X,
            EditorTransformOrientation.World,
            boundsFollowRotation: true,
            out EditorEntityTransform result);

        Assert.True(snapped);
        AssertVectorClose(new Vector3(9, 2, 3), result.Position);
        Assert.Equal(original.RotationDegrees, result.RotationDegrees);
        Assert.Equal(original.ScaleOrSize, result.ScaleOrSize);
    }

    [Fact]
    public void FloorSnapPreservesBothHorizontalCoordinates()
    {
        var original = new EditorEntityTransform(new Vector3(3, 8, -4), Vector3.Zero, new Vector3(2, 4, 6));
        var candidate = original with { Position = new Vector3(20, 5, 30) };

        Assert.True(EditorConstrainedFaceSnapPlacement.TryCalculate(
            original,
            candidate,
            new Vector3(-0.5f),
            new Vector3(0.5f),
            Hit(Vector3.Zero, Vector3.UnitY),
            EditorTranslationConstraint.Y,
            EditorTransformOrientation.World,
            boundsFollowRotation: true,
            out EditorEntityTransform result));

        AssertVectorClose(new Vector3(3, 2, -4), result.Position);
    }

    [Fact]
    public void PlaneSnapPreservesExcludedCoordinateAndUsesNonUniformAsymmetricBounds()
    {
        Vector3 normal = Vector3.Normalize(new Vector3(-1, -1, 0));
        var original = new EditorEntityTransform(new Vector3(1, 2, 7), new Vector3(0, 25, 0), new Vector3(2, 0.5f, 4));
        var candidate = original with { Position = new Vector3(6, 9, -20) };
        Vector3 minimum = new(-1, -2, -0.5f);
        Vector3 maximum = new(3, 1, 0.5f);
        Vector3 planePoint = new(10, 10, 0);

        Assert.True(EditorConstrainedFaceSnapPlacement.TryCalculate(
            original,
            candidate,
            minimum,
            maximum,
            Hit(planePoint, normal),
            EditorTranslationConstraint.XY,
            EditorTransformOrientation.World,
            boundsFollowRotation: true,
            out EditorEntityTransform result));

        Assert.Equal(original.Position.Z, result.Position.Z);
        float minimumDistance = CornerDistances(result, minimum, maximum, planePoint, normal).Min();
        Assert.InRange(minimumDistance, -0.0001f, 0.0001f);
    }

    [Fact]
    public void LocalAxisSnapUsesRotatedBasisAndFreezesOtherLocalCoordinates()
    {
        var original = new EditorEntityTransform(new Vector3(2, 3, 4), new Vector3(0, 90, 0), Vector3.One);
        (Vector3 localX, _, _) = EditorTranslationConstraintResolver.CreateBasis(
            original,
            EditorTransformOrientation.Local);
        var candidate = original with { Position = original.Position + localX * 3 + Vector3.UnitY * 5 };
        Vector3 planePoint = original.Position + localX * 10;

        Assert.True(EditorConstrainedFaceSnapPlacement.TryCalculate(
            original,
            candidate,
            new Vector3(-0.5f),
            new Vector3(0.5f),
            Hit(planePoint, -localX),
            EditorTranslationConstraint.X,
            EditorTransformOrientation.Local,
            boundsFollowRotation: true,
            out EditorEntityTransform result));

        Vector3 movement = result.Position - original.Position;
        Assert.InRange(Vector3.Cross(movement, localX).Length(), 0, 0.0001f);
        Assert.InRange(Vector3.Dot(movement, localX), 9.4999f, 9.5001f);
    }

    [Fact]
    public void SurfaceOutsideConstraintCannotSnap()
    {
        var original = new EditorEntityTransform(Vector3.Zero, Vector3.Zero, Vector3.One);
        var candidate = original with { Position = new Vector3(5, 0, 0) };

        Assert.False(EditorConstrainedFaceSnapPlacement.TryCalculate(
            original,
            candidate,
            new Vector3(-0.5f),
            new Vector3(0.5f),
            Hit(Vector3.Zero, Vector3.UnitY),
            EditorTranslationConstraint.X,
            EditorTransformOrientation.World,
            boundsFollowRotation: true,
            out EditorEntityTransform result));
        Assert.Equal(candidate, result);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, EditorTranslationConstraint.X)]
    [InlineData(false, true, false, false, false, false, EditorTranslationConstraint.Y)]
    [InlineData(false, false, true, false, false, false, EditorTranslationConstraint.Z)]
    [InlineData(false, false, false, true, false, false, EditorTranslationConstraint.XY)]
    [InlineData(false, false, false, false, true, false, EditorTranslationConstraint.YZ)]
    [InlineData(false, false, false, false, false, true, EditorTranslationConstraint.XZ)]
    public void HoveredGizmoPartResolvesConstraint(
        bool x,
        bool y,
        bool z,
        bool xy,
        bool yz,
        bool xz,
        EditorTranslationConstraint expected) =>
        Assert.Equal(expected, EditorTranslationConstraintResolver.Resolve(x, y, z, xy, yz, xz));

    private static MapStaticRayHit Hit(Vector3 point, Vector3 normal) => new(
        new MapStaticCollider("surface", MapStaticColliderKind.Box, null, Matrix4x4.Identity, default, default),
        point,
        normal,
        0.5f);

    private static IEnumerable<float> CornerDistances(
        EditorEntityTransform transform,
        Vector3 minimum,
        Vector3 maximum,
        Vector3 planePoint,
        Vector3 normal)
    {
        for (int index = 0; index < 8; index++)
        {
            var corner = new Vector3(
                (index & 1) == 0 ? minimum.X : maximum.X,
                (index & 2) == 0 ? minimum.Y : maximum.Y,
                (index & 4) == 0 ? minimum.Z : maximum.Z);
            yield return Vector3.Dot(Vector3.Transform(corner, transform.CreateMatrix()) - planePoint, normal);
        }
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 0.0002f, $"Expected {expected}, actual {actual}.");
}
