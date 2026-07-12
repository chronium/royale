using Royale.Content;

namespace Royale.Editor.Documents;

public readonly record struct EditorMapSource(string Path, bool RequiresSaveAs);

public static class EditorMapSourceResolver
{
    public static EditorMapSource Resolve(string mapId, string? explicitPath, string workingDirectory, string packagedRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath)) return new(Path.GetFullPath(explicitPath, workingDirectory), false);
        DirectoryInfo? directory = new(Path.GetFullPath(workingDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Royale.slnx")))
            {
                string source = Path.Combine(directory.FullName, "src", "Royale.Content", "Maps", mapId + ".json");
                if (File.Exists(source)) return new(source, false);
                break;
            }
            directory = directory.Parent;
        }
        return new(Path.Combine(packagedRoot, ContentCatalog.MapDirectoryName, mapId + ".json"), true);
    }
}
