using System.Xml.Linq;
namespace Royale.Editor.Tests.Platform;
public sealed class EditorPackagingTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Royale.Editor/Royale.Editor.csproj"));
    [Fact] public void EditorDependencyBoundaryExcludesGameplayAndNetworkingProjects()
    {
        XDocument document=XDocument.Load(ProjectPath); string[] references=document.Descendants("ProjectReference").Select(x=>(string)x.Attribute("Include")!).ToArray();
        Assert.Contains(references,x=>x.Contains("Royale.Platform")); Assert.Contains(references,x=>x.Contains("Royale.Rendering")); Assert.Contains(references,x=>x.Contains("Royale.Content"));
        Assert.DoesNotContain(references,x=>x.Contains("Royale.Client")||x.Contains("Royale.Server")||x.Contains("Royale.Protocol")||x.Contains("Royale.Network"));
    }
    [Fact] public void MacArmPackagingDeclaresNativeLibrariesShadersAndClientAssets()
    {
        string text=File.ReadAllText(ProjectPath); Assert.Contains("libSDL3.dylib",text); Assert.Contains("libroyale_imgui.dylib",text); Assert.Contains("libblurgtext.dylib",text); Assert.Contains("CopyRoyaleRenderingShaders",text); Assert.Contains("RoyaleAssetAudience>client",text);
    }
}
