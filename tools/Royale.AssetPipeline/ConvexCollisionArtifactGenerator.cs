using System.Numerics;
using Royale.Content;
using SimpleMesh;
using SimpleMesh.Convex;

namespace Royale.AssetPipeline;

public sealed record CollisionTriangleGeometry(IReadOnlyList<Vector3> Vertices, IReadOnlyList<int> Indices);

public static class SimpleMeshCollisionGeometryExtractor
{
    public static CollisionTriangleGeometry LoadFromFile(string path, bool discardDegenerateTriangles = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Extract(Model.FromFile(path), path, discardDegenerateTriangles);
    }

    public static CollisionTriangleGeometry Extract(Model model, string context, bool discardDegenerateTriangles = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var vertices = new List<Vector3>();
        var indices = new List<int>();
        foreach (ModelNode root in model.Roots)
            AppendNode(root, Matrix4x4.Identity, vertices, indices, context, discardDegenerateTriangles);

        if (indices.Count == 0)
            throw new InvalidDataException($"Collision source '{context}' did not contain triangle geometry.");
        return new CollisionTriangleGeometry(vertices, indices);
    }

    private static void AppendNode(
        ModelNode node,
        Matrix4x4 parentTransform,
        List<Vector3> vertices,
        List<int> indices,
        string context,
        bool discardDegenerateTriangles)
    {
        Matrix4x4 worldTransform = node.Transform * parentTransform;
        if (node.Geometry is not null)
            AppendGeometry(node.Geometry, worldTransform, vertices, indices, context, node.Name, discardDegenerateTriangles);

        foreach (ModelNode child in node.Children)
            AppendNode(child, worldTransform, vertices, indices, context, discardDegenerateTriangles);
    }

    private static void AppendGeometry(
        Geometry geometry,
        Matrix4x4 transform,
        List<Vector3> vertices,
        List<int> indices,
        string context,
        string nodeName,
        bool discardDegenerateTriangles)
    {
        if (geometry.Kind != GeometryKind.Triangles)
            throw new InvalidDataException($"Collision source '{context}' node '{nodeName}' is not triangle geometry.");

        foreach (TriangleGroup group in geometry.Groups)
        {
            if (group.IndexCount <= 0 || group.IndexCount % 3 != 0 ||
                group.StartIndex < 0 || group.StartIndex + group.IndexCount > geometry.Indices.Length)
            {
                throw new InvalidDataException($"Collision source '{context}' node '{nodeName}' has an invalid triangle group.");
            }

            for (int localIndex = 0; localIndex < group.IndexCount; localIndex += 3)
            {
                int triangleStart = vertices.Count;
                for (int corner = 0; corner < 3; corner++)
                {
                    uint sourceIndex = geometry.Indices[group.StartIndex + localIndex + corner] + (uint)group.BaseVertex;
                    if (sourceIndex >= geometry.Vertices.Count)
                        throw new InvalidDataException($"Collision source '{context}' node '{nodeName}' has an index outside its vertex array.");

                    Vector3 position = NormalizeZero(Vector3.Transform(geometry.Vertices.Position[(int)sourceIndex], transform));
                    if (!IsFinite(position))
                        throw new InvalidDataException($"Collision source '{context}' node '{nodeName}' has a non-finite transformed vertex.");
                    vertices.Add(position);
                    indices.Add(triangleStart + corner);
                }

                Vector3 edgeA = vertices[triangleStart + 1] - vertices[triangleStart];
                Vector3 edgeB = vertices[triangleStart + 2] - vertices[triangleStart];
                if (Vector3.Cross(edgeA, edgeB).LengthSquared() <= 1e-12f)
                {
                    vertices.RemoveRange(triangleStart, 3);
                    indices.RemoveRange(indices.Count - 3, 3);
                    if (!discardDegenerateTriangles)
                        throw new InvalidDataException($"Collision source '{context}' node '{nodeName}' contains a degenerate triangle.");
                }
            }
        }
    }

    private static Vector3 NormalizeZero(Vector3 value) => new(
        value.X == 0.0f ? 0.0f : value.X,
        value.Y == 0.0f ? 0.0f : value.Y,
        value.Z == 0.0f ? 0.0f : value.Z);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public static class ConvexCollisionArtifactGenerator
{
    public static ModelCollisionArtifact Generate(CollisionTriangleGeometry geometry, string context)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ValidateTriangles(geometry, context);

        Vector3[] points = geometry.Indices
            .Select(index => CollisionGeometryCanonicalizer.CanonicalizePoint(geometry.Vertices[index]))
            .Distinct()
            .OrderBy(vertex => vertex.X)
            .ThenBy(vertex => vertex.Y)
            .ThenBy(vertex => vertex.Z)
            .ToArray();

        ValidateThreeDimensional(points, context);
        if (points.Length < 4 || !TryBuildHull(points, out Hull? hull))
        {
            Vector3 minimum = points.Aggregate(new Vector3(float.PositiveInfinity), Vector3.Min);
            Vector3 maximum = points.Aggregate(new Vector3(float.NegativeInfinity), Vector3.Max);
            throw new InvalidDataException(
                $"Collision source '{context}' does not form a valid three-dimensional convex hull " +
                $"({points.Length} unique points, bounds {minimum} to {maximum}).");
        }
        Hull generatedHull = hull!;
        Vector3[] sortedVertices = generatedHull.Vertices
            .ToArray()
            .Select(CollisionGeometryCanonicalizer.CanonicalizePoint)
            .OrderBy(vertex => vertex.X)
            .ThenBy(vertex => vertex.Y)
            .ThenBy(vertex => vertex.Z)
            .ToArray();
        var artifact = new ModelCollisionArtifact
        {
            Version = ModelCollisionArtifact.CurrentVersion,
            Kind = ModelCollisionArtifactKind.Convex,
            Vertices = sortedVertices.Select(vertex => new ModelCollisionVertex(vertex.X, vertex.Y, vertex.Z)).ToList(),
            Indices = [],
        };
        ModelCollisionArtifactLoader.Validate(artifact, context);
        return artifact;
    }

    private static bool TryBuildHull(Vector3[] sortedPoints, out Hull? hull)
    {
        IEnumerable<Vector3[]> attempts =
        [
            sortedPoints,
            sortedPoints.Reverse().ToArray(),
            sortedPoints.OrderBy(StablePointKey).ThenBy(point => point.X).ThenBy(point => point.Y).ThenBy(point => point.Z).ToArray(),
        ];

        foreach (Vector3[] points in attempts)
        {
            if (Hull.TryQuickhull(points, out hull))
                return true;
        }

        hull = null;
        return false;
    }

    private static uint StablePointKey(Vector3 point)
    {
        uint hash = 2166136261;
        hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(point.X)) * 16777619;
        hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(point.Y)) * 16777619;
        return (hash ^ (uint)BitConverter.SingleToInt32Bits(point.Z)) * 16777619;
    }

    private static void ValidateThreeDimensional(Vector3[] points, string context)
    {
        if (points.Length < 4)
            throw new InvalidDataException($"Collision source '{context}' has fewer than four unique points.");

        Vector3 origin = points[0];
        Vector3 linePoint = points.MaxBy(point => Vector3.DistanceSquared(origin, point));
        Vector3 line = linePoint - origin;
        Vector3 planePoint = points.MaxBy(point => Vector3.Cross(line, point - origin).LengthSquared());
        Vector3 normal = Vector3.Cross(line, planePoint - origin);
        float volumeMeasure = points.Max(point => MathF.Abs(Vector3.Dot(normal, point - origin)));
        float scale = MathF.Max(CollisionGeometryCanonicalizer.PositionPrecision, points.Max(point => Vector3.Distance(origin, point)));
        if (!float.IsFinite(volumeMeasure) || volumeMeasure <= 1e-6f * scale * scale * scale)
            throw new InvalidDataException($"Collision source '{context}' is coplanar or too small to form a three-dimensional hull.");
    }

    private static void ValidateTriangles(CollisionTriangleGeometry geometry, string context)
    {
        if (geometry.Indices.Count == 0 || geometry.Indices.Count % 3 != 0)
            throw new InvalidDataException($"Collision source '{context}' must contain complete triangles.");

        for (int offset = 0; offset < geometry.Indices.Count; offset += 3)
        {
            Vector3 a = ReadVertex(geometry, offset, context);
            Vector3 b = ReadVertex(geometry, offset + 1, context);
            Vector3 c = ReadVertex(geometry, offset + 2, context);

            if (Vector3.Cross(b - a, c - a).LengthSquared() <= 1e-12f)
                throw new InvalidDataException($"Collision source '{context}' contains a degenerate triangle.");
        }
    }

    private static Vector3 ReadVertex(CollisionTriangleGeometry geometry, int indexOffset, string context)
    {
        int index = geometry.Indices[indexOffset];
        if ((uint)index >= geometry.Vertices.Count)
            throw new InvalidDataException($"Collision source '{context}' has index {index} outside its vertex array.");

        Vector3 vertex = geometry.Vertices[index];
        if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
            throw new InvalidDataException($"Collision source '{context}' has a non-finite vertex.");
        return vertex;
    }

}

public static class TriangleMeshCollisionArtifactGenerator
{
    public static ModelCollisionArtifact Generate(CollisionTriangleGeometry geometry, string context)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        if (geometry.Indices.Count == 0 || geometry.Indices.Count % 3 != 0)
            throw new InvalidDataException($"Collision source '{context}' must contain complete triangles.");

        var sourceTriangles = new List<(Vector3 A, Vector3 B, Vector3 C)>(geometry.Indices.Count / 3);
        for (int offset = 0; offset < geometry.Indices.Count; offset += 3)
        {
            Vector3 a = ReadVertex(geometry, offset, context);
            Vector3 b = ReadVertex(geometry, offset + 1, context);
            Vector3 c = ReadVertex(geometry, offset + 2, context);
            if (Vector3.Cross(b - a, c - a).LengthSquared() <= 1e-12f)
                continue;
            sourceTriangles.Add((a, b, c));
        }

        if (sourceTriangles.Count == 0)
            throw new InvalidDataException($"Collision source '{context}' did not contain non-degenerate triangle geometry after canonicalization.");

        Vector3[] sortedVertices = sourceTriangles
            .SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
            .Distinct()
            .OrderBy(vertex => vertex.X)
            .ThenBy(vertex => vertex.Y)
            .ThenBy(vertex => vertex.Z)
            .ToArray();
        var vertexIndices = sortedVertices
            .Select((vertex, index) => (vertex, index))
            .ToDictionary(item => item.vertex, item => item.index);

        var triangles = sourceTriangles
            .Select(triangle =>
            {
                int a = vertexIndices[triangle.A];
                int b = vertexIndices[triangle.B];
                int c = vertexIndices[triangle.C];
                return (Surface: SortAscending(a, b, c), Triangle: RotateToSmallest(a, b, c));
            })
            .DistinctBy(item => item.Surface)
            .Select(item => item.Triangle)
            .OrderBy(triangle => triangle.A)
            .ThenBy(triangle => triangle.B)
            .ThenBy(triangle => triangle.C)
            .ToArray();

        var artifact = new ModelCollisionArtifact
        {
            Version = ModelCollisionArtifact.CurrentVersion,
            Kind = ModelCollisionArtifactKind.TriangleMesh,
            Vertices = sortedVertices.Select(vertex => new ModelCollisionVertex(vertex.X, vertex.Y, vertex.Z)).ToList(),
            Indices = triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToList(),
        };
        ModelCollisionArtifactLoader.Validate(artifact, context);
        return artifact;
    }

    private static Vector3 ReadVertex(CollisionTriangleGeometry geometry, int indexOffset, string context)
    {
        int index = geometry.Indices[indexOffset];
        if ((uint)index >= geometry.Vertices.Count)
            throw new InvalidDataException($"Collision source '{context}' has index {index} outside its vertex array.");

        Vector3 vertex = geometry.Vertices[index];
        if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
            throw new InvalidDataException($"Collision source '{context}' has a non-finite vertex.");
        return CollisionGeometryCanonicalizer.CanonicalizePoint(vertex);
    }

    private static (int A, int B, int C) RotateToSmallest(int a, int b, int c)
    {
        if (a <= b && a <= c) return (a, b, c);
        return b <= c ? (b, c, a) : (c, a, b);
    }

    private static (int A, int B, int C) SortAscending(int a, int b, int c)
    {
        if (a > b) (a, b) = (b, a);
        if (b > c) (b, c) = (c, b);
        if (a > b) (a, b) = (b, a);
        return (a, b, c);
    }
}

internal static class CollisionGeometryCanonicalizer
{
    public const float PositionPrecision = 0.000001f;

    public static Vector3 CanonicalizePoint(Vector3 value) => NormalizeZero(new Vector3(
        MathF.Round(value.X / PositionPrecision) * PositionPrecision,
        MathF.Round(value.Y / PositionPrecision) * PositionPrecision,
        MathF.Round(value.Z / PositionPrecision) * PositionPrecision));

    private static Vector3 NormalizeZero(Vector3 value) => new(
        value.X == 0.0f ? 0.0f : value.X,
        value.Y == 0.0f ? 0.0f : value.Y,
        value.Z == 0.0f ? 0.0f : value.Z);
}
