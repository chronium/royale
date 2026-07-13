using System.Numerics;
using Royale.Editor.Documents;
using Royale.Editor.Viewport.FaceSnap;
using Royale.Simulation.World;

namespace Royale.Editor.Tests.Viewport;

public sealed class EditorFaceSnapPlacementTests
{
    [Fact]
    public void FloorAndWallPlacementSitBoxBoundsFlush()
    {
        var transform = new EditorEntityTransform(Vector3.Zero, Vector3.Zero, new Vector3(2.0f, 4.0f, 6.0f));

        EditorEntityTransform floor = Place(transform, new Vector3(-0.5f), new Vector3(0.5f), Vector3.Zero, Vector3.UnitY);
        EditorEntityTransform wall = Place(transform, new Vector3(-0.5f), new Vector3(0.5f), Vector3.Zero, Vector3.UnitX);

        AssertVectorClose(new Vector3(0.0f, 2.0f, 0.0f), floor.Position);
        AssertVectorClose(new Vector3(1.0f, 0.0f, 0.0f), wall.Position);
    }

    [Fact]
    public void RotatedSurfaceAndNonUniformAsymmetricBoundsUseExactSupportDistance()
    {
        Vector3 normal = Vector3.Normalize(new Vector3(1.0f, 1.0f, 0.0f));
        var transform = new EditorEntityTransform(Vector3.Zero, new Vector3(0.0f, 30.0f, 0.0f), new Vector3(2.0f, 0.5f, 4.0f));
        Vector3 minimum = new(-1.0f, -2.0f, -0.5f);
        Vector3 maximum = new(3.0f, 1.0f, 0.5f);

        EditorEntityTransform result = Place(transform, minimum, maximum, Vector3.Zero, normal);

        float minimumDistance = CornerDistances(result, minimum, maximum, Vector3.Zero, normal).Min();
        Assert.InRange(minimumDistance, -0.0001f, 0.0001f);
        Assert.All(CornerDistances(result, minimum, maximum, Vector3.Zero, normal), value => Assert.True(value >= -0.0001f));
    }

    [Theory]
    [InlineData(EditorFaceSnapAxis.PositiveX)]
    [InlineData(EditorFaceSnapAxis.NegativeX)]
    [InlineData(EditorFaceSnapAxis.PositiveY)]
    [InlineData(EditorFaceSnapAxis.NegativeY)]
    [InlineData(EditorFaceSnapAxis.PositiveZ)]
    [InlineData(EditorFaceSnapAxis.NegativeZ)]
    public void AlignmentRotatesEveryLocalAxisOntoSurfaceNormal(EditorFaceSnapAxis axis)
    {
        Vector3 normal = Vector3.Normalize(new Vector3(1.0f, 2.0f, -3.0f));
        var settings = new EditorFaceSnapSettings(true, axis);
        var original = new EditorEntityTransform(Vector3.Zero, new Vector3(12.0f, -24.0f, 18.0f), Vector3.One);

        EditorEntityTransform result = EditorFaceSnapPlacement.Calculate(
            original,
            new Vector3(-0.5f),
            new Vector3(0.5f),
            Hit(Vector3.Zero, normal),
            settings,
            alignmentSupported: true);

        Vector3 aligned = Vector3.Normalize(Vector3.TransformNormal(settings.GetLocalAxis(), result.CreateMatrix()));
        Assert.True(Vector3.Dot(aligned, normal) > 0.9999f);
    }

    [Fact]
    public void AntiParallelAlignmentAndProxyPlacementAreStable()
    {
        var settings = new EditorFaceSnapSettings(true, EditorFaceSnapAxis.PositiveY);
        EditorEntityTransform result = EditorFaceSnapPlacement.Calculate(
            new EditorEntityTransform(Vector3.Zero, Vector3.Zero, Vector3.One),
            new Vector3(-0.55f),
            new Vector3(0.55f),
            Hit(Vector3.Zero, -Vector3.UnitY),
            settings,
            alignmentSupported: true);

        Vector3 aligned = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, result.CreateMatrix()));
        Assert.True(Vector3.Dot(aligned, -Vector3.UnitY) > 0.9999f);
        AssertVectorClose(new Vector3(0.0f, -0.55f, 0.0f), result.Position);
    }

    [Fact]
    public void TranslationOnlyEntityIgnoresAlignment()
    {
        var original = new EditorEntityTransform(Vector3.Zero, Vector3.Zero, Vector3.One);
        EditorEntityTransform result = EditorFaceSnapPlacement.Calculate(
            original,
            new Vector3(-0.35f),
            new Vector3(0.35f),
            Hit(Vector3.Zero, Vector3.UnitY),
            new EditorFaceSnapSettings(true, EditorFaceSnapAxis.PositiveX),
            alignmentSupported: false);

        AssertVectorClose(Vector3.Zero, result.RotationDegrees);
        AssertVectorClose(new Vector3(0.0f, 0.35f, 0.0f), result.Position);
    }

    [Fact]
    public void SpawnProxyRemainsAxisAlignedWhenItsAttachmentAxisAligns()
    {
        Vector3 normal = Vector3.Normalize(new Vector3(1.0f, 1.0f, 1.0f));
        const float radius = 0.55f;
        EditorEntityTransform result = EditorFaceSnapPlacement.Calculate(
            new EditorEntityTransform(Vector3.Zero, Vector3.Zero, Vector3.One),
            new Vector3(-radius),
            new Vector3(radius),
            Hit(Vector3.Zero, normal),
            new EditorFaceSnapSettings(true, EditorFaceSnapAxis.PositiveY),
            alignmentSupported: true,
            boundsFollowRotation: false);

        float support = radius * (MathF.Abs(normal.X) + MathF.Abs(normal.Y) + MathF.Abs(normal.Z));
        AssertVectorClose(normal * support, result.Position);
        Vector3 aligned = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, result.CreateMatrix()));
        Assert.True(Vector3.Dot(aligned, normal) > 0.9999f);
    }

    private static EditorEntityTransform Place(
        EditorEntityTransform transform,
        Vector3 minimum,
        Vector3 maximum,
        Vector3 point,
        Vector3 normal) => EditorFaceSnapPlacement.Calculate(
            transform,
            minimum,
            maximum,
            Hit(point, normal),
            new EditorFaceSnapSettings(),
            alignmentSupported: true);

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
            Vector3 world = Vector3.Transform(corner, transform.CreateMatrix());
            yield return Vector3.Dot(world - planePoint, normal);
        }
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 0.0002f, $"Expected {expected}, actual {actual}.");
}
