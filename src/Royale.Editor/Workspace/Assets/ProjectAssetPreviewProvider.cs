using System.Collections.Concurrent;
using Royale.Content.Models;
using Royale.Rendering;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using Royale.Rendering.Screenshots;
using SDL;

namespace Royale.Editor.Workspace.Assets;

public sealed class ProjectAssetPreviewProvider : IAssetPreviewProvider, IDisposable
{
    private const int Resolution = ModelThumbnailFraming.Resolution;
    private readonly SdlGpuDevice gpu;
    private readonly SdlGpuOffscreenTarget target;
    private readonly StaticMeshAssetCache meshes;
    private readonly string sourceRoot;
    private readonly string cacheRoot;
    private readonly Dictionary<string, ModelAssetDefinition> definitions;
    private readonly Dictionary<string, PreviewState> states = new(StringComparer.Ordinal);
    private readonly Queue<string> requests = new();
    private readonly Queue<ReadyPixels> uploads = new();
    private readonly ConcurrentQueue<string> backgroundFailures = new();
    private readonly List<Task> cacheWrites = [];
    private readonly Action<string>? reportFailure;
    private PendingReadback? pendingReadback;
    private bool disposed;

    public ProjectAssetPreviewProvider(SdlGpuDevice gpu, StaticMeshAssetCache meshes, ModelAssetManifest manifest, string sourceRoot, string cacheRoot, Action<string>? reportFailure = null)
    {
        this.gpu = gpu;
        this.meshes = meshes;
        this.sourceRoot = Path.GetFullPath(sourceRoot);
        this.cacheRoot = Path.GetFullPath(cacheRoot);
        this.reportFailure = reportFailure;
        definitions = manifest.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Directory.CreateDirectory(this.cacheRoot);
        target = gpu.CreateOffscreenTarget(Resolution, Resolution);
    }

    public nint GetPreviewTexture(string assetId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (states.TryGetValue(assetId, out PreviewState? state))
            return state.Texture?.NativeTextureHandle ?? 0;
        states.Add(assetId, new PreviewState());
        requests.Enqueue(assetId);
        return 0;
    }

    public ModelThumbnailPreviewDiagnostics Diagnostics => new(
        states.Count,
        states.Values.Count(state => state.Texture is not null),
        requests.Count,
        pendingReadback is not null,
        uploads.Count);

    public void ProcessFrame()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CompleteOneReadback();
        UploadOneCachedImage();
        SubmitOneRenderOrCacheLoad();
        cacheWrites.RemoveAll(task => task.IsCompleted);
        if (backgroundFailures.TryDequeue(out string? failure))
            reportFailure?.Invoke(failure);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (pendingReadback is not null)
        {
            try
            {
                _ = pendingReadback.Completion.GetAwaiter().GetResult();
            }
            catch
            {
                // Failure is discarded during lifecycle teardown.
            }
        }
        pendingReadback = null;
        Task.WaitAll(cacheWrites.ToArray());
        cacheWrites.Clear();
        foreach (PreviewState state in states.Values)
            state.Texture?.Dispose();
        states.Clear();
        requests.Clear();
        uploads.Clear();
        target.Dispose();
    }

    private void SubmitOneRenderOrCacheLoad()
    {
        if (pendingReadback is not null || requests.Count == 0)
            return;
        string assetId = requests.Dequeue();
        PreviewState state = states[assetId];
        if (!definitions.TryGetValue(assetId, out ModelAssetDefinition? definition) || definition.Render is null)
            return;

        try
        {
            string fingerprint = ModelThumbnailFingerprint.Calculate(assetId, definition.Render, sourceRoot);
            state.Fingerprint = fingerprint;
            string path = Path.Combine(cacheRoot, $"{assetId}-{fingerprint}.png");
            if (File.Exists(path))
            {
                try
                {
                    PngImage cached = PngImageCodec.Decode(File.ReadAllBytes(path));
                    if (cached.Width != Resolution || cached.Height != Resolution)
                        throw new InvalidDataException($"Cached thumbnail must be {Resolution}×{Resolution}.");
                    uploads.Enqueue(new ReadyPixels(assetId, fingerprint, cached.RgbaBytes));
                    return;
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException)
                {
                    File.Delete(path);
                    Report(assetId, $"Ignored corrupt thumbnail cache: {exception.Message}");
                }
            }

            StaticMeshAsset asset = meshes.GetRequired(assetId);
            ModelBounds bounds = ModelThumbnailFraming.CalculateBounds(asset);
            var frame = new RenderFrame(
                ModelThumbnailFraming.CreateCamera(bounds),
                ModelThumbnailFraming.CreateScene(asset),
                RenderViewMode.Normal,
                ClearColor: new SDL_FColor { r = 0.18f, g = 0.18f, b = 0.18f, a = 1.0f });
            GpuImageReadbackRequest request = gpu.BeginOffscreenReadback(target, frame);
            pendingReadback = new PendingReadback(assetId, fingerprint, Task.Run(request.Wait));
        }
        catch (Exception exception)
        {
            state.Failed = true;
            Report(assetId, exception.Message);
        }
    }

    private void CompleteOneReadback()
    {
        if (pendingReadback is null || !pendingReadback.Completion.IsCompleted)
            return;
        PendingReadback completed = pendingReadback;
        pendingReadback = null;
        try
        {
            GpuImageReadback image = completed.Completion.GetAwaiter().GetResult();
            uploads.Enqueue(new ReadyPixels(completed.AssetId, completed.Fingerprint, image.RgbaBytes));
            cacheWrites.Add(Task.Run(() => WriteCache(completed.AssetId, completed.Fingerprint, image.RgbaBytes)));
        }
        catch (Exception exception)
        {
            states[completed.AssetId].Failed = true;
            Report(completed.AssetId, exception.Message);
        }
    }

    private void UploadOneCachedImage()
    {
        if (uploads.Count == 0)
            return;
        ReadyPixels ready = uploads.Dequeue();
        if (!states.TryGetValue(ready.AssetId, out PreviewState? state) || state.Fingerprint != ready.Fingerprint)
            return;
        try
        {
            state.Texture?.Dispose();
            state.Texture = gpu.UploadRgbaTexture(ready.Rgba, Resolution, Resolution);
        }
        catch (Exception exception)
        {
            state.Failed = true;
            Report(ready.AssetId, exception.Message);
        }
    }

    private void WriteCache(string assetId, string fingerprint, byte[] rgba)
    {
        try
        {
            string path = Path.Combine(cacheRoot, $"{assetId}-{fingerprint}.png");
            PngImageCodec.Write(path, rgba, Resolution, Resolution, atomic: true);
            foreach (string stale in Directory.EnumerateFiles(cacheRoot, $"{assetId}-*.png"))
                if (!string.Equals(stale, path, StringComparison.Ordinal))
                    File.Delete(stale);
        }
        catch (Exception exception)
        {
            backgroundFailures.Enqueue($"Thumbnail {assetId}: Could not cache thumbnail: {exception.Message}");
        }
    }

    private void Report(string assetId, string message) => reportFailure?.Invoke($"Thumbnail {assetId}: {message}");

    private sealed class PreviewState
    {
        public string? Fingerprint { get; set; }
        public SdlGpuSampledTexture? Texture { get; set; }
        public bool Failed { get; set; }
    }

    private sealed record PendingReadback(string AssetId, string Fingerprint, Task<GpuImageReadback> Completion);
    private sealed record ReadyPixels(string AssetId, string Fingerprint, byte[] Rgba);
}

public readonly record struct ModelThumbnailPreviewDiagnostics(
    int Requested,
    int Ready,
    int Queued,
    bool ReadbackPending,
    int AwaitingUpload);
