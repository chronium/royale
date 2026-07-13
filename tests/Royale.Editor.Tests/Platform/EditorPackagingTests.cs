using System.Xml.Linq;

namespace Royale.Editor.Tests.Platform;

public sealed class EditorPackagingTests
{
    private static string ProjectPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/Royale.Editor/Royale.Editor.csproj"));

    [Fact]
    public void EditorDependencyBoundaryExcludesGameplayAndNetworkingProjects()
    {
        XDocument document = XDocument.Load(ProjectPath);
        string[] references = document.Descendants("ProjectReference")
            .Select(element => (string)element.Attribute("Include")!)
            .ToArray();

        Assert.Contains(references, reference => reference.Contains("Royale.Platform"));
        Assert.Contains(references, reference => reference.Contains("Royale.Rendering"));
        Assert.Contains(references, reference => reference.Contains("Royale.Content"));
        Assert.Contains(references, reference => reference.Contains("Royale.Simulation"));
        Assert.Contains(references, reference => reference.Contains("Royale.Box3D"));
        Assert.DoesNotContain(
            references,
            reference =>
                reference.Contains("Royale.Client") ||
                reference.Contains("Royale.Server") ||
                reference.Contains("Royale.Protocol") ||
                reference.Contains("Royale.Network"));
    }

    [Fact]
    public void MacArmPackagingDeclaresNativeLibrariesShadersAndClientAssets()
    {
        string text = File.ReadAllText(ProjectPath);

        Assert.Contains("libSDL3.dylib", text);
        Assert.Contains("libroyale_imgui.dylib", text);
        Assert.Contains("libblurgtext.dylib", text);
        Assert.Contains("Royale.Box3D", text);
        Assert.Contains("CopyRoyaleRenderingShaders", text);
        Assert.Contains("RoyaleAssetAudience>client", text);
    }
}
