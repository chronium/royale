using Royale.Content.Models;
using Royale.Editor.Projects;

namespace Royale.Editor.Workspace.Assets;

public enum ProjectAssetNodeKind { Folder, RegisteredModel, File }

public sealed record ProjectAssetNode(
    string Name,
    string RelativePath,
    ProjectAssetNodeKind Kind,
    string? AssetId,
    IReadOnlyList<ProjectAssetNode> Children);

public static class ProjectAssetTree
{
    public static ProjectAssetNode Scan(string assetsRoot, ModelAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string root = Path.GetFullPath(assetsRoot);
        var models = manifest.Assets
            .Where(asset => asset.Render is not null)
            .ToDictionary(asset => asset.Render!.Source, asset => asset.Id, StringComparer.Ordinal);
        return ScanDirectory(root, root, models);
    }

    public static IReadOnlyList<ProjectAssetNode> Search(ProjectAssetNode root, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];
        return Flatten(root).Where(node => node != root
            && (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || node.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (node.AssetId?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))).ToList();
    }

    public static IReadOnlyList<string> Breadcrumbs(string relativeFolder)
    {
        string normalized = relativeFolder.Replace('\\', '/').Trim('/');
        var result = new List<string> { string.Empty };
        if (normalized.Length == 0)
            return result;
        string current = string.Empty;
        foreach (string segment in normalized.Split('/'))
        {
            current = current.Length == 0 ? segment : $"{current}/{segment}";
            result.Add(current);
        }
        return result;
    }

    private static ProjectAssetNode ScanDirectory(string root, string directory, IReadOnlyDictionary<string, string> models)
    {
        var children = new List<ProjectAssetNode>();
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                continue;
            string relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            if ((attributes & FileAttributes.Directory) != 0)
                children.Add(ScanDirectory(root, path, models));
            else
                children.Add(new ProjectAssetNode(
                    Path.GetFileName(path), relative,
                    models.TryGetValue(relative, out string? id) ? ProjectAssetNodeKind.RegisteredModel : ProjectAssetNodeKind.File,
                    id, []));
        }
        children.Sort((left, right) =>
        {
            int kind = (left.Kind == ProjectAssetNodeKind.Folder ? 0 : 1).CompareTo(right.Kind == ProjectAssetNodeKind.Folder ? 0 : 1);
            return kind != 0 ? kind : StringComparer.Ordinal.Compare(left.Name, right.Name);
        });
        string relativeDirectory = Path.GetRelativePath(root, directory).Replace(Path.DirectorySeparatorChar, '/');
        if (relativeDirectory == ".") relativeDirectory = string.Empty;
        return new ProjectAssetNode(Path.GetFileName(directory), relativeDirectory, ProjectAssetNodeKind.Folder, null, children);
    }

    private static IEnumerable<ProjectAssetNode> Flatten(ProjectAssetNode node)
    {
        yield return node;
        foreach (ProjectAssetNode child in node.Children)
            foreach (ProjectAssetNode descendant in Flatten(child))
                yield return descendant;
    }
}
