using Royale.AssetPipeline.Processing;
using Royale.Editor.Documents;
using Royale.Editor.Projects;
using Royale.Simulation.World;

namespace Royale.Editor.Viewport.FaceSnap;

public static class EditorFaceSnapCollisionWorldFactory
{
    public static MapStaticCollisionWorld Create(
        EditorMapDocument document,
        EditorProjectSession? projectSession)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (projectSession is null)
            return MapStaticCollisionWorld.Create(document.Map);

        RoyaleProjectPaths paths = projectSession.Project.Paths;
        AssetPipelineProcessor.Build(
            paths.AssetManifest,
            paths.Sources,
            paths.GeneratedServer,
            AssetPipelineAudience.Server,
            requireAssets: false);
        return MapStaticCollisionWorld.Create(document.Map, new DirectoryInfo(paths.GeneratedServer));
    }
}
