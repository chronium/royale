using System.Text.Json;
using System.Text.Json.Serialization;

namespace Royale.Content.Models;

public sealed record ModelCollisionArtifact
{
    public const int CurrentVersion = 1;

    public int Version { get; init; }

    [JsonRequired]
    public ModelCollisionArtifactKind Kind { get; init; }

    public List<ModelCollisionVertex> Vertices { get; init; } = [];

    public List<int> Indices { get; init; } = [];
}

public readonly record struct ModelCollisionVertex(float X, float Y, float Z);

public enum ModelCollisionArtifactKind
{
    Convex = 1,
    TriangleMesh = 2,
}

public static class ModelCollisionArtifactLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static ModelCollisionArtifact Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Model collision artifact was not found at '{path}'.", path);

        ModelCollisionArtifact artifact;
        try
        {
            using FileStream stream = File.OpenRead(path);
            artifact = JsonSerializer.Deserialize<ModelCollisionArtifact>(stream, JsonOptions)
                ?? throw InvalidArtifact(path, "the document did not contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw InvalidArtifact(path, "the document is not valid strict JSON collision content.", exception);
        }

        Validate(artifact, path);
        return artifact;
    }

    public static JsonSerializerOptions CreateSerializerOptions(bool writeIndented = false) =>
        new(JsonOptions) { WriteIndented = writeIndented };

    public static void Validate(ModelCollisionArtifact artifact, string context)
    {
        if (artifact.Version != ModelCollisionArtifact.CurrentVersion)
            throw InvalidArtifact(context, $"version must be {ModelCollisionArtifact.CurrentVersion}.");
        if (artifact.Kind is not ModelCollisionArtifactKind.Convex and not ModelCollisionArtifactKind.TriangleMesh)
            throw InvalidArtifact(context, $"kind '{artifact.Kind}' is unsupported.");
        int minimumVertices = artifact.Kind == ModelCollisionArtifactKind.Convex ? 4 : 3;
        if (artifact.Vertices.Count < minimumVertices)
            throw InvalidArtifact(context, $"at least {minimumVertices} vertices are required for {artifact.Kind} content.");
        if (artifact.Kind == ModelCollisionArtifactKind.Convex && artifact.Indices.Count != 0)
            throw InvalidArtifact(context, "convex artifacts contain support vertices and must not contain triangle indices.");
        if (artifact.Kind == ModelCollisionArtifactKind.TriangleMesh &&
            (artifact.Indices.Count == 0 || artifact.Indices.Count % 3 != 0))
        {
            throw InvalidArtifact(context, "triangle-mesh indices must contain complete triangles.");
        }

        for (int index = 0; index < artifact.Vertices.Count; index++)
        {
            ModelCollisionVertex vertex = artifact.Vertices[index];
            if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
                throw InvalidArtifact(context, $"vertex {index} is not finite.");
        }

        foreach (int index in artifact.Indices)
        {
            if ((uint)index >= artifact.Vertices.Count)
                throw InvalidArtifact(context, $"index {index} is outside the vertex array.");
        }
    }

    private static InvalidDataException InvalidArtifact(string path, string message, Exception? inner = null) =>
        new($"Model collision artifact '{path}' is invalid: {message}", inner);
}
