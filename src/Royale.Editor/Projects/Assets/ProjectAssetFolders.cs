using Royale.AssetPipeline.Processing;
using Royale.Content.Models;

namespace Royale.Editor.Projects.Assets;

public static class ProjectAssetFolders
{
    public static void Create(LoadedRoyaleProject project, string parent, string name)
    {
        parent = ProjectAssetPaths.NormalizeFolder(parent);
        if (!ProjectAssetPaths.IsPortableName(name)) throw new InvalidDataException($"Folder name '{name}' is not portable.");
        string path = ProjectAssetPaths.Resolve(project.Paths.Sources, Join(parent, name));
        if (Directory.Exists(path) || File.Exists(path)) throw new IOException($"Asset path '{Join(parent, name)}' already exists.");
        Directory.CreateDirectory(path);
    }

    public static void Delete(LoadedRoyaleProject project, string folder)
    {
        folder = RequireFolder(folder);
        string path = ProjectAssetPaths.Resolve(project.Paths.Sources, folder);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        if (Directory.EnumerateFileSystemEntries(path).Any()) throw new IOException($"Asset folder '{folder}' is not empty.");
        if (References(project.AssetManifest).Any(reference => IsAtOrBelow(reference, folder)))
            throw new IOException($"Asset folder '{folder}' is referenced by the manifest.");
        Directory.Delete(path);
    }

    public static LoadedRoyaleProject Move(LoadedRoyaleProject project, string folder, string newParent, string? newName = null)
    {
        folder = RequireFolder(folder);
        newParent = ProjectAssetPaths.NormalizeFolder(newParent);
        string name = newName ?? Path.GetFileName(folder);
        if (!ProjectAssetPaths.IsPortableName(name)) throw new InvalidDataException($"Folder name '{name}' is not portable.");
        string destination = Join(newParent, name);
        if (IsAtOrBelow(destination, folder)) throw new IOException("An asset folder cannot be moved into itself.");
        string sourcePath = ProjectAssetPaths.Resolve(project.Paths.Sources, folder);
        string destinationPath = ProjectAssetPaths.Resolve(project.Paths.Sources, destination);
        if (!Directory.Exists(sourcePath)) throw new DirectoryNotFoundException(sourcePath);
        if (Directory.Exists(destinationPath) || File.Exists(destinationPath)) throw new IOException($"Asset path '{destination}' already exists.");

        ModelAssetManifest rewritten = project.AssetManifest with
        {
            Assets = project.AssetManifest.Assets.Select(asset => asset with
            {
                Render = asset.Render is null ? null : asset.Render with
                {
                    Source = Rewrite(asset.Render.Source, folder, destination),
                    Resources = asset.Render.Resources.Select(path => Rewrite(path, folder, destination)).ToList(),
                },
                Collision = asset.Collision with { Source = asset.Collision.Source is null ? null : Rewrite(asset.Collision.Source, folder, destination) },
            }).ToList(),
        };

        string stage = Path.Combine(project.Paths.Root, ".asset-stage-" + Guid.NewGuid().ToString("N"));
        string stagedSources = Path.Combine(stage, "assets");
        string stagedClient = Path.Combine(stage, "client");
        string stagedServer = Path.Combine(stage, "server");
        ProjectAssetTransaction.CopyDirectory(project.Paths.Sources, stagedSources);
        string stagedSourcePath = ProjectAssetPaths.Resolve(stagedSources, folder);
        string stagedDestinationPath = ProjectAssetPaths.Resolve(stagedSources, destination);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedDestinationPath)!);
        Directory.Move(stagedSourcePath, stagedDestinationPath);
        string stagedManifest = Path.Combine(stagedSources, RoyaleProjectLayout.AssetManifestFileName);
        File.WriteAllBytes(stagedManifest, ModelAssetManifestSerializer.Serialize(rewritten));
        AssetPipelineProcessor.Build(stagedManifest, stagedSources, stagedClient, AssetPipelineAudience.Client);
        AssetPipelineProcessor.Build(stagedManifest, stagedSources, stagedServer, AssetPipelineAudience.Server);
        ProjectAssetTransaction.Commit(project.Paths, stage);
        return RoyaleProjectLoader.Load(project.Paths.Root);
    }

    private static IEnumerable<string> References(ModelAssetManifest manifest) => manifest.Assets.SelectMany(asset =>
        (asset.Render is null ? [] : new[] { asset.Render.Source }.Concat(asset.Render.Resources))
        .Concat(asset.Collision.Source is null ? [] : [asset.Collision.Source]));
    private static string Rewrite(string path, string oldFolder, string newFolder) => IsAtOrBelow(path, oldFolder) ? newFolder + path[oldFolder.Length..] : path;
    private static bool IsAtOrBelow(string path, string folder) => path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal);
    private static string RequireFolder(string folder) { folder = ProjectAssetPaths.NormalizeFolder(folder); return folder.Length == 0 ? throw new InvalidDataException("The assets root cannot be changed.") : folder; }
    private static string Join(string left, string right) => left.Length == 0 ? right : $"{left}/{right}";
}
