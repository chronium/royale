using System.Numerics;
using SimpleMesh;

namespace Royale.Client.Rendering.Meshes;

public static class SimpleMeshStaticMeshLoader
{
    public static StaticMeshGeometry LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        Model model = Model.FromStream(stream, TextureIgnoringResources.Instance).AutoselectRoot(out _).CalculateNormals();
        var vertices = new List<StaticMeshVertex>();
        var indices = new List<ushort>();

        foreach (ModelNode root in model.Roots)
            AppendNode(root, Matrix4x4.Identity, vertices, indices, path);

        if (vertices.Count == 0 || indices.Count == 0)
            throw new InvalidDataException($"SimpleMesh model '{path}' did not contain triangle geometry.");

        if (indices.Count % 3 != 0)
            throw new InvalidDataException($"SimpleMesh model '{path}' produced a non-triangle index count.");

        return new StaticMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static void AppendNode(
        ModelNode node,
        Matrix4x4 parentTransform,
        List<StaticMeshVertex> vertices,
        List<ushort> indices,
        string path)
    {
        Matrix4x4 worldTransform = node.Transform * parentTransform;

        if (node.Geometry is not null)
            AppendGeometry(node.Geometry, worldTransform, vertices, indices, path, node.Name);

        foreach (ModelNode child in node.Children)
            AppendNode(child, worldTransform, vertices, indices, path);
    }

    private static void AppendGeometry(
        Geometry geometry,
        Matrix4x4 worldTransform,
        List<StaticMeshVertex> vertices,
        List<ushort> indices,
        string path,
        string nodeName)
    {
        if (geometry.Kind != GeometryKind.Triangles)
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains unsupported non-triangle geometry.");

        if (!Matrix4x4.Invert(worldTransform, out Matrix4x4 inverseWorldTransform))
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' has a non-invertible transform.");

        Matrix4x4 normalTransform = Matrix4x4.Transpose(inverseWorldTransform);
        int baseVertex = vertices.Count;
        int sourceVertexCount = geometry.Vertices.Count;

        if (baseVertex + sourceVertexCount > ushort.MaxValue)
            throw new InvalidDataException($"SimpleMesh model '{path}' exceeds the 16-bit static mesh vertex limit.");

        for (int vertexIndex = 0; vertexIndex < sourceVertexCount; vertexIndex++)
        {
            Vector3 position = Vector3.Transform(geometry.Vertices.Position[vertexIndex], worldTransform);
            Vector3 normal = Vector3.TransformNormal(geometry.Vertices.Normal[vertexIndex], normalTransform);

            if (!IsFinite(position))
                throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid vertex position.");

            if (!IsFinite(normal) || normal.LengthSquared() <= 0.0f)
                throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid vertex normal.");

            normal = Vector3.Normalize(normal);

            if (!IsFinite(normal))
                throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid normalized vertex normal.");

            vertices.Add(new StaticMeshVertex(position, normal));
        }

        foreach (TriangleGroup group in geometry.Groups)
        {
            if (group.IndexCount <= 0 || group.IndexCount % 3 != 0)
                throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains a non-triangle group.");

            for (int groupIndex = 0; groupIndex < group.IndexCount; groupIndex++)
            {
                int indexOffset = group.StartIndex + groupIndex;

                if (indexOffset < 0 || indexOffset >= geometry.Indices.Length)
                    throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains an index outside the index buffer.");

                uint sourceIndex = geometry.Indices[indexOffset] + (uint)group.BaseVertex;

                if (sourceIndex >= sourceVertexCount)
                    throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains an index outside the vertex buffer.");

                indices.Add(checked((ushort)(baseVertex + (int)sourceIndex)));
            }
        }
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) &&
        float.IsFinite(vector.Y) &&
        float.IsFinite(vector.Z);

    private sealed class TextureIgnoringResources : IExternalResources
    {
        public static TextureIgnoringResources Instance { get; } = new();

        public bool CanLoadResources => true;

        public Stream OpenStream(string filename) => new MemoryStream();
    }
}
