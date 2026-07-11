using System.Runtime.InteropServices;
using Royale.Box3D.Bindings.Interop;

using Royale.Box3D.Tests.Infrastructure;

using Royale.Box3D.Bodies;
using Royale.Box3D.Geometry;
using Royale.Box3D.Runtime;
using Royale.Box3D.Worlds;

namespace Royale.Box3D.Tests.Bindings;

[Collection(Box3DNativeTestCollection.Name)]
public unsafe sealed class Box3DDebugDrawBindingTests
{
    private static readonly B3DrawBoundsFcn BoundsCallback = OnDrawBounds;

    [Fact]
    public void DefaultDebugDrawReturnsInitializedCallbacksAndBounds()
    {
        B3DebugDraw draw = Box3DBindingSurface.b3DefaultDebugDraw();

        Assert.NotEqual(nint.Zero, draw.DrawShapeFcn);
        Assert.NotEqual(nint.Zero, draw.DrawSegmentFcn);
        Assert.NotEqual(nint.Zero, draw.DrawTransformFcn);
        Assert.NotEqual(nint.Zero, draw.DrawPointFcn);
        Assert.NotEqual(nint.Zero, draw.DrawSphereFcn);
        Assert.NotEqual(nint.Zero, draw.DrawCapsuleFcn);
        Assert.NotEqual(nint.Zero, draw.DrawBoundsFcn);
        Assert.NotEqual(nint.Zero, draw.DrawBoxFcn);
        Assert.NotEqual(nint.Zero, draw.DrawStringFcn);
        Assert.False(draw.DrawShapes);
        Assert.False(draw.DrawBounds);
        Assert.Equal(1.0f, draw.ForceScale);
        Assert.Equal(1.0f, draw.JointScale);
        Assert.InRange(draw.DrawingBounds.LowerBound.X, -100.1f, -99.9f);
        Assert.InRange(draw.DrawingBounds.UpperBound.X, 99.9f, 100.1f);
    }

    [Fact]
    public void WorldDrawCanInvokeManagedBoundsCallback()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            CreateStaticBox(worldId);
            Box3DBindingSurface.b3World_Step(worldId, 1.0f / 60.0f, 4);

            var context = new DrawContext();
            using CallbackHandle handle = new(context);
            B3DebugDraw draw = Box3DBindingSurface.b3DefaultDebugDraw();
            draw.DrawBoundsFcn = Marshal.GetFunctionPointerForDelegate(BoundsCallback);
            draw.DrawBounds = true;
            draw.Context = handle.Pointer;

            Box3DBindingSurface.b3World_Draw(worldId, ref draw, Box3DBindingSurface.B3DefaultMaskBits);

            Assert.True(context.BoundsCount > 0);
            Assert.True(context.LastBounds.UpperBound.X > context.LastBounds.LowerBound.X);
        }
        finally
        {
            if (Box3DBindingSurface.b3World_IsValid(worldId))
                Box3DBindingSurface.b3DestroyWorld(worldId);
        }
    }

    private static void OnDrawBounds(B3Aabb aabb, B3HexColor color, nint context)
    {
        var drawContext = (DrawContext)GCHandle.FromIntPtr(context).Target!;
        drawContext.BoundsCount++;
        drawContext.LastBounds = aabb;
    }

    private static B3WorldId CreateWorld()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        worldDef.Gravity = new B3Vec3();
        B3WorldId worldId = Box3DBindingSurface.b3CreateWorld(in worldDef);
        Assert.True(Box3DBindingSurface.b3World_IsValid(worldId));
        return worldId;
    }

    private static void CreateStaticBox(B3WorldId worldId)
    {
        B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
        bodyDef.Type = B3BodyType.StaticBody;
        B3BodyId bodyId = Box3DBindingSurface.b3CreateBody(worldId, in bodyDef);
        Assert.True(Box3DBindingSurface.b3Body_IsValid(bodyId));

        B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
        B3BoxHull box = Box3DBindingSurface.b3MakeCubeHull(1.0f);
        B3ShapeId shapeId = Box3DBindingSurface.b3CreateHullShape(bodyId, in shapeDef, in box.Base);
        Assert.True(Box3DBindingSurface.b3Shape_IsValid(shapeId));
    }

    private sealed class DrawContext
    {
        public int BoundsCount { get; set; }

        public B3Aabb LastBounds { get; set; }
    }

    private sealed class CallbackHandle : IDisposable
    {
        private GCHandle handle;

        public CallbackHandle(object target)
        {
            handle = GCHandle.Alloc(target);
        }

        public nint Pointer => GCHandle.ToIntPtr(handle);

        public void Dispose()
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}
