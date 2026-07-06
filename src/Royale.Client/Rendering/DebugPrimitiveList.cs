using System.Numerics;

namespace Royale.Client.Rendering;

public sealed class DebugPrimitiveList
{
    private const int DefaultCircleSegments = 48;
    private readonly List<DebugLine> lines = [];

    public IReadOnlyList<DebugLine> Lines => lines;

    public int LineCount => lines.Count;

    public bool IsEmpty => lines.Count == 0;

    public void AddLine(Vector3 start, Vector3 end, Vector4 color)
    {
        if (!IsFinite(start) || !IsFinite(end) || !IsFinite(color))
            return;

        lines.Add(new DebugLine(start, end, color));
    }

    public void AddPoint(Vector3 center, float size, Vector4 color)
    {
        if (!float.IsFinite(size) || size <= 0.0f)
            return;

        float halfSize = size * 0.5f;
        AddLine(center - Vector3.UnitX * halfSize, center + Vector3.UnitX * halfSize, color);
        AddLine(center - Vector3.UnitY * halfSize, center + Vector3.UnitY * halfSize, color);
        AddLine(center - Vector3.UnitZ * halfSize, center + Vector3.UnitZ * halfSize, color);
    }

    public void AddTransform(Matrix4x4 transform, float axisLength)
    {
        if (!float.IsFinite(axisLength) || axisLength <= 0.0f)
            return;

        Vector3 origin = Vector3.Transform(Vector3.Zero, transform);
        AddLine(origin, Vector3.Transform(Vector3.UnitX * axisLength, transform), new Vector4(1.0f, 0.12f, 0.12f, 1.0f));
        AddLine(origin, Vector3.Transform(Vector3.UnitY * axisLength, transform), new Vector4(0.25f, 1.0f, 0.25f, 1.0f));
        AddLine(origin, Vector3.Transform(Vector3.UnitZ * axisLength, transform), new Vector4(0.22f, 0.45f, 1.0f, 1.0f));
    }

    public void AddWireBounds(Vector3 min, Vector3 max, Vector4 color)
    {
        Vector3 center = (min + max) * 0.5f;
        Vector3 extents = (max - min) * 0.5f;
        AddWireBox(extents, Matrix4x4.CreateTranslation(center), color);
    }

    public void AddWireBox(Vector3 extents, Matrix4x4 transform, Vector4 color)
    {
        if (!IsFinite(extents) || extents.X < 0.0f || extents.Y < 0.0f || extents.Z < 0.0f)
            return;

        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = Vector3.Transform(new Vector3(-extents.X, -extents.Y, -extents.Z), transform);
        corners[1] = Vector3.Transform(new Vector3(extents.X, -extents.Y, -extents.Z), transform);
        corners[2] = Vector3.Transform(new Vector3(extents.X, -extents.Y, extents.Z), transform);
        corners[3] = Vector3.Transform(new Vector3(-extents.X, -extents.Y, extents.Z), transform);
        corners[4] = Vector3.Transform(new Vector3(-extents.X, extents.Y, -extents.Z), transform);
        corners[5] = Vector3.Transform(new Vector3(extents.X, extents.Y, -extents.Z), transform);
        corners[6] = Vector3.Transform(new Vector3(extents.X, extents.Y, extents.Z), transform);
        corners[7] = Vector3.Transform(new Vector3(-extents.X, extents.Y, extents.Z), transform);

        AddLine(corners[0], corners[1], color);
        AddLine(corners[1], corners[2], color);
        AddLine(corners[2], corners[3], color);
        AddLine(corners[3], corners[0], color);
        AddLine(corners[4], corners[5], color);
        AddLine(corners[5], corners[6], color);
        AddLine(corners[6], corners[7], color);
        AddLine(corners[7], corners[4], color);
        AddLine(corners[0], corners[4], color);
        AddLine(corners[1], corners[5], color);
        AddLine(corners[2], corners[6], color);
        AddLine(corners[3], corners[7], color);
    }

    public void AddCapsule(Vector3 center1, Vector3 center2, float radius, Vector4 color)
    {
        if (!float.IsFinite(radius) || radius <= 0.0f)
            return;

        AddCircleXz(center1, radius, color, 24);
        AddCircleXz(center2, radius, color, 24);
        AddLine(center1 + Vector3.UnitX * radius, center2 + Vector3.UnitX * radius, color);
        AddLine(center1 - Vector3.UnitX * radius, center2 - Vector3.UnitX * radius, color);
        AddLine(center1 + Vector3.UnitZ * radius, center2 + Vector3.UnitZ * radius, color);
        AddLine(center1 - Vector3.UnitZ * radius, center2 - Vector3.UnitZ * radius, color);
    }

    public void AddCircleXz(Vector3 center, float radius, Vector4 color, int segmentCount = DefaultCircleSegments)
    {
        AddCircle(center, radius, color, segmentCount, static angle => new Vector3(MathF.Cos(angle), 0.0f, MathF.Sin(angle)));
    }

    public void AddCircleXy(Vector3 center, float radius, Vector4 color, int segmentCount = DefaultCircleSegments)
    {
        AddCircle(center, radius, color, segmentCount, static angle => new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0.0f));
    }

    public void AddCircleYz(Vector3 center, float radius, Vector4 color, int segmentCount = DefaultCircleSegments)
    {
        AddCircle(center, radius, color, segmentCount, static angle => new Vector3(0.0f, MathF.Cos(angle), MathF.Sin(angle)));
    }

    public DebugLineVertex[] ToVertices()
    {
        if (lines.Count == 0)
            return [];

        var vertices = new DebugLineVertex[checked(lines.Count * 2)];
        for (int index = 0; index < lines.Count; index++)
        {
            DebugLine line = lines[index];
            vertices[index * 2] = new DebugLineVertex(line.Start, line.Color);
            vertices[index * 2 + 1] = new DebugLineVertex(line.End, line.Color);
        }

        return vertices;
    }

    private void AddCircle(Vector3 center, float radius, Vector4 color, int segmentCount, Func<float, Vector3> unitPoint)
    {
        if (!float.IsFinite(radius) || radius <= 0.0f || segmentCount < 3)
            return;

        for (int index = 0; index < segmentCount; index++)
        {
            float angle0 = MathF.Tau * index / segmentCount;
            float angle1 = MathF.Tau * (index + 1) / segmentCount;
            AddLine(center + unitPoint(angle0) * radius, center + unitPoint(angle1) * radius, color);
        }
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static bool IsFinite(Vector4 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z) && float.IsFinite(vector.W);
}

public readonly record struct DebugLine(Vector3 Start, Vector3 End, Vector4 Color);
