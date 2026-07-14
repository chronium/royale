using System.Numerics;
using Royale.Platform.Desktop;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using Royale.Rendering.Text;
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
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!request.TryComplete(out image) && DateTime.UtcNow < deadline)
            Thread.Sleep(1);
        GpuImageReadback completed = image
            ?? throw new InvalidOperationException("asynchronous readback did not complete within the polling budget.");

        using SdlGpuSampledTexture texture = device.UploadRgbaTexture(completed.RgbaBytes, completed.Width, completed.Height);
        Require(texture.NativeTextureHandle != 0, "sampled texture upload returned a null native handle.");
        Require(texture.Width == target.Width && texture.Height == target.Height, "sampled texture dimensions did not match the readback.");
        Console.WriteLine($"GPU_HARNESS_PASS async-readback-upload {completed.Width}x{completed.Height}");
    }

    private static void VerifyKenneyContactSheetViews(SdlGpuDevice device, SdlGpuOffscreenTarget target)
    {
        StaticMeshAsset asset = StaticMeshAssetCache.Load(AppContext.BaseDirectory).GetRequired("kenney-crate");
        ModelBounds bounds = ModelThumbnailFraming.CalculateBounds(asset);
        StaticMeshScene scene = ModelThumbnailFraming.CreateScene(asset);
        target.Resize(ModelContactSheetFraming.TileSize, ModelContactSheetFraming.TileSize);
        var fingerprints = new HashSet<ulong>();

        foreach (ModelContactSheetView view in ModelContactSheetFraming.AxisViews.Concat(ModelContactSheetFraming.DiagonalViews))
        {
            var frame = new RenderFrame(
                ModelContactSheetFraming.CreateCamera(bounds, view),
                scene,
                RenderViewMode.Normal,
                ClearColor: new SDL_FColor { r = 0.18f, g = 0.18f, b = 0.18f, a = 1.0f },
                ScreenText: [ScreenTextLabel.Create(view.Label, new Vector2(16.0f, 12.0f))],
                ShowSmokeLabel: false);
            using GpuImageReadbackRequest request = device.BeginOffscreenReadback(target, frame);
            GpuImageReadback image = WaitForReadback(request, $"contact-sheet view {view.Label}");
            VerifyFramedNonblankTile(image, view.Label);
            fingerprints.Add(Fingerprint(image.RgbaBytes));
        }

        Require(fingerprints.Count >= 10, $"contact-sheet views produced only {fingerprints.Count} materially distinct tiles.");
        Console.WriteLine($"GPU_HARNESS_PASS kenney-contact-sheet views=14 distinct={fingerprints.Count}");
    }

    private static GpuImageReadback WaitForReadback(GpuImageReadbackRequest request, string description)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        GpuImageReadback? image;
        while (!request.TryComplete(out image) && DateTime.UtcNow < deadline)
            Thread.Sleep(1);
        return image ?? throw new InvalidOperationException($"{description} did not complete within 10 seconds.");
    }

    private static void VerifyFramedNonblankTile(GpuImageReadback image, string label)
    {
        const byte clear = 46;
        int changed = 0;
        int transparent = 0;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            int offset = (y * image.Width + x) * 4;
            if (image.RgbaBytes[offset + 3] == 0)
                transparent++;
            bool differs = Math.Abs(image.RgbaBytes[offset] - clear) > 2 ||
                Math.Abs(image.RgbaBytes[offset + 1] - clear) > 2 ||
                Math.Abs(image.RgbaBytes[offset + 2] - clear) > 2;
            if (!differs)
                continue;
            changed++;
            Require(x > 3 && x < image.Width - 4 && y > 3 && y < image.Height - 4,
                $"contact-sheet view {label} was clipped against the tile edge at {x},{y}.");
        }
        Require(transparent == 0, $"contact-sheet view {label} contained {transparent} transparent pixels.");
        Require(changed > 500, $"contact-sheet view {label} was blank or contained only a label ({changed} changed pixels).");
    }

    private static ulong Fingerprint(byte[] bytes)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        ulong hash = offsetBasis;
        foreach (byte value in bytes)
        {
            hash ^= value;
            hash *= prime;
        }
        return hash;
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
            VerifyKenneyContactSheetViews(device, target);
            host.RequestExit();
        }

        public void ProcessEvent(in SDL_Event sdlEvent) { }
        public void Update(SdlFrameTime time) { }
        public void FixedUpdate(SdlFixedTickTime time) { }
        public void Render(SdlFrameTime time) { }
    }
}
