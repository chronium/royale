using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Royale.Content.Models;

public sealed record ModelAssetManifest
{
    public const int CurrentVersion = 1;

    public int Version
    {
        get; init;
    }

    public List<ModelAssetDefinition> Assets { get; init; } = [];
}

public sealed record ModelAssetDefinition
{
    public string Id { get; init; } = string.Empty;

    public ModelRenderAssetDefinition? Render
    {
        get; init;
    }

    [JsonRequired]
    public ModelCollisionAssetDefinition Collision { get; init; } = new();
}

public sealed record ModelRenderAssetDefinition
{
    public string Source { get; init; } = string.Empty;

    public List<string> Resources { get; init; } = [];
}

public sealed record ModelCollisionAssetDefinition
{
    [JsonRequired]
    public ModelCollisionMode Mode
    {
        get; init;
    }

    public string? Source
    {
        get; init;
    }

    public string? Artifact
    {
        get; init;
    }
}

public enum ModelCollisionMode
{
    None = 0,
    Convex = 1,
    TriangleMesh = 2,
    SeparateMesh = 3,
}

public static class ModelAssetManifestLoader
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

    public static ModelAssetManifest LoadSource(string manifestPath, string sourceRoot, bool requireAssets = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);

        string fullSourceRoot = Path.GetFullPath(sourceRoot);
        ModelAssetManifest manifest = Read(manifestPath);
        Validate(manifest, manifestPath, fullSourceRoot, requireAssets, validateSourceFiles: true);
        return manifest;
    }

    public static ModelAssetManifest LoadGenerated(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        ModelAssetManifest manifest = Read(manifestPath);
        Validate(manifest, manifestPath, sourceRoot: null, requireAssets: false, validateSourceFiles: false);
        return manifest;
    }

    public static JsonSerializerOptions CreateSerializerOptions(bool writeIndented = false)
    {
        var options = new JsonSerializerOptions(JsonOptions)
        {
            WriteIndented = writeIndented,
        };
        return options;
    }

    public static string ResolveSourcePath(string sourceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ValidateRelativePath(relativePath, "asset path");

        string fullRoot = Path.GetFullPath(sourceRoot);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(fullRoot, fullPath);

        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Asset path '{relativePath}' escapes source root '{fullRoot}'.");

        return fullPath;
    }

    private static ModelAssetManifest Read(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Model asset manifest was not found at '{manifestPath}'.", manifestPath);

        try
        {
            using FileStream stream = File.OpenRead(manifestPath);
            return JsonSerializer.Deserialize<ModelAssetManifest>(stream, JsonOptions)
                ?? throw InvalidManifest(manifestPath, "the document did not contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw InvalidManifest(manifestPath, "the document is not valid strict JSON model-asset content.", exception);
        }
    }

    private static void Validate(
        ModelAssetManifest manifest,
        string manifestPath,
        string? sourceRoot,
        bool requireAssets,
        bool validateSourceFiles)
    {
        if (manifest.Version != ModelAssetManifest.CurrentVersion)
            throw InvalidManifest(manifestPath, $"version must be {ModelAssetManifest.CurrentVersion}.");

        if (requireAssets && manifest.Assets.Count == 0)
            throw InvalidManifest(manifestPath, "at least one asset is required.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ModelAssetDefinition asset in manifest.Assets)
        {
            ValidateAssetId(asset.Id, manifestPath);
            if (!ids.Add(asset.Id))
                throw InvalidManifest(manifestPath, $"asset id '{asset.Id}' must be unique.");

            if (asset.Render is null && asset.Collision.Mode == ModelCollisionMode.None)
                throw InvalidManifest(manifestPath, $"asset '{asset.Id}' must define render or collision content.");

            if (asset.Render is not null)
            {
                ValidateGlbPath(asset.Render.Source, $"asset '{asset.Id}' render source");
                ValidateSourceFile(asset.Render.Source, sourceRoot, validateSourceFiles, manifestPath, asset.Id);

                var resources = new HashSet<string>(StringComparer.Ordinal);
                foreach (string resource in asset.Render.Resources)
                {
                    ValidateRelativePath(resource, $"asset '{asset.Id}' render resource");
                    if (!resources.Add(resource))
                        throw InvalidManifest(manifestPath, $"asset '{asset.Id}' render resource '{resource}' is duplicated.");
                    ValidateSourceFile(resource, sourceRoot, validateSourceFiles, manifestPath, asset.Id);
                }

                if (validateSourceFiles)
                    ValidateExternalGlbResources(asset, sourceRoot!, manifestPath, resources);
            }

            switch (asset.Collision.Mode)
            {
                case ModelCollisionMode.None:
                case ModelCollisionMode.Convex:
                case ModelCollisionMode.TriangleMesh:
                    if (asset.Collision.Source is not null)
                        throw InvalidManifest(manifestPath, $"asset '{asset.Id}' collision source is only valid for separateMesh mode.");
                    break;
                case ModelCollisionMode.SeparateMesh:
                    if (validateSourceFiles && string.IsNullOrWhiteSpace(asset.Collision.Source))
                        throw InvalidManifest(manifestPath, $"asset '{asset.Id}' separateMesh collision requires source.");
                    if (asset.Collision.Source is not null)
                    {
                        ValidateGlbPath(asset.Collision.Source, $"asset '{asset.Id}' collision source");
                        ValidateSourceFile(asset.Collision.Source, sourceRoot, validateSourceFiles, manifestPath, asset.Id);
                    }
                    break;
                default:
                    throw InvalidManifest(manifestPath, $"asset '{asset.Id}' has unknown collision mode '{asset.Collision.Mode}'.");
            }

            if (validateSourceFiles && asset.Collision.Artifact is not null)
                throw InvalidManifest(manifestPath, $"asset '{asset.Id}' collision artifact is generated and cannot be declared in the source manifest.");

            if (!validateSourceFiles && asset.Collision.Mode != ModelCollisionMode.None && asset.Collision.Artifact is null)
                throw InvalidManifest(manifestPath, $"asset '{asset.Id}' generated collision content requires an artifact path.");

            if (asset.Collision.Artifact is not null)
                ValidateRelativePath(asset.Collision.Artifact, $"asset '{asset.Id}' collision artifact");
        }
    }

    private static void ValidateAssetId(string assetId, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(assetId))
            throw InvalidManifest(manifestPath, "asset id is required.");

        foreach (char character in assetId)
        {
            bool valid = character is >= 'a' and <= 'z' || character is >= '0' and <= '9' || character == '-';
            if (!valid)
                throw InvalidManifest(manifestPath, $"asset id '{assetId}' must contain only lowercase ASCII letters, digits, or '-'.");
        }
    }

    private static void ValidateGlbPath(string path, string field)
    {
        ValidateRelativePath(path, field);
        if (!string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{field} '{path}' must reference a GLB file.");
    }

    private static void ValidateRelativePath(string path, string field)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException($"{field} must be a non-empty relative path.");
        if (Path.IsPathRooted(path) || path.Contains('\\'))
            throw new InvalidDataException($"{field} '{path}' must use a portable relative path with '/' separators.");

        string[] segments = path.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException($"{field} '{path}' contains an invalid path segment.");
    }

    private static void ValidateSourceFile(
        string relativePath,
        string? sourceRoot,
        bool validateSourceFiles,
        string manifestPath,
        string assetId)
    {
        if (!validateSourceFiles)
            return;

        string path = ResolveSourcePath(sourceRoot!, relativePath);
        if (!File.Exists(path))
            throw InvalidManifest(manifestPath, $"asset '{assetId}' source file '{relativePath}' does not exist under '{sourceRoot}'.");
    }

    private static void ValidateExternalGlbResources(
        ModelAssetDefinition asset,
        string sourceRoot,
        string manifestPath,
        HashSet<string> declaredResources)
    {
        string renderSource = asset.Render!.Source;
        string renderPath = ResolveSourcePath(sourceRoot, renderSource);
        byte[] bytes = File.ReadAllBytes(renderPath);
        // Mesh decoding remains the runtime/collision cooker's responsibility. This pass only
        // inspects files that identify themselves as GLB 2.0 containers so legacy lightweight
        // render-only fixtures do not become GLB parser tests.
        if (bytes.Length < 20
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) != 2)
            return;

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        uint jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        uint jsonType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
        if (declaredLength != bytes.Length || jsonType != 0x4E4F534A || jsonLength > bytes.Length - 20)
            throw InvalidManifest(manifestPath, $"asset '{asset.Id}' render source '{renderSource}' has an invalid GLB container.");

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(20, checked((int)jsonLength)));
            ValidateUriArray("buffers");
            ValidateUriArray("images");

            void ValidateUriArray(string propertyName)
            {
                if (!document.RootElement.TryGetProperty(propertyName, out JsonElement values))
                    return;

                foreach (JsonElement value in values.EnumerateArray())
                {
                    if (!value.TryGetProperty("uri", out JsonElement uriElement))
                        continue;

                    string uri = uriElement.GetString()
                        ?? throw new InvalidDataException($"GLB {propertyName} URI must be a string.");
                    if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string decodedUri = Uri.UnescapeDataString(uri);
                    string sourceDirectory = Path.GetDirectoryName(renderSource.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
                    string resource = Path.GetRelativePath(
                            sourceRoot,
                            Path.GetFullPath(Path.Combine(sourceRoot, sourceDirectory, decodedUri.Replace('/', Path.DirectorySeparatorChar))))
                        .Replace(Path.DirectorySeparatorChar, '/');
                    ValidateRelativePath(resource, $"asset '{asset.Id}' external GLB resource");
                    if (!declaredResources.Contains(resource))
                    {
                        throw InvalidManifest(
                            manifestPath,
                            $"asset '{asset.Id}' render source '{renderSource}' references external resource '{uri}', but '{resource}' is not declared in render.resources.");
                    }
                }
            }
        }
        catch (JsonException exception)
        {
            throw InvalidManifest(manifestPath, $"asset '{asset.Id}' render source '{renderSource}' has an invalid GLB JSON chunk.", exception);
        }
    }

    private static InvalidDataException InvalidManifest(string path, string message, Exception? inner = null) =>
        new($"Model asset manifest '{path}' is invalid: {message}", inner);
}

public static class ModelAssetManifestSerializer
{
    public static byte[] Serialize(ModelAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string json = JsonSerializer.Serialize(
            manifest,
            ModelAssetManifestLoader.CreateSerializerOptions(writeIndented: true));
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        return new UTF8Encoding(false).GetBytes(json + "\n");
    }
}
