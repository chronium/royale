using System.Numerics;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;
using Royale.Rendering.UI;
using SDL;

namespace Royale.Rendering.Tests.Rendering;

public sealed class RenderingExtractionTests
{
    [Fact]
    public void UnitBoxGeometryHasStableResourceIdentity()
    {
        Assert.Same(UnitBoxMesh.Create(), UnitBoxMesh.Create());
    }

    [Fact]
    public void ResourceKeysRemainStableWhenOnlyInstancesChange()
    {
        StaticMeshGeometry geometry = UnitBoxMesh.Create();
        var first = new StaticMeshResourceKey(geometry, StaticMeshMaterial.GrayBox);
        var second = new StaticMeshResourceKey(geometry, StaticMeshMaterial.GrayBox);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RenderFramesCanReplaceScenesAndTransforms()
    {
        var firstScene = new StaticMeshScene([new StaticMeshInstance(Matrix4x4.Identity)], []);
        var translated = Matrix4x4.CreateTranslation(5.0f, 0.0f, 0.0f);
        var secondScene = new StaticMeshScene([new StaticMeshInstance(translated)], []);
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);

        var first = new RenderFrame(camera, firstScene, RenderViewMode.Normal);
        var second = first with { StaticScene = secondScene };

        Assert.NotSame(first.StaticScene, second.StaticScene);
        Assert.Equal(translated, second.StaticScene.UnitBoxInstances[0].Transform);
    }

    [Fact]
    public void BgraReadbackIsNormalizedToRgba()
    {
        byte[] rgba = GpuImageReadback.NormalizeToRgba([10, 20, 30, 255], SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM);
        Assert.Equal([30, 20, 10, 255], rgba);
    }

    [Fact]
    public void RgbaReadbackIsPreserved()
    {
        byte[] rgba = GpuImageReadback.NormalizeToRgba([10, 20, 30, 255], SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM);
        Assert.Equal([10, 20, 30, 255], rgba);
    }

    [Fact]
    public void ImGuiDockingDefaultsOffAndCaptureStateKeepsBothChannels()
    {
        Assert.False(default(SdlGpuImGuiSettings).EnableDocking);
        Assert.True(new SdlGpuImGuiSettings(EnableDocking: true).EnableDocking);
        Assert.Equal(new ImGuiCaptureState(true, false), new ImGuiCaptureState(true, false));
    }
}
