using System.Numerics;
using Royale.Box3D.Bindings;

namespace Royale.Simulation.Debug;

public sealed class Box3DDebugShapeGeometry
{
    private Box3DDebugShapeGeometry(B3ShapeId shapeId, B3ShapeType type, IReadOnlyList<Box3DDebugShapeSegment> segments)
    {
        ShapeId = shapeId;
        Type = type;
        Segments = segments;
    }

    public B3ShapeId ShapeId { get; }

    public B3ShapeType Type { get; }

    public IReadOnlyList<Box3DDebugShapeSegment> Segments { get; }

    public static unsafe Box3DDebugShapeGeometry Create(in B3DebugShape debugShape)
    {
        return debugShape.Type switch
        {
            B3ShapeType.HullShape => CreateHull(debugShape.ShapeId, debugShape.Shape),
            B3ShapeType.CapsuleShape => CreateCapsule(debugShape.ShapeId, debugShape.Shape),
            B3ShapeType.SphereShape => CreateSphere(debugShape.ShapeId, debugShape.Shape),
            _ => new Box3DDebugShapeGeometry(debugShape.ShapeId, debugShape.Type, []),
        };
    }

    private static unsafe Box3DDebugShapeGeometry CreateHull(B3ShapeId shapeId, nint hullPointer)
    {
        if (hullPointer == nint.Zero)
            return new Box3DDebugShapeGeometry(shapeId, B3ShapeType.HullShape, []);

        B3HullData* hull = (B3HullData*)hullPointer;
        B3Vec3* points = (B3Vec3*)((byte*)hull + hull->PointOffset);
        B3HullHalfEdge* edges = (B3HullHalfEdge*)((byte*)hull + hull->EdgeOffset);
        var segments = new List<Box3DDebugShapeSegment>(hull->EdgeCount / 2);

        for (int edgeIndex = 0; edgeIndex < hull->EdgeCount; edgeIndex++)
        {
            B3HullHalfEdge edge = edges[edgeIndex];
            if (edgeIndex > edge.Twin)
                continue;

            B3HullHalfEdge next = edges[edge.Next];
            segments.Add(new Box3DDebugShapeSegment(
                ToVector3(points[edge.Origin]),
                ToVector3(points[next.Origin])));
        }

        return new Box3DDebugShapeGeometry(shapeId, B3ShapeType.HullShape, segments);
    }

    private static unsafe Box3DDebugShapeGeometry CreateCapsule(B3ShapeId shapeId, nint capsulePointer)
    {
        if (capsulePointer == nint.Zero)
            return new Box3DDebugShapeGeometry(shapeId, B3ShapeType.CapsuleShape, []);

        B3Capsule capsule = *(B3Capsule*)capsulePointer;
        var builder = new DebugShapeSegmentBuilder();
        builder.AddCapsule(ToVector3(capsule.Center1), ToVector3(capsule.Center2), capsule.Radius);
        return new Box3DDebugShapeGeometry(shapeId, B3ShapeType.CapsuleShape, builder.ToArray());
    }

    private static unsafe Box3DDebugShapeGeometry CreateSphere(B3ShapeId shapeId, nint spherePointer)
    {
        if (spherePointer == nint.Zero)
            return new Box3DDebugShapeGeometry(shapeId, B3ShapeType.SphereShape, []);

        B3Sphere sphere = *(B3Sphere*)spherePointer;
        var builder = new DebugShapeSegmentBuilder();
        Vector3 center = ToVector3(sphere.Center);
        builder.AddCircleXz(center, sphere.Radius);
        builder.AddCircleXy(center, sphere.Radius);
        builder.AddCircleYz(center, sphere.Radius);
        return new Box3DDebugShapeGeometry(shapeId, B3ShapeType.SphereShape, builder.ToArray());
    }

    private static Vector3 ToVector3(B3Vec3 vector) => new(vector.X, vector.Y, vector.Z);

    private sealed class DebugShapeSegmentBuilder
    {
        private const int CircleSegments = 24;
        private readonly List<Box3DDebugShapeSegment> segments = [];

        public void AddCapsule(Vector3 center1, Vector3 center2, float radius)
        {
            AddCircleXz(center1, radius);
            AddCircleXz(center2, radius);

            AddSegment(center1 + new Vector3(radius, 0.0f, 0.0f), center2 + new Vector3(radius, 0.0f, 0.0f));
            AddSegment(center1 - new Vector3(radius, 0.0f, 0.0f), center2 - new Vector3(radius, 0.0f, 0.0f));
            AddSegment(center1 + new Vector3(0.0f, 0.0f, radius), center2 + new Vector3(0.0f, 0.0f, radius));
            AddSegment(center1 - new Vector3(0.0f, 0.0f, radius), center2 - new Vector3(0.0f, 0.0f, radius));
        }

        public void AddCircleXz(Vector3 center, float radius)
        {
            AddCircle(center, radius, static angle => new Vector3(MathF.Cos(angle), 0.0f, MathF.Sin(angle)));
        }

        public void AddCircleXy(Vector3 center, float radius)
        {
            AddCircle(center, radius, static angle => new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0.0f));
        }

        public void AddCircleYz(Vector3 center, float radius)
        {
            AddCircle(center, radius, static angle => new Vector3(0.0f, MathF.Cos(angle), MathF.Sin(angle)));
        }

        public Box3DDebugShapeSegment[] ToArray() => segments.ToArray();

        private void AddCircle(Vector3 center, float radius, Func<float, Vector3> unitPoint)
        {
            for (int index = 0; index < CircleSegments; index++)
            {
                float angle0 = MathF.Tau * index / CircleSegments;
                float angle1 = MathF.Tau * (index + 1) / CircleSegments;
                AddSegment(center + unitPoint(angle0) * radius, center + unitPoint(angle1) * radius);
            }
        }

        private void AddSegment(Vector3 start, Vector3 end)
        {
            segments.Add(new Box3DDebugShapeSegment(start, end));
        }
    }
}

public readonly record struct Box3DDebugShapeSegment(Vector3 Start, Vector3 End);
