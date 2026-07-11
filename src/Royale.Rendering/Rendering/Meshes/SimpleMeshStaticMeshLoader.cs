using System.Numerics;
using SimpleMesh;

namespace Royale.Rendering.Meshes;

public static class SimpleMeshStaticMeshLoader
{
    public static StaticMeshAsset LoadAssetFromFile(string assetId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Model model = Model.FromFile(path).AutoselectRoot(out _).CalculateNormals();
        var primitives = new List<StaticMeshPrimitive>();
        var textures = new Dictionary<string, StaticMeshTextureData>(StringComparer.Ordinal);

        foreach (ModelNode root in model.Roots)
            AppendNode(root, Matrix4x4.Identity, model, primitives, textures, path);

        if (primitives.Count == 0)
            throw new InvalidDataException($"SimpleMesh model '{path}' did not contain triangle geometry.");

        return new StaticMeshAsset(assetId, primitives);
    }

    public static StaticMeshGeometry LoadFromFile(string path)
    {
        StaticMeshAsset asset = LoadAssetFromFile(Path.GetFileNameWithoutExtension(path), path);
        if (asset.Primitives.Count == 1)
            return asset.Primitives[0].Geometry;

        var vertices = new List<StaticMeshVertex>();
        var indices = new List<ushort>();
        foreach (StaticMeshPrimitive primitive in asset.Primitives)
        {
            int baseVertex = vertices.Count;
            if (baseVertex + primitive.Geometry.Vertices.Count > ushort.MaxValue)
                throw new InvalidDataException($"SimpleMesh model '{path}' exceeds the 16-bit static mesh vertex limit.");

            vertices.AddRange(primitive.Geometry.Vertices);
            indices.AddRange(primitive.Geometry.Indices.Select(index => checked((ushort)(baseVertex + index))));
        }

        return new StaticMeshGeometry(vertices, indices);
    }

    private static void AppendNode(
        ModelNode node,
        Matrix4x4 parentTransform,
        Model model,
        List<StaticMeshPrimitive> primitives,
        Dictionary<string, StaticMeshTextureData> textures,
        string path)
    {
        Matrix4x4 worldTransform = node.Transform * parentTransform;

        if (node.Geometry is not null)
            AppendGeometry(node.Geometry, worldTransform, model, primitives, textures, path, node.Name);

        foreach (ModelNode child in node.Children)
            AppendNode(child, worldTransform, model, primitives, textures, path);
    }

    private static void AppendGeometry(
        Geometry geometry,
        Matrix4x4 worldTransform,
        Model model,
        List<StaticMeshPrimitive> primitives,
        Dictionary<string, StaticMeshTextureData> textures,
        string path,
        string nodeName)
    {
        if (geometry.Kind != GeometryKind.Triangles)
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains unsupported non-triangle geometry.");

        if (!Matrix4x4.Invert(worldTransform, out Matrix4x4 inverseWorldTransform))
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' has a non-invertible transform.");

        Matrix4x4 normalTransform = Matrix4x4.Transpose(inverseWorldTransform);
        int sourceVertexCount = geometry.Vertices.Count;

        if (sourceVertexCount > ushort.MaxValue)
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' exceeds the 16-bit static mesh vertex limit.");

        for (int groupIndex = 0; groupIndex < geometry.Groups.Length; groupIndex++)
        {
            TriangleGroup group = geometry.Groups[groupIndex];
            ValidateGroup(group, geometry, sourceVertexCount, path, nodeName);

            bool hasTextureCoordinates =
                (geometry.Vertices.Descriptor.Attributes & VertexAttributes.Texture1) == VertexAttributes.Texture1;
            if (group.Material.DiffuseTexture is not null && !hasTextureCoordinates)
                throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' material '{group.Material.Name}' has a base-color texture but no texture coordinates.");

            var vertices = new StaticMeshVertex[sourceVertexCount];
            for (int vertexIndex = 0; vertexIndex < sourceVertexCount; vertexIndex++)
            {
                Vector3 position = Vector3.Transform(geometry.Vertices.Position[vertexIndex], worldTransform);
                Vector3 normal = Vector3.TransformNormal(geometry.Vertices.Normal[vertexIndex], normalTransform);
                Vector2 textureCoordinate = hasTextureCoordinates
                    ? geometry.Vertices.Texture1[vertexIndex]
                    : Vector2.Zero;

                if (!IsFinite(position))
                    throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid vertex position.");
                if (!IsFinite(normal) || normal.LengthSquared() <= 0.0f)
                    throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid vertex normal.");
                if (!IsFinite(textureCoordinate))
                    throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid texture coordinate.");

                normal = Vector3.Normalize(normal);
                if (!IsFinite(normal))
                    throw new InvalidDataException($"SimpleMesh model '{path}' contains an invalid normalized vertex normal.");

                vertices[vertexIndex] = new StaticMeshVertex(position, normal, textureCoordinate);
            }

            var indices = new ushort[group.IndexCount];
            for (int localIndex = 0; localIndex < group.IndexCount; localIndex++)
            {
                uint sourceIndex = geometry.Indices[group.StartIndex + localIndex] + (uint)group.BaseVertex;
                indices[localIndex] = checked((ushort)sourceIndex);
            }

            string materialName = string.IsNullOrWhiteSpace(group.Material.Name)
                ? $"material-{groupIndex}"
                : group.Material.Name;
            primitives.Add(new StaticMeshPrimitive(
                $"{nodeName}/{materialName}",
                new StaticMeshGeometry(vertices, indices),
                CreateMaterial(group.Material, model, textures, path)));
        }
    }

    private static void ValidateGroup(
        TriangleGroup group,
        Geometry geometry,
        int sourceVertexCount,
        string path,
        string nodeName)
    {
        if (group.IndexCount <= 0 || group.IndexCount % 3 != 0)
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains a non-triangle group.");
        if (group.StartIndex < 0 || group.StartIndex + group.IndexCount > geometry.Indices.Length)
            throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains an index range outside the index buffer.");

        for (int index = 0; index < group.IndexCount; index++)
        {
            uint sourceIndex = geometry.Indices[group.StartIndex + index] + (uint)group.BaseVertex;
            if (sourceIndex >= sourceVertexCount)
                throw new InvalidDataException($"SimpleMesh model '{path}' node '{nodeName}' contains an index outside the vertex buffer.");
        }
    }

    private static StaticMeshMaterial CreateMaterial(
        Material material,
        Model model,
        Dictionary<string, StaticMeshTextureData> textures,
        string path)
    {
        Vector4 baseColor = new(
            material.DiffuseColor.R,
            material.DiffuseColor.G,
            material.DiffuseColor.B,
            material.DiffuseColor.A);
        if (!IsFinite(baseColor))
            throw new InvalidDataException($"SimpleMesh model '{path}' material '{material.Name}' has an invalid base color.");

        StaticMeshTextureData? texture = null;
        if (material.DiffuseTexture is TextureInfo textureInfo)
        {
            if (!model.Images.TryGetValue(textureInfo.Name, out ImageData? image))
                throw new InvalidDataException($"SimpleMesh model '{path}' material '{material.Name}' references missing image '{textureInfo.Name}'.");

            if (!textures.TryGetValue(textureInfo.Name, out texture))
            {
                texture = new StaticMeshTextureData(image.Name, image.MimeType, image.Data.ToArray());
                textures.Add(textureInfo.Name, texture);
            }
        }

        return new StaticMeshMaterial(baseColor, texture);
    }

    private static bool IsFinite(Vector2 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y);

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static bool IsFinite(Vector4 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z) && float.IsFinite(vector.W);
}
