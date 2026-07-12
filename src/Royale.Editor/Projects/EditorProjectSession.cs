using Royale.Editor.Documents;
using Royale.Editor.Persistence;

namespace Royale.Editor.Projects;

public sealed class EditorProjectSession
{
    private EditorProjectSession(LoadedRoyaleProject project)
    {
        Project = project;
        Document = new EditorMapDocument(
            project.Map,
            project.Paths.Map,
            EditorMapPersistence.Fingerprint(project.Paths.Map),
            requiresSaveAs: false);
        RefreshFingerprints();
    }

    public LoadedRoyaleProject Project { get; private set; }
    public EditorMapDocument Document { get; }
    public string ManifestFingerprint { get; private set; } = string.Empty;
    public string AssetManifestFingerprint { get; private set; } = string.Empty;

    public static EditorProjectSession Load(string root) => new(RoyaleProjectLoader.Load(root));

    public void Save()
    {
        RequireUnchanged(Project.Paths.Manifest, ManifestFingerprint, "project manifest");
        RequireUnchanged(Project.Paths.AssetManifest, AssetManifestFingerprint, "source asset manifest");
        EditorMapPersistence.Save(Document, Project.Paths.Map, checkExternalChange: true);
        Project = RoyaleProjectLoader.Load(Project.Paths.Root);
        RefreshFingerprints();
    }

    private void RefreshFingerprints()
    {
        ManifestFingerprint = EditorMapPersistence.Fingerprint(Project.Paths.Manifest);
        AssetManifestFingerprint = EditorMapPersistence.Fingerprint(Project.Paths.AssetManifest);
    }

    private static void RequireUnchanged(string path, string expected, string kind)
    {
        if (!File.Exists(path) || !string.Equals(EditorMapPersistence.Fingerprint(path), expected, StringComparison.Ordinal))
            throw new IOException($"Project {kind} '{path}' changed externally; save was cancelled.");
    }
}
