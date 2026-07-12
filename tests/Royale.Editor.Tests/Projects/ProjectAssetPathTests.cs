using Royale.Editor.Projects;
using Royale.Editor.Projects.Assets;

namespace Royale.Editor.Tests.Projects;

public sealed class ProjectAssetPathTests
{
    [Theory]
    [InlineData("Crate Model.glb", "crate-model")]
    [InlineData("--MY__Thing 2--.glb", "my-thing-2")]
    [InlineData("é.glb", "")]
    public void AssetIdsUsePortableSlugs(string file, string expected) => Assert.Equal(expected, AssetIdSlug.FromFileName(file));

    [Theory]
    [InlineData("props", true)]
    [InlineData("props_2", true)]
    [InlineData("Props", false)]
    [InlineData("../props", false)]
    public void FolderNamesArePortable(string name, bool expected) => Assert.Equal(expected, ProjectAssetPaths.IsPortableName(name));
}
