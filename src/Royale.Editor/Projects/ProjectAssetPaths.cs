namespace Royale.Editor.Projects;

public static class ProjectAssetPaths
{
    public static bool IsPortableName(string name) =>
        !string.IsNullOrEmpty(name)
        && name.All(character => character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '-' or '_');

    public static string NormalizeFolder(string relativeFolder)
    {
        relativeFolder = (relativeFolder ?? string.Empty).Replace('\\', '/').Trim('/');
        if (relativeFolder.Length == 0)
            return string.Empty;
        if (relativeFolder.Split('/').Any(segment => !IsPortableName(segment)))
            throw new InvalidDataException($"Asset folder '{relativeFolder}' must contain only lowercase ASCII letters, digits, '-' or '_'.");
        return relativeFolder;
    }

    public static string Resolve(string assetsRoot, string relativePath)
    {
        string root = Path.GetFullPath(assetsRoot);
        string full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Asset path '{relativePath}' escapes '{root}'.");
        return full;
    }
}
