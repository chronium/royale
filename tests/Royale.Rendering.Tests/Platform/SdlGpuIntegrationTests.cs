using System.Numerics;
using Royale.Platform.Desktop;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using SDL;

namespace Royale.Rendering.Tests.Platform;

public sealed class SdlGpuIntegrationTests
{
    [Fact]
    public void HiddenWindowRendersAndResizesOffscreenTargetWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ROYALE_GPU_TESTS"), "1", StringComparison.Ordinal))
            return;

        using var host = new SdlDesktopHost(
            new SdlWindowSettings("Royale GPU test", 64, 64, SDL_WindowFlags.SDL_WINDOW_HIDDEN),
            new SdlLoopSettings(1.0 / 60.0, 1, 0));
        var application = new OffscreenTestApplication();
        host.Run(application);
        application.Dispose();
    }

    private sealed class OffscreenTestApplication : ISdlDesktopApplication, IDisposable
    {
        private SdlDesktopHost? host;
        private SdlGpuDevice? device;
        private SdlGpuOffscreenTarget? target;
        private int frame;

        public void Initialize(SdlDesktopHost initializedHost)
        {
            host = initializedHost;
            device = SdlGpuDevice.Create(initializedHost.Window!);
            target = device.CreateOffscreenTarget(32, 24);
        }

        public void Render(SdlFrameTime time)
        {
            int expectedWidth = frame == 0 ? 32 : 17;
            int expectedHeight = frame == 0 ? 24 : 13;
            if (frame == 1)
                target!.Resize(expectedWidth, expectedHeight);

            var scene = new StaticMeshScene([new StaticMeshInstance(Matrix4x4.Identity)], []);
            var renderFrame = new RenderFrame(
                new RenderCamera(new Vector3(0.0f, 0.0f, 3.0f), MathF.PI, 0.0f),
                scene,
                RenderViewMode.Normal,
                ClearColor: new SDL_FColor { r = 0.2f, g = 0.1f, b = 0.05f, a = 1.0f });
            GpuImageReadback image = device!.RenderOffscreen(target!, renderFrame, readback: true)!;

            Assert.Equal(expectedWidth, image.Width);
            Assert.Equal(expectedHeight, image.Height);
            Assert.Contains(image.RgbaBytes, value => value != 0);

            frame++;
            if (frame == 2)
                host!.RequestExit();
        }

        public void ProcessEvent(in SDL_Event sdlEvent) { }
        public void Update(SdlFrameTime time) { }
        public void FixedUpdate(SdlFixedTickTime time) { }

        public void Dispose()
        {
            target?.Dispose();
            device?.Dispose();
        }
    }
}
