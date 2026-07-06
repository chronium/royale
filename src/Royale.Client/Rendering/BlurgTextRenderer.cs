using System.Numerics;
using System.Runtime.InteropServices;
using BlurgText;
using Royale.Native;
using SDL;
using static SDL.SDL3;

namespace Royale.Client.Rendering;

internal sealed unsafe class BlurgTextRenderer : IDisposable
{
    private static readonly string[] DefaultFontFamilies =
    [
        "SF Pro",
        "DejaVu Sans",
        "Noto Sans",
        "Liberation Sans",
        "Segoe UI",
        "Arial",
    ];

    private readonly SDL_GPUDevice* device;
    private readonly Blurg blurg;
    private readonly BlurgFont defaultFont;
    private readonly TextQuadRenderer quadRenderer;
    private readonly List<TextQuadSource> queuedSources = [];
    private readonly List<TextQuadSource> worldSourceScratch = [];
    private readonly List<TextProjectedQuadSource> queuedProjectedSources = [];
    private readonly List<TextAtlasUpdate> pendingAtlasUpdates = [];
    private readonly List<IntPtr> atlasTextures = [];
    private bool disposed;

    public BlurgTextRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat)
    {
        this.device = device;
        NativeLibraryResolver.ConfigureForAssembly(typeof(Blurg).Assembly);

        blurg = new Blurg(AllocateAtlasTexture, QueueAtlasTextureUpdate);
        if (!blurg.EnableSystemFonts())
            throw new InvalidOperationException("BlurgText system font loading failed.");

        defaultFont = ResolveDefaultFont(blurg);
        quadRenderer = new TextQuadRenderer(device, swapchainFormat, shaderFormat);
    }

    public string DefaultFontFamily => defaultFont.FamilyName;

    public TextSmokeLabelState SmokeLabelState => TextSmokeLabelState.CreateDefault();

    public Vector2 Measure(string text, float fontSize)
    {
        ThrowIfDisposed();
        return blurg.MeasureString(defaultFont, fontSize, text);
    }

    public void RenderSmokeLabel(SDL_GPUCommandBuffer* commandBuffer, SDL_GPUTexture* swapchainTexture, uint width, uint height)
    {
        RenderLabels(commandBuffer, swapchainTexture, width, height, default, null);
    }

    public void RenderLabels(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPUTexture* swapchainTexture,
        uint width,
        uint height,
        RenderCamera camera,
        IReadOnlyList<WorldTextBillboard>? worldBillboards)
    {
        ThrowIfDisposed();

        queuedSources.Clear();
        queuedProjectedSources.Clear();
        QueueSmokeLabel();
        QueueWorldLabels(camera, width, height, worldBillboards);

        FlushAtlasUpdates(commandBuffer);

        TextQuadBatch batch = TextQuadBatchBuilder.CreateCombined(queuedSources, Vector2.Zero, queuedProjectedSources);
        if (batch.IsEmpty)
            return;

        quadRenderer.Upload(commandBuffer, batch);

        var colorTarget = new SDL_GPUColorTargetInfo
        {
            texture = swapchainTexture,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_LOAD,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
        };

        SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(commandBuffer, &colorTarget, 1, (SDL_GPUDepthStencilTargetInfo*)null);
        if (renderPass is null)
            throw new InvalidOperationException($"SDL GPU text render pass creation failed: {SDL_GetError()}");

        quadRenderer.Render(commandBuffer, renderPass, width, height, batch);
        SDL_EndGPURenderPass(renderPass);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        quadRenderer.Dispose();
        blurg.Dispose();

        foreach (IntPtr texture in atlasTextures)
            SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)texture);

        atlasTextures.Clear();
    }

    private void QueueSmokeLabel()
    {
        TextSmokeLabelState label = TextSmokeLabelState.CreateDefault();
        QueueString(label.Text, label.Position + label.ShadowOffset, label.FontSize, label.Shadow);
        QueueString(label.Text, label.Position, label.FontSize, label.Foreground);
    }

    private void QueueString(string text, Vector2 position, float fontSize, BlurgColor color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        using BlurgResult result = blurg.BuildString(defaultFont, fontSize, color, text);
        for (int index = 0; index < result.Count; index++)
        {
            BlurgRect rect = result[index];
            queuedSources.Add(new TextQuadSource(
                rect.UserData,
                (int)MathF.Round(position.X + rect.X),
                (int)MathF.Round(position.Y + rect.Y),
                rect.Width,
                rect.Height,
                rect.U0,
                rect.V0,
                rect.U1,
                rect.V1,
                rect.Color));
        }
    }

    private void QueueWorldLabels(
        RenderCamera camera,
        uint width,
        uint height,
        IReadOnlyList<WorldTextBillboard>? worldBillboards)
    {
        if (worldBillboards is null || worldBillboards.Count == 0)
            return;

        foreach (WorldTextBillboard billboard in worldBillboards)
        {
            if (string.IsNullOrWhiteSpace(billboard.Text))
                continue;

            Vector2 textPixelSize = blurg.MeasureString(defaultFont, WorldTextBillboard.DefaultFontSize, billboard.Text);
            QueueWorldString(billboard, billboard.ShadowOffsetPixels, billboard.Shadow, textPixelSize, camera, width, height);
            QueueWorldString(billboard, Vector2.Zero, billboard.Foreground, textPixelSize, camera, width, height);
        }
    }

    private void QueueWorldString(
        WorldTextBillboard billboard,
        Vector2 offsetPixels,
        BlurgColor color,
        Vector2 textPixelSize,
        RenderCamera camera,
        uint width,
        uint height)
    {
        if (string.IsNullOrWhiteSpace(billboard.Text))
            return;

        worldSourceScratch.Clear();
        using BlurgResult result = blurg.BuildString(defaultFont, WorldTextBillboard.DefaultFontSize, color, billboard.Text);
        for (int index = 0; index < result.Count; index++)
        {
            BlurgRect rect = result[index];
            worldSourceScratch.Add(new TextQuadSource(
                rect.UserData,
                (int)MathF.Round(offsetPixels.X + rect.X),
                (int)MathF.Round(offsetPixels.Y + rect.Y),
                rect.Width,
                rect.Height,
                rect.U0,
                rect.V0,
                rect.U1,
                rect.V1,
                rect.Color));
        }

        IReadOnlyList<TextProjectedQuadSource> projectedSources = WorldTextProjector.CreateProjectedQuads(
            billboard,
            worldSourceScratch,
            textPixelSize,
            camera,
            width,
            height);

        foreach (TextProjectedQuadSource source in projectedSources)
            queuedProjectedSources.Add(source);
    }

    private IntPtr AllocateAtlasTexture(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"BlurgText requested an invalid atlas texture size: {width}x{height}.");

        var createInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = checked((uint)width),
            height = checked((uint)height),
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };

        SDL_GPUTexture* texture = SDL_CreateGPUTexture(device, &createInfo);
        if (texture is null)
            throw new InvalidOperationException($"SDL GPU BlurgText atlas texture creation failed: {SDL_GetError()}");

        atlasTextures.Add((IntPtr)texture);
        return (IntPtr)texture;
    }

    private void QueueAtlasTextureUpdate(IntPtr textureUserData, IntPtr buffer, int x, int y, int width, int height)
    {
        if (textureUserData == IntPtr.Zero || buffer == IntPtr.Zero || width <= 0 || height <= 0)
            return;

        int byteCount = checked(width * height * 4);
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(buffer, pixels, 0, byteCount);
        pendingAtlasUpdates.Add(new TextAtlasUpdate(textureUserData, pixels, x, y, width, height));
    }

    private void FlushAtlasUpdates(SDL_GPUCommandBuffer* commandBuffer)
    {
        foreach (TextAtlasUpdate update in pendingAtlasUpdates)
            UploadAtlasUpdate(commandBuffer, update);

        pendingAtlasUpdates.Clear();
    }

    private void UploadAtlasUpdate(SDL_GPUCommandBuffer* commandBuffer, TextAtlasUpdate update)
    {
        var transferCreateInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = checked((uint)update.Pixels.Length),
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(device, &transferCreateInfo);
        if (transferBuffer is null)
            throw new InvalidOperationException($"SDL GPU BlurgText atlas transfer buffer creation failed: {SDL_GetError()}");

        try
        {
            IntPtr mapped = SDL_MapGPUTransferBuffer(device, transferBuffer, cycle: false);
            if (mapped == IntPtr.Zero)
                throw new InvalidOperationException($"SDL GPU BlurgText atlas transfer buffer mapping failed: {SDL_GetError()}");

            fixed (byte* pixels = update.Pixels)
                Buffer.MemoryCopy(pixels, (void*)mapped, update.Pixels.Length, update.Pixels.Length);

            SDL_UnmapGPUTransferBuffer(device, transferBuffer);

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);
            if (copyPass is null)
                throw new InvalidOperationException($"SDL GPU BlurgText atlas copy pass creation failed: {SDL_GetError()}");

            var source = new SDL_GPUTextureTransferInfo
            {
                transfer_buffer = transferBuffer,
                pixels_per_row = checked((uint)update.Width),
                rows_per_layer = checked((uint)update.Height),
            };
            var destination = new SDL_GPUTextureRegion
            {
                texture = (SDL_GPUTexture*)update.TextureUserData,
                x = checked((uint)update.X),
                y = checked((uint)update.Y),
                w = checked((uint)update.Width),
                h = checked((uint)update.Height),
                d = 1,
            };

            SDL_UploadToGPUTexture(copyPass, &source, &destination, cycle: false);
            SDL_EndGPUCopyPass(copyPass);
        }
        finally
        {
            SDL_ReleaseGPUTransferBuffer(device, transferBuffer);
        }
    }

    private static BlurgFont ResolveDefaultFont(Blurg blurg)
    {
        foreach (string family in DefaultFontFamilies)
        {
            BlurgFont? font = blurg.QueryFont(family, FontWeight.Regular, italic: false);
            if (font is not null)
                return font;
        }

        throw new InvalidOperationException(
            $"BlurgText could not resolve a default system font. Tried: {string.Join(", ", DefaultFontFamilies)}.");
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(BlurgTextRenderer));
    }
}
