using Royale.Editor.Documents;
using Royale.Editor.Persistence;
using Royale.Editor.Projects.Assets;

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
        string mapFingerprint = EditorMapPersistence.Save(
            Document,
            Project.Paths.Map,
            checkExternalChange: true,
            markDocumentSaved: false);
        LoadedRoyaleProject project = RoyaleProjectLoader.Load(Project.Paths.Root);
        string manifestFingerprint = EditorMapPersistence.Fingerprint(project.Paths.Manifest);
        string assetManifestFingerprint = EditorMapPersistence.Fingerprint(project.Paths.AssetManifest);

        Project = project;
        ManifestFingerprint = manifestFingerprint;
        AssetManifestFingerprint = assetManifestFingerprint;
        Document.MarkSaved(Project.Paths.Map, mapFingerprint);
    }

    public void ImportAssets(string destinationFolder, IReadOnlyList<PendingAssetImport> pending)
    {
        RequireUnchanged(Project.Paths.AssetManifest, AssetManifestFingerprint, "source asset manifest");
        Project = ProjectAssetImporter.Import(Project, destinationFolder, pending);
        RefreshFingerprints();
    }

    public void CreateAssetFolder(string parent, string name)
    {
        RequireUnchanged(Project.Paths.AssetManifest, AssetManifestFingerprint, "source asset manifest");
        ProjectAssetFolders.Create(Project, parent, name);
        Project = RoyaleProjectLoader.Load(Project.Paths.Root);
        RefreshFingerprints();
    }

    public void DeleteAssetFolder(string folder)
    {
        RequireUnchanged(Project.Paths.AssetManifest, AssetManifestFingerprint, "source asset manifest");
        ProjectAssetFolders.Delete(Project, folder);
        Project = RoyaleProjectLoader.Load(Project.Paths.Root);
        RefreshFingerprints();
    }

    public void MoveAssetFolder(string folder, string newParent, string? newName = null)
    {
        RequireUnchanged(Project.Paths.AssetManifest, AssetManifestFingerprint, "source asset manifest");
        Project = ProjectAssetFolders.Move(Project, folder, newParent, newName);
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
