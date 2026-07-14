using Microsoft.Extensions.Logging.Abstractions;
using Royale.Content;
using Royale.Content.Models;
using Royale.Editor.Mcp;
using Royale.Rendering;
using Royale.Rendering.Meshes;

namespace Royale.Editor.Tests.Mcp;

public sealed class ModelContactSheetCaptureServiceTests
{
    [Fact]
    public async Task RejectsConcurrentCaptureUntilActiveRequestCompletes()
    {
        var backend = new FakeGpuBackend();
        using ModelContactSheetCaptureService service = CreateService(backend);
        Task<ModelContactSheetCapture> first = service.CaptureAsync("kenney-crate", "axis", CancellationToken.None);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = service.CaptureAsync("kenney-crate", "diagonal", CancellationToken.None);
        });

        Assert.Contains("already active", error.Message);
        service.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        Assert.True(backend.Target!.Disposed);
    }

    [Fact]
    public async Task CancellationBetweenViewsStopsSubmissionAfterDrainingPendingReadback()
    {
        var backend = new FakeGpuBackend();
        using ModelContactSheetCaptureService service = CreateService(backend);
        using var cancellation = new CancellationTokenSource();
        Task<ModelContactSheetCapture> capture = service.CaptureAsync("kenney-crate", "axis", cancellation.Token);

        service.ProcessFrame();
        backend.Complete(0);
        service.ProcessFrame();
        service.ProcessFrame();
        cancellation.Cancel();

        Assert.False(capture.IsCompleted);
        backend.Complete(1);
        service.ProcessFrame();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        Assert.Equal(2, backend.Readbacks.Count);
        Assert.True(backend.Target!.Disposed);
    }

    [Fact]
    public async Task GpuFailureFaultsCaptureAndReleasesTarget()
    {
        var backend = new FakeGpuBackend();
        using ModelContactSheetCaptureService service = CreateService(backend);
        Task<ModelContactSheetCapture> capture = service.CaptureAsync("kenney-crate", "axis", CancellationToken.None);

        service.ProcessFrame();
        backend.Fail(0, new InvalidOperationException("simulated readback failure"));
        service.ProcessFrame();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => capture);
        Assert.Contains("simulated readback failure", error.Message);
        Assert.True(backend.Target!.Disposed);
    }

    [Fact]
    public async Task ShutdownDrainsInflightReadbackAndFaultsRequestAfterCleanup()
    {
        var backend = new FakeGpuBackend();
        ModelContactSheetCaptureService service = CreateService(backend);
        Task<ModelContactSheetCapture> capture = service.CaptureAsync("kenney-crate", "axis", CancellationToken.None);
        service.ProcessFrame();

        Task shutdown = Task.Run(service.Dispose);
        await Task.Yield();
        Assert.False(shutdown.IsCompleted);
        backend.Complete(0);
        await shutdown;

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => capture);
        Assert.Contains("shut down", error.Message);
        Assert.True(backend.Target!.Disposed);
    }

    private static ModelContactSheetCaptureService CreateService(FakeGpuBackend backend)
    {
        string assetRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        ModelAssetManifest manifest = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName));
        StaticMeshAssetCache cache = StaticMeshAssetCache.Load(AppContext.BaseDirectory);
        return new ModelContactSheetCaptureService(
            backend,
            () => manifest,
            () => cache,
            () => null,
            NullLogger.Instance);
    }

    private sealed class FakeGpuBackend : IModelContactSheetGpuBackend
    {
        public List<TaskCompletionSource<GpuImageReadback>> Readbacks { get; } = [];
        public FakeTarget? Target { get; private set; }

        public IModelContactSheetGpuTarget CreateTarget(int width, int height)
        {
            Assert.Equal(ModelContactSheetFraming.TileSize, width);
            Assert.Equal(ModelContactSheetFraming.TileSize, height);
            Target = new FakeTarget();
            return Target;
        }

        public Task<GpuImageReadback> BeginReadback(IModelContactSheetGpuTarget target, RenderFrame frame)
        {
            Assert.Same(Target, target);
            var completion = new TaskCompletionSource<GpuImageReadback>(TaskCreationOptions.RunContinuationsAsynchronously);
            Readbacks.Add(completion);
            return completion.Task;
        }

        public void Complete(int index) => Readbacks[index].SetResult(SolidTile());

        public void Fail(int index, Exception exception) => Readbacks[index].SetException(exception);

        private static GpuImageReadback SolidTile()
        {
            byte[] rgba = new byte[ModelContactSheetFraming.TileSize * ModelContactSheetFraming.TileSize * 4];
            for (int offset = 3; offset < rgba.Length; offset += 4)
                rgba[offset] = byte.MaxValue;
            return new GpuImageReadback(ModelContactSheetFraming.TileSize, ModelContactSheetFraming.TileSize, rgba);
        }
    }

    private sealed class FakeTarget : IModelContactSheetGpuTarget
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
