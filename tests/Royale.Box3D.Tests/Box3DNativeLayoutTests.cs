using System.Runtime.InteropServices;
using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

public sealed class Box3DNativeLayoutTests
{
    [Fact]
    public unsafe void FoundationalStructSizesMatchPinnedBox3DLayout()
    {
        AssertSize<B3Vec2>(8);
        AssertSize<B3Vec3>(12);
        AssertSize<B3CosSin>(8);
        AssertSize<B3Quat>(16);
        AssertSize<B3Transform>(28);
        AssertSize<B3Pos>(12);
        AssertSize<B3WorldTransform>(28);
        AssertSize<B3Matrix3>(36);
        AssertSize<B3Aabb>(24);
        AssertSize<B3Plane>(16);
        AssertSize<B3WorldId>(4);
        AssertSize<B3BodyId>(8);
        AssertSize<B3ShapeId>(8);
        AssertSize<B3JointId>(8);
        AssertSize<B3ContactId>(12);
        AssertSize<B3Version>(12);
        AssertSize<B3Filter>(24);
        AssertSize<B3QueryFilter>(32);
        AssertSize<B3Profile>(92);
        AssertSize<B3Counters>(200);
        AssertSize<B3RayCastInput>(28);
        AssertSize<B3RayResult>(64);
        AssertSize<B3ShapeProxy>(16);
        AssertSize<B3ShapeCastInput>(40);
        AssertSize<B3BoxCastInput>(40);
        AssertSize<B3CastOutput>(48);
        AssertSize<B3WorldCastOutput>(48);
        AssertSize<B3BodyCastResult>(56);
        AssertSize<B3TreeStats>(8);
        AssertSize<B3PlaneResult>(28);
        AssertSize<B3CollisionPlane>(28);
        AssertSize<B3PlaneSolverResult>(16);
        AssertSize<B3BodyPlaneResult>(36);
    }

    [Fact]
    public void FoundationalMathOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3Quat>(nameof(B3Quat.V), 0);
        AssertOffset<B3Quat>(nameof(B3Quat.S), 12);
        AssertOffset<B3Transform>(nameof(B3Transform.P), 0);
        AssertOffset<B3Transform>(nameof(B3Transform.Q), 12);
        AssertOffset<B3WorldTransform>(nameof(B3WorldTransform.P), 0);
        AssertOffset<B3WorldTransform>(nameof(B3WorldTransform.Q), 12);
        AssertOffset<B3Matrix3>(nameof(B3Matrix3.Cx), 0);
        AssertOffset<B3Matrix3>(nameof(B3Matrix3.Cy), 12);
        AssertOffset<B3Matrix3>(nameof(B3Matrix3.Cz), 24);
        AssertOffset<B3Aabb>(nameof(B3Aabb.LowerBound), 0);
        AssertOffset<B3Aabb>(nameof(B3Aabb.UpperBound), 12);
        AssertOffset<B3Plane>(nameof(B3Plane.Normal), 0);
        AssertOffset<B3Plane>(nameof(B3Plane.Offset), 12);
    }

    [Fact]
    public void OpaqueIdOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3WorldId>(nameof(B3WorldId.Index1), 0);
        AssertOffset<B3WorldId>(nameof(B3WorldId.Generation), 2);
        AssertOffset<B3BodyId>(nameof(B3BodyId.Index1), 0);
        AssertOffset<B3BodyId>(nameof(B3BodyId.World0), 4);
        AssertOffset<B3BodyId>(nameof(B3BodyId.Generation), 6);
        AssertOffset<B3ShapeId>(nameof(B3ShapeId.World0), 4);
        AssertOffset<B3JointId>(nameof(B3JointId.Generation), 6);
        AssertOffset<B3ContactId>(nameof(B3ContactId.World0), 4);
        AssertOffset<B3ContactId>(nameof(B3ContactId.Padding), 6);
        AssertOffset<B3ContactId>(nameof(B3ContactId.Generation), 8);
    }

    [Fact]
    public void SupportStructOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3Version>(nameof(B3Version.Major), 0);
        AssertOffset<B3Version>(nameof(B3Version.Minor), 4);
        AssertOffset<B3Version>(nameof(B3Version.Revision), 8);
        AssertOffset<B3Filter>(nameof(B3Filter.CategoryBits), 0);
        AssertOffset<B3Filter>(nameof(B3Filter.MaskBits), 8);
        AssertOffset<B3Filter>(nameof(B3Filter.GroupIndex), 16);
        AssertOffset<B3QueryFilter>(nameof(B3QueryFilter.CategoryBits), 0);
        AssertOffset<B3QueryFilter>(nameof(B3QueryFilter.MaskBits), 8);
        AssertOffset<B3QueryFilter>(nameof(B3QueryFilter.Id), 16);
        AssertOffset<B3QueryFilter>(nameof(B3QueryFilter.Name), 24);
        AssertOffset<B3Profile>(nameof(B3Profile.Step), 0);
        AssertOffset<B3Profile>(nameof(B3Profile.Sensors), 88);
        AssertOffset<B3Counters>(nameof(B3Counters.ColorCounts), 52);
        AssertOffset<B3Counters>(nameof(B3Counters.ManifoldCounts), 148);
        AssertOffset<B3Counters>(nameof(B3Counters.AwakeContactCount), 180);
        AssertOffset<B3Counters>(nameof(B3Counters.RootIterations), 196);
    }

    [Fact]
    public void QueryAndResultOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3RayCastInput>(nameof(B3RayCastInput.Translation), 12);
        AssertOffset<B3RayCastInput>(nameof(B3RayCastInput.MaxFraction), 24);
        AssertOffset<B3RayResult>(nameof(B3RayResult.ShapeId), 0);
        AssertOffset<B3RayResult>(nameof(B3RayResult.Point), 8);
        AssertOffset<B3RayResult>(nameof(B3RayResult.Normal), 20);
        AssertOffset<B3RayResult>(nameof(B3RayResult.UserMaterialId), 32);
        AssertOffset<B3RayResult>(nameof(B3RayResult.Hit), 60);
        AssertOffset<B3ShapeProxy>(nameof(B3ShapeProxy.Points), 0);
        AssertOffset<B3ShapeProxy>(nameof(B3ShapeProxy.Count), 8);
        AssertOffset<B3ShapeProxy>(nameof(B3ShapeProxy.Radius), 12);
        AssertOffset<B3ShapeCastInput>(nameof(B3ShapeCastInput.Translation), 16);
        AssertOffset<B3ShapeCastInput>(nameof(B3ShapeCastInput.MaxFraction), 28);
        AssertOffset<B3ShapeCastInput>(nameof(B3ShapeCastInput.CanEncroach), 32);
        AssertOffset<B3BoxCastInput>(nameof(B3BoxCastInput.Translation), 24);
        AssertOffset<B3BoxCastInput>(nameof(B3BoxCastInput.MaxFraction), 36);
        AssertOffset<B3CastOutput>(nameof(B3CastOutput.Point), 12);
        AssertOffset<B3CastOutput>(nameof(B3CastOutput.Hit), 44);
        AssertOffset<B3WorldCastOutput>(nameof(B3WorldCastOutput.Point), 12);
        AssertOffset<B3WorldCastOutput>(nameof(B3WorldCastOutput.Hit), 44);
        AssertOffset<B3BodyCastResult>(nameof(B3BodyCastResult.Point), 8);
        AssertOffset<B3BodyCastResult>(nameof(B3BodyCastResult.UserMaterialId), 40);
        AssertOffset<B3BodyCastResult>(nameof(B3BodyCastResult.Hit), 52);
        AssertOffset<B3PlaneResult>(nameof(B3PlaneResult.Point), 16);
        AssertOffset<B3CollisionPlane>(nameof(B3CollisionPlane.PushLimit), 16);
        AssertOffset<B3CollisionPlane>(nameof(B3CollisionPlane.ClipVelocity), 24);
        AssertOffset<B3PlaneSolverResult>(nameof(B3PlaneSolverResult.IterationCount), 12);
        AssertOffset<B3BodyPlaneResult>(nameof(B3BodyPlaneResult.Result), 8);
    }

    [Fact]
    public void EnumValuesMatchPinnedBox3DHeaders()
    {
        Assert.Equal(0, (int)B3BodyType.StaticBody);
        Assert.Equal(1, (int)B3BodyType.KinematicBody);
        Assert.Equal(2, (int)B3BodyType.DynamicBody);
        Assert.Equal(3, (int)B3BodyType.BodyTypeCount);

        Assert.Equal(0, (int)B3ShapeType.CapsuleShape);
        Assert.Equal(1, (int)B3ShapeType.CompoundShape);
        Assert.Equal(2, (int)B3ShapeType.HeightShape);
        Assert.Equal(3, (int)B3ShapeType.HullShape);
        Assert.Equal(4, (int)B3ShapeType.MeshShape);
        Assert.Equal(5, (int)B3ShapeType.SphereShape);
        Assert.Equal(6, (int)B3ShapeType.ShapeTypeCount);
    }

    private static unsafe void AssertSize<T>(int expected)
        where T : unmanaged
    {
        Assert.Equal(expected, sizeof(T));
    }

    private static void AssertOffset<T>(string fieldName, int expected)
    {
        Assert.Equal(expected, Marshal.OffsetOf<T>(fieldName).ToInt32());
    }
}
