using System.Numerics;
using System.Runtime.InteropServices;
using Royale.Box3D.Bindings;
using Royale.Simulation;

namespace Royale.Client.Rendering;

public static class Box3DDebugDrawAdapter
{
    private static readonly B3DrawShapeFcn DrawShapeCallback = OnDrawShape;
    private static readonly B3DrawSegmentFcn DrawSegmentCallback = OnDrawSegment;
    private static readonly B3DrawTransformFcn DrawTransformCallback = OnDrawTransform;
    private static readonly B3DrawPointFcn DrawPointCallback = OnDrawPoint;
    private static readonly B3DrawSphereFcn DrawSphereCallback = OnDrawSphere;
    private static readonly B3DrawCapsuleFcn DrawCapsuleCallback = OnDrawCapsule;
    private static readonly B3DrawBoundsFcn DrawBoundsCallback = OnDrawBounds;
    private static readonly B3DrawBoxFcn DrawBoxCallback = OnDrawBox;
    private static readonly B3DrawStringFcn DrawStringCallback = OnDrawString;

    public static void AppendWorld(MapStaticCollisionWorld collisionWorld, DebugPrimitiveList primitives)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(primitives);

        B3DebugDraw draw = Box3DBindingSurface.b3DefaultDebugDraw();
        draw.DrawShapeFcn = Marshal.GetFunctionPointerForDelegate(DrawShapeCallback);
        draw.DrawSegmentFcn = Marshal.GetFunctionPointerForDelegate(DrawSegmentCallback);
        draw.DrawTransformFcn = Marshal.GetFunctionPointerForDelegate(DrawTransformCallback);
        draw.DrawPointFcn = Marshal.GetFunctionPointerForDelegate(DrawPointCallback);
        draw.DrawSphereFcn = Marshal.GetFunctionPointerForDelegate(DrawSphereCallback);
        draw.DrawCapsuleFcn = Marshal.GetFunctionPointerForDelegate(DrawCapsuleCallback);
        draw.DrawBoundsFcn = Marshal.GetFunctionPointerForDelegate(DrawBoundsCallback);
        draw.DrawBoxFcn = Marshal.GetFunctionPointerForDelegate(DrawBoxCallback);
        draw.DrawStringFcn = Marshal.GetFunctionPointerForDelegate(DrawStringCallback);
        draw.DrawShapes = true;
        draw.DrawBounds = false;
        draw.DrawJoints = false;
        draw.DrawContacts = false;

        using CallbackHandle handle = new(primitives);
        draw.Context = handle.Pointer;

        Box3DBindingSurface.b3World_Draw(collisionWorld.WorldId, ref draw, Box3DBindingSurface.B3DefaultMaskBits);
    }

    private static bool OnDrawShape(nint userShape, B3WorldTransform transform, B3HexColor color, nint context)
    {
        if (userShape == nint.Zero)
            return true;

        if (GCHandle.FromIntPtr(userShape).Target is not Box3DDebugShapeGeometry geometry)
            return true;

        DebugPrimitiveList primitives = GetPrimitives(context);
        Matrix4x4 world = ToMatrix(transform);
        Vector4 lineColor = ToColor(color);

        foreach (Box3DDebugShapeSegment segment in geometry.Segments)
            primitives.AddLine(Vector3.Transform(segment.Start, world), Vector3.Transform(segment.End, world), lineColor);

        return true;
    }

    private static void OnDrawSegment(B3Pos p1, B3Pos p2, B3HexColor color, nint context)
    {
        GetPrimitives(context).AddLine(ToVector3(p1), ToVector3(p2), ToColor(color));
    }

    private static void OnDrawTransform(B3WorldTransform transform, nint context)
    {
        GetPrimitives(context).AddTransform(ToMatrix(transform), axisLength: 0.3f);
    }

    private static void OnDrawPoint(B3Pos p, float size, B3HexColor color, nint context)
    {
        float worldSize = Math.Clamp(size * 0.01f, 0.03f, 0.25f);
        GetPrimitives(context).AddPoint(ToVector3(p), worldSize, ToColor(color));
    }

    private static void OnDrawSphere(B3Pos p, float radius, B3HexColor color, float alpha, nint context)
    {
        Vector4 lineColor = ToColor(color, alpha);
        DebugPrimitiveList primitives = GetPrimitives(context);
        Vector3 center = ToVector3(p);
        primitives.AddCircleXz(center, radius, lineColor, 24);
        primitives.AddCircleXy(center, radius, lineColor, 24);
        primitives.AddCircleYz(center, radius, lineColor, 24);
    }

    private static void OnDrawCapsule(B3Pos p1, B3Pos p2, float radius, B3HexColor color, float alpha, nint context)
    {
        GetPrimitives(context).AddCapsule(ToVector3(p1), ToVector3(p2), radius, ToColor(color, alpha));
    }

    private static void OnDrawBounds(B3Aabb aabb, B3HexColor color, nint context)
    {
        GetPrimitives(context).AddWireBounds(ToVector3(aabb.LowerBound), ToVector3(aabb.UpperBound), ToColor(color));
    }

    private static void OnDrawBox(B3Vec3 extents, B3WorldTransform transform, B3HexColor color, nint context)
    {
        GetPrimitives(context).AddWireBox(ToVector3(extents), ToMatrix(transform), ToColor(color));
    }

    private static void OnDrawString(B3Pos p, nint text, B3HexColor color, nint context)
    {
    }

    private static DebugPrimitiveList GetPrimitives(nint context) =>
        (DebugPrimitiveList)GCHandle.FromIntPtr(context).Target!;

    private static Matrix4x4 ToMatrix(B3WorldTransform transform)
    {
        Quaternion rotation = ToQuaternion(transform.Q);
        return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(ToVector3(transform.P));
    }

    private static Quaternion ToQuaternion(B3Quat quaternion)
    {
        var value = new Quaternion(quaternion.V.X, quaternion.V.Y, quaternion.V.Z, quaternion.S);
        float lengthSquared = value.LengthSquared();
        return float.IsFinite(lengthSquared) && lengthSquared > 0.0f
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;
    }

    private static Vector3 ToVector3(B3Pos vector) => new(vector.X, vector.Y, vector.Z);

    private static Vector3 ToVector3(B3Vec3 vector) => new(vector.X, vector.Y, vector.Z);

    private static Vector4 ToColor(B3HexColor color, float alpha = 1.0f)
    {
        uint packed = (uint)color & 0x00FFFFFFu;
        return new Vector4(
            ((packed >> 16) & 0xFF) / 255.0f,
            ((packed >> 8) & 0xFF) / 255.0f,
            (packed & 0xFF) / 255.0f,
            Math.Clamp(alpha, 0.0f, 1.0f));
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
