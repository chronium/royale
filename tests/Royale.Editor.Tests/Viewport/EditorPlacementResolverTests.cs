using System.Numerics;
using Royale.Content.Maps;
using Royale.Editor.Viewport;

namespace Royale.Editor.Tests.Viewport;

public sealed class EditorPlacementResolverTests
{
    private static readonly MapBounds Bounds = new()
    {
        Min = new MapVector3(-5, -2, -5),
        Max = new MapVector3(5, 4, 5),
    };

    [Fact]
    public void IntersectsGridPlaneAndAppliesTranslationSnapping()
    {
        var ray = new EditorRay(new Vector3(1.2f, 5, 2.7f), -Vector3.UnitY);

        Vector3 result = EditorPlacementResolver.Resolve(ray, Bounds, true, 1);

        Assert.Equal(new Vector3(1, 0, 3), result);
    }

    [Fact]
    public void ClampsGridIntersectionToWorldBounds()
    {
        var ray = new EditorRay(new Vector3(20, 5, -20), -Vector3.UnitY);

        Vector3 result = EditorPlacementResolver.Resolve(ray, Bounds, false, 1);

        Assert.Equal(new Vector3(5, 0, -5), result);
    }

    [Fact]
    public void ParallelOrBehindRayFallsBackToBoundsCentre()
    {
        Assert.Equal(new Vector3(0, 1, 0), EditorPlacementResolver.Resolve(
            new EditorRay(new Vector3(2, 3, 4), Vector3.UnitX), Bounds, false, 1));
        Assert.Equal(new Vector3(0, 1, 0), EditorPlacementResolver.Resolve(
            new EditorRay(new Vector3(0, -1, 0), -Vector3.UnitY), Bounds, false, 1));
    }
}
