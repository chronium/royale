using System.Numerics;
using Royale.Platform.Desktop;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using SDL;

namespace Royale.Rendering.GpuHarness;

public static class Program
{
    private static readonly SDL_FColor ClearColor = new() { r = 0.125f, g = 0.25f, b = 0.75f, a = 1.0f };

    public static int Main()
    {
        Console.WriteLine("GPU_HARNESS_START");

        try
        {
            Run();
            Console.WriteLine("GPU_HARNESS_SUCCESS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"GPU_HARNESS_FAILURE: {exception}");
            return 1;
        }
    }

    private static void Run()
    {
        using var host = new SdlDesktopHost(
            new SdlWindowSettings("Royale GPU harness", 128, 96, SDL_WindowFlags.SDL_WINDOW_HIDDEN),
            new SdlLoopSettings(1.0 / 60.0, 1, 0));
        host.Run(new HarnessApplication());
    }

    private static void RenderAndVerify(
        SdlGpuDevice device,
        SdlGpuOffscreenTarget target,
        string passName,
        int expectedWidth,
        int expectedHeight)
    {
        var scene = new StaticMeshScene(
            [new StaticMeshInstance(Matrix4x4.CreateRotationY(0.35f), "gpu-harness-box")],
            []);
        var frame = new RenderFrame(
            new RenderCamera(new Vector3(0.0f, 0.0f, 3.0f), 0.0f, 0.0f),
            scene,
            RenderViewMode.Normal,
            ClearColor: ClearColor);
        GpuImageReadback image = device.RenderOffscreen(target, frame, readback: true)
            ?? throw new InvalidOperationException("Offscreen rendering did not return a readback image.");

        Require(image.Width == expectedWidth, $"{passName}: expected width {expectedWidth}, got {image.Width}.");
        Require(image.Height == expectedHeight, $"{passName}: expected height {expectedHeight}, got {image.Height}.");
        Require(
            image.RgbaBytes.Length == checked(expectedWidth * expectedHeight * 4),
            $"{passName}: expected {expectedWidth * expectedHeight * 4} RGBA bytes, got {image.RgbaBytes.Length}.");

        byte clearRed = ToByte(ClearColor.r);
        byte clearGreen = ToByte(ClearColor.g);
        byte clearBlue = ToByte(ClearColor.b);
        bool foundRenderedPixel = false;

        for (int offset = 0; offset < image.RgbaBytes.Length; offset += 4)
        {
            Require(image.RgbaBytes[offset + 3] == byte.MaxValue, $"{passName}: pixel alpha was not opaque at byte offset {offset}.");
            if (image.RgbaBytes[offset] != clearRed ||
                image.RgbaBytes[offset + 1] != clearGreen ||
                image.RgbaBytes[offset + 2] != clearBlue)
            {
                foundRenderedPixel = true;
            }
        }

        Require(foundRenderedPixel, $"{passName}: every pixel matched the clear color; indexed box rendering was not visible.");
        Console.WriteLine($"GPU_HARNESS_PASS {passName} {image.Width}x{image.Height} rgba={image.RgbaBytes.Length}");
    }

    private static void VerifyAsyncReadbackAndUpload(SdlGpuDevice device, SdlGpuOffscreenTarget target)
    {
        var scene = new StaticMeshScene([new StaticMeshInstance(Matrix4x4.Identity, "async-box")], []);
        var frame = new RenderFrame(
            new RenderCamera(new Vector3(0.0f, 0.0f, 3.0f), 0.0f, 0.0f),
            scene,
            RenderViewMode.Normal,
            ClearColor: ClearColor);
        using GpuImageReadbackRequest request = device.BeginOffscreenReadback(target, frame);
        GpuImageReadback? image = null;
        for (int poll = 0; poll < 10_000 && !request.TryComplete(out image); poll++)
            Thread.Yield();
        GpuImageReadback completed = image
            ?? throw new InvalidOperationException("asynchronous readback did not complete within the polling budget.");

        using SdlGpuSampledTexture texture = device.UploadRgbaTexture(completed.RgbaBytes, completed.Width, completed.Height);
        Require(texture.NativeTextureHandle != 0, "sampled texture upload returned a null native handle.");
        Require(texture.Width == target.Width && texture.Height == target.Height, "sampled texture dimensions did not match the readback.");
        Console.WriteLine($"GPU_HARNESS_PASS async-readback-upload {completed.Width}x{completed.Height}");
    }

    private static byte ToByte(float value) => (byte)MathF.Round(Math.Clamp(value, 0.0f, 1.0f) * byte.MaxValue);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class HarnessApplication : ISdlDesktopApplication
    {
        public void Initialize(SdlDesktopHost host)
        {
            using SdlGpuDevice device = SdlGpuDevice.Create(host.Window!);
            using SdlGpuOffscreenTarget target = device.CreateOffscreenTarget(128, 96);

            RenderAndVerify(device, target, "initial", 128, 96);
            target.Resize(79, 61);
            RenderAndVerify(device, target, "resized", 79, 61);
            VerifyAsyncReadbackAndUpload(device, target);
            host.RequestExit();
        }

        public void ProcessEvent(in SDL_Event sdlEvent) { }
        public void Update(SdlFrameTime time) { }
        public void FixedUpdate(SdlFixedTickTime time) { }
        public void Render(SdlFrameTime time) { }
    }
}
