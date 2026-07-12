using Royale.Content.Maps;
using Royale.Content.Models;

namespace Royale.Editor.Projects;

public sealed record LoadedRoyaleProject(
    RoyaleProjectManifest Manifest,
    RoyaleProjectPaths Paths,
    GameMap Map,
    ModelAssetManifest AssetManifest);
