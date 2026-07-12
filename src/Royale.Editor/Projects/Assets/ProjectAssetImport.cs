using Royale.AssetPipeline.Processing;
using Royale.Content.Models;

namespace Royale.Editor.Projects.Assets;

public sealed record PendingAssetImport(
    string SourcePath,
    string AssetId,
    bool Include = true,
    ModelCollisionMode CollisionMode = ModelCollisionMode.Convex,
    string? SeparateCollisionPath = null);

public sealed record AssetImportDiagnostic(int Row, string Message);

public static class AssetIdSlug
{
    public static string FromFileName(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var result = new System.Text.StringBuilder(stem.Length);
        bool separator = false;
        foreach (char character in stem)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (separator && result.Length > 0) result.Append('-');
                result.Append(character);
                separator = false;
            }
            else separator = true;
        }
        return result.ToString();
    }
}

public static class ProjectAssetImporter
{
    public static void Recover(RoyaleProjectPaths paths) => ProjectAssetTransaction.Recover(paths);

    public static LoadedRoyaleProject Import(
        LoadedRoyaleProject project,
        string destinationFolder,
        IReadOnlyList<PendingAssetImport> pending)
    {
        destinationFolder = ProjectAssetPaths.NormalizeFolder(destinationFolder);
        List<PendingAssetImport> included = pending.Where(row => row.Include).ToList();
        if (included.Count == 0) throw new InvalidDataException("At least one asset must be included.");
        Validate(project, destinationFolder, included);

        string transactionId = Guid.NewGuid().ToString("N");
        string stagingRoot = Path.Combine(project.Paths.Root, $".asset-stage-{transactionId}");
        string stagedSources = Path.Combine(stagingRoot, "assets");
        string stagedManifest = Path.Combine(stagedSources, RoyaleProjectLayout.AssetManifestFileName);
        string stagedClient = Path.Combine(stagingRoot, "client");
        string stagedServer = Path.Combine(stagingRoot, "server");
        Directory.CreateDirectory(stagedSources);
        ProjectAssetTransaction.CopyDirectory(project.Paths.Sources, stagedSources);

        var assets = project.AssetManifest.Assets.ToList();
        foreach (PendingAssetImport row in included)
        {
            string file = Path.GetFileName(row.SourcePath);
            string sourceRelative = Join(destinationFolder, file);
            CopyModelAndResources(row.SourcePath, Path.Combine(stagedSources, destinationFolder.Replace('/', Path.DirectorySeparatorChar)));
            string? collisionRelative = null;
            if (row.CollisionMode == ModelCollisionMode.SeparateMesh)
            {
                collisionRelative = Join(destinationFolder, Path.GetFileName(row.SeparateCollisionPath!));
                string collisionTarget = ProjectAssetPaths.Resolve(stagedSources, collisionRelative);
                if (!File.Exists(collisionTarget))
                    File.Copy(row.SeparateCollisionPath!, collisionTarget);
            }
            IReadOnlyList<string> uris = GlbExternalResourceInspector.Inspect(row.SourcePath);
            string sourceDirectory = Path.GetDirectoryName(sourceRelative)?.Replace('\\', '/') ?? string.Empty;
            assets.Add(new ModelAssetDefinition
            {
                Id = row.AssetId,
                Render = new ModelRenderAssetDefinition { Source = sourceRelative, Resources = uris.Select(uri => Join(sourceDirectory, uri)).ToList() },
                Collision = new ModelCollisionAssetDefinition { Mode = row.CollisionMode, Source = collisionRelative },
            });
        }
        File.WriteAllBytes(stagedManifest, ModelAssetManifestSerializer.Serialize(new ModelAssetManifest { Version = ModelAssetManifest.CurrentVersion, Assets = assets }));
        AssetPipelineProcessor.Build(stagedManifest, stagedSources, stagedClient, AssetPipelineAudience.Client);
        AssetPipelineProcessor.Build(stagedManifest, stagedSources, stagedServer, AssetPipelineAudience.Server);

        ProjectAssetTransaction.Commit(project.Paths, stagingRoot);
        return RoyaleProjectLoader.Load(project.Paths.Root);
    }

    private static void Validate(LoadedRoyaleProject project, string folder, IReadOnlyList<PendingAssetImport> rows)
    {
        var ids = project.AssetManifest.Assets.Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);
        var destinations = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (PendingAssetImport row in rows)
        {
            if (!File.Exists(row.SourcePath) || !string.Equals(Path.GetExtension(row.SourcePath), ".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Import source '{row.SourcePath}' must be an existing GLB file.");
            if (AssetIdSlug.FromFileName(row.AssetId) != row.AssetId || row.AssetId.Length == 0 || !ids.Add(row.AssetId))
                throw new InvalidDataException($"Asset id '{row.AssetId}' is invalid or duplicated.");
            if (!Enum.IsDefined(row.CollisionMode)) throw new InvalidDataException($"Collision mode '{row.CollisionMode}' is unsupported.");
            if (row.CollisionMode == ModelCollisionMode.SeparateMesh)
            {
                if (row.SeparateCollisionPath is null || !File.Exists(row.SeparateCollisionPath)
                    || !string.Equals(Path.GetExtension(row.SeparateCollisionPath), ".glb", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Asset '{row.AssetId}' requires a separate collision GLB.");
                GlbExternalResourceInspector.Inspect(row.SeparateCollisionPath);
                ValidateDestination(Join(folder, Path.GetFileName(row.SeparateCollisionPath)), row.SeparateCollisionPath);
            }
            ValidateDestination(Join(folder, Path.GetFileName(row.SourcePath)), row.SourcePath);
            foreach (string uri in GlbExternalResourceInspector.Inspect(row.SourcePath))
                ValidateDestination(Join(folder, uri), Path.GetFullPath(Path.Combine(Path.GetDirectoryName(row.SourcePath)!, uri.Replace('/', Path.DirectorySeparatorChar))));
        }

        void ValidateDestination(string relative, string source)
        {
            if (!File.Exists(source)) throw new FileNotFoundException($"Referenced import resource was not found at '{source}'.", source);
            string target = ProjectAssetPaths.Resolve(project.Paths.Sources, relative);
            if (File.Exists(target)) throw new IOException($"Import destination '{relative}' already exists.");
            byte[] bytes = File.ReadAllBytes(source);
            if (destinations.TryGetValue(relative, out byte[]? existing) && !existing.AsSpan().SequenceEqual(bytes))
                throw new IOException($"Import destination '{relative}' has conflicting source bytes.");
            destinations[relative] = bytes;
        }
    }

    private static void CopyModelAndResources(string model, string destination)
    {
        Directory.CreateDirectory(destination);
        File.Copy(model, Path.Combine(destination, Path.GetFileName(model)));
        foreach (string uri in GlbExternalResourceInspector.Inspect(model))
        {
            string source = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(model)!, uri.Replace('/', Path.DirectorySeparatorChar)));
            string target = Path.Combine(destination, uri.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target)) File.Copy(source, target);
            else if (!File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(source)))
                throw new IOException($"Shared resource '{uri}' differs between imports.");
        }
    }

    private static string Join(string folder, string name) => folder.Length == 0 ? name.Replace('\\', '/') : $"{folder}/{name.Replace('\\', '/')}";
}
