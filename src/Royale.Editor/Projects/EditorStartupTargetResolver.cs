using Royale.Editor.Documents;
using Royale.Editor.Launch;

namespace Royale.Editor.Projects;

public enum EditorStartupTargetKind { Project, Map }

public sealed record EditorStartupTarget(EditorStartupTargetKind Kind, string Path, bool RequiresSaveAs, string? Warning = null);

public static class EditorStartupTargetResolver
{
    public static EditorStartupTarget Resolve(
        EditorLaunchOptions options,
        RecentProjectStore recent,
        string currentDirectory,
        string baseDirectory)
    {
        if (options.ProjectPath is not null)
            return new(EditorStartupTargetKind.Project, Path.GetFullPath(options.ProjectPath), false);
        if (options.MapFilePath is not null)
            return new(EditorStartupTargetKind.Map, Path.GetFullPath(options.MapFilePath), false);

        if (options.ExplicitMap)
        {
            EditorMapSource source = EditorMapSourceResolver.Resolve(options.MapId, null, currentDirectory, baseDirectory);
            return new(EditorStartupTargetKind.Map, source.Path, source.RequiresSaveAs);
        }

        string? recentPath;
        try
        {
            recentPath = recent.Read();
        }
        catch (Exception exception)
        {
            EditorMapSource fallback = EditorMapSourceResolver.Resolve(options.MapId, null, currentDirectory, baseDirectory);
            return new(EditorStartupTargetKind.Map, fallback.Path, fallback.RequiresSaveAs, $"Recent project state failed to load: {exception.Message}");
        }

        if (recentPath is not null)
        {
            try
            {
                RoyaleProjectLoader.Load(recentPath);
                return new(EditorStartupTargetKind.Project, recentPath, false);
            }
            catch (Exception exception)
            {
                EditorMapSource fallback = EditorMapSourceResolver.Resolve(options.MapId, null, currentDirectory, baseDirectory);
                return new(EditorStartupTargetKind.Map, fallback.Path, fallback.RequiresSaveAs, $"Recent project failed to load: {exception.Message}");
            }
        }

        EditorMapSource sourceDefault = EditorMapSourceResolver.Resolve(options.MapId, null, currentDirectory, baseDirectory);
        return new(EditorStartupTargetKind.Map, sourceDefault.Path, sourceDefault.RequiresSaveAs);
    }
}
