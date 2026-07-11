using System.Runtime.InteropServices;
using Royale.Box3D.Bindings.Interop;

using Royale.Box3D.Bodies;
using Royale.Box3D.Geometry;
using Royale.Box3D.Runtime;
using Royale.Box3D.Worlds;

namespace Royale.Box3D.Tests.Bindings;

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
        AssertSize<B3Capacity>(20);
        AssertSize<B3WorldDef>(144);
        AssertSize<B3MotionLocks>(6);
        AssertSize<B3BodyDef>(104);
        AssertSize<B3SurfaceMaterial>(40);
        AssertSize<B3ShapeDef>(120);
        AssertSize<B3Capsule>(28);
        AssertSize<B3Sphere>(16);
        AssertSize<B3DebugShape>(24);
        AssertSize<B3DebugDraw>(136);
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
        AssertSize<B3HullVertex>(1);
        AssertSize<B3HullHalfEdge>(4);
        AssertSize<B3HullFace>(1);
        AssertSize<B3HullData>(136);
        AssertSize<B3BoxHull>(440);
        AssertSize<B3MeshDef>(40);
        AssertSize<B3MeshTriangle>(12);
        AssertSize<B3MeshNode>(32);
        AssertSize<B3MeshData>(88);
        AssertSize<B3Mesh>(24);
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
    public void WorldDefinitionOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3Capacity>(nameof(B3Capacity.StaticShapeCount), 0);
        AssertOffset<B3Capacity>(nameof(B3Capacity.DynamicShapeCount), 4);
        AssertOffset<B3Capacity>(nameof(B3Capacity.StaticBodyCount), 8);
        AssertOffset<B3Capacity>(nameof(B3Capacity.DynamicBodyCount), 12);
        AssertOffset<B3Capacity>(nameof(B3Capacity.ContactCount), 16);

        AssertOffset<B3WorldDef>(nameof(B3WorldDef.Gravity), 0);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.RestitutionThreshold), 12);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.HitEventThreshold), 16);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.ContactHertz), 20);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.ContactDampingRatio), 24);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.ContactSpeed), 28);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.MaximumLinearSpeed), 32);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.FrictionCallback), 40);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.RestitutionCallback), 48);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.EnableSleep), 56);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.EnableContinuous), 57);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.WorkerCount), 60);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.EnqueueTask), 64);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.FinishTask), 72);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.UserTaskContext), 80);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.UserData), 88);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.CreateDebugShape), 96);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.DestroyDebugShape), 104);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.UserDebugShapeContext), 112);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.Capacity), 120);
        AssertOffset<B3WorldDef>(nameof(B3WorldDef.InternalValue), 140);
    }

    [Fact]
    public void BodyAndShapeDefinitionOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3MotionLocks>(nameof(B3MotionLocks.LinearX), 0);
        AssertOffset<B3MotionLocks>(nameof(B3MotionLocks.LinearY), 1);
        AssertOffset<B3MotionLocks>(nameof(B3MotionLocks.LinearZ), 2);
        AssertOffset<B3MotionLocks>(nameof(B3MotionLocks.AngularX), 3);
        AssertOffset<B3MotionLocks>(nameof(B3MotionLocks.AngularY), 4);
        AssertOffset<B3MotionLocks>(nameof(B3MotionLocks.AngularZ), 5);

        AssertOffset<B3BodyDef>(nameof(B3BodyDef.Type), 0);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.Position), 4);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.Rotation), 16);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.LinearVelocity), 32);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.AngularVelocity), 44);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.LinearDamping), 56);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.AngularDamping), 60);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.GravityScale), 64);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.SleepThreshold), 68);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.Name), 72);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.UserData), 80);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.MotionLocks), 88);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.EnableSleep), 94);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.IsAwake), 95);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.IsBullet), 96);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.IsEnabled), 97);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.AllowFastRotation), 98);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.EnableContactRecycling), 99);
        AssertOffset<B3BodyDef>(nameof(B3BodyDef.InternalValue), 100);

        AssertOffset<B3SurfaceMaterial>(nameof(B3SurfaceMaterial.Friction), 0);
        AssertOffset<B3SurfaceMaterial>(nameof(B3SurfaceMaterial.Restitution), 4);
        AssertOffset<B3SurfaceMaterial>(nameof(B3SurfaceMaterial.RollingResistance), 8);
        AssertOffset<B3SurfaceMaterial>(nameof(B3SurfaceMaterial.TangentVelocity), 12);
        AssertOffset<B3SurfaceMaterial>(nameof(B3SurfaceMaterial.UserMaterialId), 24);
        AssertOffset<B3SurfaceMaterial>(nameof(B3SurfaceMaterial.CustomColor), 32);

        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.Name), 0);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.UserData), 8);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.Materials), 16);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.MaterialCount), 24);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.BaseMaterial), 32);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.Density), 72);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.ExplosionScale), 76);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.Filter), 80);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.EnableCustomFiltering), 104);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.IsSensor), 105);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.EnableSensorEvents), 106);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.EnableContactEvents), 107);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.EnableHitEvents), 108);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.EnablePreSolveEvents), 109);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.InvokeContactCreation), 110);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.UpdateBodyMass), 111);
        AssertOffset<B3ShapeDef>(nameof(B3ShapeDef.InternalValue), 112);

        AssertOffset<B3Capsule>(nameof(B3Capsule.Center1), 0);
        AssertOffset<B3Capsule>(nameof(B3Capsule.Center2), 12);
        AssertOffset<B3Capsule>(nameof(B3Capsule.Radius), 24);
        AssertOffset<B3Sphere>(nameof(B3Sphere.Center), 0);
        AssertOffset<B3Sphere>(nameof(B3Sphere.Radius), 12);
    }

    [Fact]
    public void DebugDrawOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3DebugShape>(nameof(B3DebugShape.ShapeId), 0);
        AssertOffset<B3DebugShape>(nameof(B3DebugShape.Type), 8);
        AssertOffset<B3DebugShape>(nameof(B3DebugShape.Shape), 16);

        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawShapeFcn), 0);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawSegmentFcn), 8);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawTransformFcn), 16);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawPointFcn), 24);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawSphereFcn), 32);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawCapsuleFcn), 40);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawBoundsFcn), 48);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawBoxFcn), 56);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawStringFcn), 64);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawingBounds), 72);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.ForceScale), 96);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.JointScale), 100);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawShapes), 104);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawBounds), 107);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawContacts), 111);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawAnchorA), 112);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.DrawIslands), 121);
        AssertOffset<B3DebugDraw>(nameof(B3DebugDraw.Context), 128);
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
    public void HullOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3HullVertex>(nameof(B3HullVertex.Edge), 0);
        AssertOffset<B3HullHalfEdge>(nameof(B3HullHalfEdge.Next), 0);
        AssertOffset<B3HullHalfEdge>(nameof(B3HullHalfEdge.Twin), 1);
        AssertOffset<B3HullHalfEdge>(nameof(B3HullHalfEdge.Origin), 2);
        AssertOffset<B3HullHalfEdge>(nameof(B3HullHalfEdge.Face), 3);
        AssertOffset<B3HullFace>(nameof(B3HullFace.Edge), 0);

        AssertOffset<B3HullData>(nameof(B3HullData.Version), 0);
        AssertOffset<B3HullData>(nameof(B3HullData.ByteCount), 8);
        AssertOffset<B3HullData>(nameof(B3HullData.Hash), 12);
        AssertOffset<B3HullData>(nameof(B3HullData.Aabb), 16);
        AssertOffset<B3HullData>(nameof(B3HullData.SurfaceArea), 40);
        AssertOffset<B3HullData>(nameof(B3HullData.Volume), 44);
        AssertOffset<B3HullData>(nameof(B3HullData.InnerRadius), 48);
        AssertOffset<B3HullData>(nameof(B3HullData.Center), 52);
        AssertOffset<B3HullData>(nameof(B3HullData.CentralInertia), 64);
        AssertOffset<B3HullData>(nameof(B3HullData.VertexCount), 100);
        AssertOffset<B3HullData>(nameof(B3HullData.VertexOffset), 104);
        AssertOffset<B3HullData>(nameof(B3HullData.PointOffset), 108);
        AssertOffset<B3HullData>(nameof(B3HullData.EdgeCount), 112);
        AssertOffset<B3HullData>(nameof(B3HullData.EdgeOffset), 116);
        AssertOffset<B3HullData>(nameof(B3HullData.FaceCount), 120);
        AssertOffset<B3HullData>(nameof(B3HullData.FaceOffset), 124);
        AssertOffset<B3HullData>(nameof(B3HullData.PlaneOffset), 128);
        AssertOffset<B3HullData>(nameof(B3HullData.Padding), 132);

        AssertOffset<B3BoxHull>(nameof(B3BoxHull.Base), 0);
        AssertOffset<B3BoxHull>(nameof(B3BoxHull.BoxVertices), 136);
        AssertOffset<B3BoxHull>(nameof(B3BoxHull.BoxPoints), 144);
        AssertOffset<B3BoxHull>(nameof(B3BoxHull.BoxEdges), 240);
        AssertOffset<B3BoxHull>(nameof(B3BoxHull.BoxFaces), 336);
        AssertOffset<B3BoxHull>(nameof(B3BoxHull.Padding), 342);
        AssertOffset<B3BoxHull>(nameof(B3BoxHull.BoxPlanes), 344);
    }

    [Fact]
    public void MeshOffsetsMatchPinnedBox3DLayout()
    {
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.Vertices), 0);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.Indices), 8);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.MaterialIndices), 16);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.WeldTolerance), 24);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.VertexCount), 28);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.TriangleCount), 32);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.WeldVertices), 36);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.UseMedianSplit), 37);
        AssertOffset<B3MeshDef>(nameof(B3MeshDef.IdentifyEdges), 38);

        AssertOffset<B3MeshTriangle>(nameof(B3MeshTriangle.Index1), 0);
        AssertOffset<B3MeshTriangle>(nameof(B3MeshTriangle.Index2), 4);
        AssertOffset<B3MeshTriangle>(nameof(B3MeshTriangle.Index3), 8);
        AssertOffset<B3MeshNode>(nameof(B3MeshNode.LowerBound), 0);
        AssertOffset<B3MeshNode>(nameof(B3MeshNode.Data), 12);
        AssertOffset<B3MeshNode>(nameof(B3MeshNode.UpperBound), 16);
        AssertOffset<B3MeshNode>(nameof(B3MeshNode.TriangleOffset), 28);

        AssertOffset<B3MeshData>(nameof(B3MeshData.Version), 0);
        AssertOffset<B3MeshData>(nameof(B3MeshData.ByteCount), 8);
        AssertOffset<B3MeshData>(nameof(B3MeshData.Hash), 12);
        AssertOffset<B3MeshData>(nameof(B3MeshData.Bounds), 16);
        AssertOffset<B3MeshData>(nameof(B3MeshData.SurfaceArea), 40);
        AssertOffset<B3MeshData>(nameof(B3MeshData.TreeHeight), 44);
        AssertOffset<B3MeshData>(nameof(B3MeshData.DegenerateCount), 48);
        AssertOffset<B3MeshData>(nameof(B3MeshData.NodeOffset), 52);
        AssertOffset<B3MeshData>(nameof(B3MeshData.NodeCount), 56);
        AssertOffset<B3MeshData>(nameof(B3MeshData.VertexOffset), 60);
        AssertOffset<B3MeshData>(nameof(B3MeshData.VertexCount), 64);
        AssertOffset<B3MeshData>(nameof(B3MeshData.TriangleOffset), 68);
        AssertOffset<B3MeshData>(nameof(B3MeshData.TriangleCount), 72);
        AssertOffset<B3MeshData>(nameof(B3MeshData.MaterialOffset), 76);
        AssertOffset<B3MeshData>(nameof(B3MeshData.MaterialCount), 80);
        AssertOffset<B3MeshData>(nameof(B3MeshData.FlagsOffset), 84);
        AssertOffset<B3Mesh>(nameof(B3Mesh.Data), 0);
        AssertOffset<B3Mesh>(nameof(B3Mesh.Scale), 8);
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
        Assert.Equal(0xA9A9A9, (int)B3HexColor.DarkGray);
        Assert.Equal(0xFFD700, (int)B3HexColor.Gold);
        Assert.Equal(0xFFFFFF, (int)B3HexColor.White);
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
