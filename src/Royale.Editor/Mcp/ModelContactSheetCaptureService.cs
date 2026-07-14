using System.Numerics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Royale.Content.Models;
using Royale.Editor.Projects;
using Royale.Rendering;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using Royale.Rendering.Text;
using SDL;

namespace Royale.Editor.Mcp;

public interface IModelContactSheetCaptureService
{
    Task<ModelContactSheetCapture> CaptureAsync(string assetId, string? viewSet, CancellationToken cancellationToken);
}

public interface IModelContactSheetGpuTarget : IDisposable
{
}

public interface IModelContactSheetGpuBackend
{
    IModelContactSheetGpuTarget CreateTarget(int width, int height);
    Task<GpuImageReadback> BeginReadback(IModelContactSheetGpuTarget target, RenderFrame frame);
}

public sealed record ModelContactSheetCapture(
    EditorMcpContactSheetResult Metadata,
    IReadOnlyList<byte[]> PngImages);

public sealed class ModelContactSheetCaptureService : IModelContactSheetCaptureService, IDisposable
{
    private static readonly SDL_FColor Background = new() { r = 0.18f, g = 0.18f, b = 0.18f, a = 1.0f };

    private readonly IModelContactSheetGpuBackend gpu;
    private readonly Func<ModelAssetManifest?> getManifest;
    private readonly Func<StaticMeshAssetCache?> getMeshCache;
    private readonly Func<EditorProjectSession?> getProjectSession;
    private readonly ILogger logger;
    private CaptureRequest? active;
    private bool disposed;

    public bool IsActive => active is not null;

    public ModelContactSheetCaptureService(
        SdlGpuDevice gpu,
        Func<ModelAssetManifest?> getManifest,
        Func<StaticMeshAssetCache?> getMeshCache,
        Func<EditorProjectSession?> getProjectSession,
        ILogger logger)
        : this(
            new SdlModelContactSheetGpuBackend(gpu),
            getManifest,
            getMeshCache,
            getProjectSession,
            logger)
    {
    }

    public ModelContactSheetCaptureService(
        IModelContactSheetGpuBackend gpu,
        Func<ModelAssetManifest?> getManifest,
        Func<StaticMeshAssetCache?> getMeshCache,
        Func<EditorProjectSession?> getProjectSession,
        ILogger logger)
    {
        this.gpu = gpu;
        this.getManifest = getManifest;
        this.getMeshCache = getMeshCache;
        this.getProjectSession = getProjectSession;
        this.logger = logger;
    }

    public Task<ModelContactSheetCapture> CaptureAsync(
        string assetId,
        string? viewSet,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (active is not null)
            throw new InvalidOperationException("A model contact-sheet capture is already active; wait for it to finish before requesting another.");
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        ContactSheetSelection selection = ParseSelection(viewSet);
        ModelAssetManifest manifest = getManifest()
            ?? throw new InvalidOperationException("Model contact-sheet capture is unavailable because rendering is not initialized.");
        ModelAssetDefinition definition = manifest.Assets.SingleOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Model asset '{assetId}' is not present in the active manifest.");
        if (definition.Render is null)
            throw new InvalidOperationException($"Model asset '{assetId}' is collision-only and cannot be rendered.");
        StaticMeshAssetCache meshes = getMeshCache()
            ?? throw new InvalidOperationException("Model contact-sheet capture is unavailable because rendering is not initialized.");
        StaticMeshAsset asset = meshes.GetRequired(assetId);
        ModelBounds bounds = ModelContactSheetFraming.NormalizeBounds(ModelThumbnailFraming.CalculateBounds(asset));
        StaticMeshScene scene = ModelThumbnailFraming.CreateScene(asset);
        string fingerprint = getProjectSession()?.AssetManifestFingerprint ?? Fingerprint(manifest);
        IModelContactSheetGpuTarget target = gpu.CreateTarget(
            ModelContactSheetFraming.TileSize,
            ModelContactSheetFraming.TileSize);
        active = new CaptureRequest(
            assetId,
            fingerprint,
            selection,
            bounds,
            scene,
            target,
            cancellationToken);
        logger.LogInformation(
            "Started {ViewCount}-view contact-sheet capture for model asset {AssetId}.",
            active.Views.Count,
            assetId);
        return active.Completion.Task;
    }

    public void ProcessFrame()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CaptureRequest? request = active;
        if (request is null)
            return;

        try
        {
            if (request.PendingReadback is not null)
            {
                if (!request.PendingReadback.IsCompleted)
                {
                    request.PendingPollFrames++;
                    if (request.PendingPollFrames % 300 == 0)
                    {
                        logger.LogWarning(
                            "Contact-sheet view {ViewIndex}/{ViewCount} for {AssetId} is still waiting for its GPU fence after {PollFrames} editor frames.",
                            request.NextViewIndex,
                            request.Views.Count,
                            request.AssetId,
                            request.PendingPollFrames);
                    }
                    return;
                }
                GpuImageReadback image = request.PendingReadback.GetAwaiter().GetResult();
                request.PendingReadback = null;
                logger.LogDebug(
                    "Completed contact-sheet view {ViewIndex}/{ViewCount} for {AssetId}.",
                    request.NextViewIndex,
                    request.Views.Count,
                    request.AssetId);
                if (!request.CancelRequested)
                    request.Tiles.Add(image);
                else
                    CancelCompleted(request);
                return;
            }

            if (request.Encoding is not null)
            {
                if (!request.Encoding.IsCompleted)
                    return;
                if (request.CancelRequested)
                {
                    ObserveEncoding(request.Encoding);
                    CancelCompleted(request);
                    return;
                }
                Complete(request, request.Encoding.GetAwaiter().GetResult());
                return;
            }

            if (request.CancelRequested)
            {
                CancelCompleted(request);
                return;
            }

            if (request.NextViewIndex < request.Views.Count)
            {
                ModelContactSheetView view = request.Views[request.NextViewIndex++];
                var frame = new RenderFrame(
                    ModelContactSheetFraming.CreateCamera(request.Bounds, view),
                    request.Scene,
                    RenderViewMode.Normal,
                    ClearColor: Background,
                    ScreenText: [ScreenTextLabel.Create(view.Label, new Vector2(16.0f, 12.0f), 24.0f)],
                    ShowSmokeLabel: false);
                request.PendingReadback = gpu.BeginReadback(request.Target, frame);
                request.PendingPollFrames = 0;
                logger.LogDebug(
                    "Submitted contact-sheet view {ViewIndex}/{ViewCount} ({Label}) for {AssetId}.",
                    request.NextViewIndex,
                    request.Views.Count,
                    view.Label,
                    request.AssetId);
                return;
            }

            logger.LogDebug("Encoding contact sheets for {AssetId} on a background worker.", request.AssetId);
            request.Encoding = Task.Run(() => Encode(request));
        }
        catch (Exception exception)
        {
            Fail(request, exception);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        CaptureRequest? request = active;
        active = null;
        if (request is null)
            return;

        try
        {
            if (request.PendingReadback is not null)
                _ = request.PendingReadback.GetAwaiter().GetResult();
            if (request.Encoding is not null)
                _ = request.Encoding.GetAwaiter().GetResult();
        }
        catch
        {
            // Shutdown still releases all retained GPU and cancellation resources.
        }
        finally
        {
            request.Dispose();
            request.Completion.TrySetException(new InvalidOperationException("The editor shut down before model contact-sheet capture completed."));
        }
    }

    private static ModelContactSheetCapture Encode(CaptureRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        var images = new List<byte[]>(request.Selection == ContactSheetSelection.Both ? 2 : 1);
        var sheetInfos = new List<EditorMcpContactSheetInfo>(images.Capacity);
        var viewInfos = new List<EditorMcpContactSheetView>(request.Views.Count);
        int tileOffset = 0;
        int contentIndex = 0;

        if (request.Selection is ContactSheetSelection.Axis or ContactSheetSelection.Both)
        {
            images.Add(ModelContactSheetComposer.ComposePng(request.Tiles.Skip(tileOffset).Take(6).ToArray(), 3));
            sheetInfos.Add(new EditorMcpContactSheetInfo("axis", new EditorMcpDimensions(1152, 768), contentIndex));
            AddViews(viewInfos, ModelContactSheetFraming.AxisViews, "axis", contentIndex);
            tileOffset += 6;
            contentIndex++;
        }

        if (request.Selection is ContactSheetSelection.Diagonal or ContactSheetSelection.Both)
        {
            images.Add(ModelContactSheetComposer.ComposePng(request.Tiles.Skip(tileOffset).Take(8).ToArray(), 4));
            sheetInfos.Add(new EditorMcpContactSheetInfo("diagonal", new EditorMcpDimensions(1536, 768), contentIndex));
            AddViews(viewInfos, ModelContactSheetFraming.DiagonalViews, "diagonal", contentIndex);
        }

        var metadata = new EditorMcpContactSheetResult(
            request.AssetId,
            request.ManifestFingerprint,
            new EditorMcpBounds(ToContract(request.Bounds.Minimum), ToContract(request.Bounds.Maximum)),
            ModelContactSheetFraming.CalculateOrthographicSize(request.Bounds),
            new EditorMcpDimensions(ModelContactSheetFraming.TileSize, ModelContactSheetFraming.TileSize),
            sheetInfos,
            viewInfos);
        return new ModelContactSheetCapture(metadata, images);
    }

    private static void AddViews(
        List<EditorMcpContactSheetView> destination,
        IReadOnlyList<ModelContactSheetView> views,
        string viewSet,
        int contentIndex)
    {
        foreach (ModelContactSheetView view in views)
            destination.Add(new EditorMcpContactSheetView(
                viewSet,
                view.Label,
                view.Row,
                view.Column,
                contentIndex,
                ToContract(view.CameraFromDirection),
                ToContract(view.UpDirection)));
    }

    private static EditorMcpVector3 ToContract(Vector3 value) => new(value.X, value.Y, value.Z);

    private static string Fingerprint(ModelAssetManifest manifest) =>
        Convert.ToHexStringLower(SHA256.HashData(ModelAssetManifestSerializer.Serialize(manifest)));

    private static void ObserveEncoding(Task<ModelContactSheetCapture> encoding)
    {
        try
        {
            _ = encoding.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Complete(CaptureRequest request, ModelContactSheetCapture capture)
    {
        active = null;
        request.Dispose();
        request.Completion.TrySetResult(capture);
        logger.LogInformation("Completed contact-sheet capture for model asset {AssetId}.", request.AssetId);
    }

    private void CancelCompleted(CaptureRequest request)
    {
        active = null;
        request.Dispose();
        request.Completion.TrySetCanceled(request.CancellationToken);
        logger.LogInformation("Canceled contact-sheet capture for model asset {AssetId}.", request.AssetId);
    }

    private void Fail(CaptureRequest request, Exception exception)
    {
        active = null;
        request.Dispose();
        request.Completion.TrySetException(new InvalidOperationException(
            $"Model contact-sheet capture failed: {exception.Message}", exception));
        logger.LogError(exception, "Contact-sheet capture failed for model asset {AssetId}.", request.AssetId);
    }

    private enum ContactSheetSelection
    {
        Axis,
        Diagonal,
        Both,
    }

    private static ContactSheetSelection ParseSelection(string? value) => value switch
    {
        null or "both" => ContactSheetSelection.Both,
        "axis" => ContactSheetSelection.Axis,
        "diagonal" => ContactSheetSelection.Diagonal,
        _ => throw new ArgumentException("viewSet must be 'axis', 'diagonal', or 'both'.", nameof(value)),
    };

    private sealed class CaptureRequest : IDisposable
    {
        private readonly CancellationTokenRegistration cancellationRegistration;

        public CaptureRequest(
            string assetId,
            string manifestFingerprint,
            ContactSheetSelection selection,
            ModelBounds bounds,
            StaticMeshScene scene,
            IModelContactSheetGpuTarget target,
            CancellationToken cancellationToken)
        {
            AssetId = assetId;
            ManifestFingerprint = manifestFingerprint;
            Selection = selection;
            Bounds = bounds;
            Scene = scene;
            Target = target;
            CancellationToken = cancellationToken;
            Views = selection switch
            {
                ContactSheetSelection.Axis => ModelContactSheetFraming.AxisViews,
                ContactSheetSelection.Diagonal => ModelContactSheetFraming.DiagonalViews,
                _ => ModelContactSheetFraming.AxisViews.Concat(ModelContactSheetFraming.DiagonalViews).ToArray(),
            };
            cancellationRegistration = cancellationToken.Register(() => CancelRequested = true);
        }

        public string AssetId { get; }
        public string ManifestFingerprint { get; }
        public ContactSheetSelection Selection { get; }
        public ModelBounds Bounds { get; }
        public StaticMeshScene Scene { get; }
        public IModelContactSheetGpuTarget Target { get; }
        public CancellationToken CancellationToken { get; }
        public IReadOnlyList<ModelContactSheetView> Views { get; }
        public List<GpuImageReadback> Tiles { get; } = [];
        public TaskCompletionSource<ModelContactSheetCapture> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<GpuImageReadback>? PendingReadback { get; set; }
        public Task<ModelContactSheetCapture>? Encoding { get; set; }
        public int NextViewIndex { get; set; }
        public int PendingPollFrames { get; set; }
        public bool CancelRequested
        {
            get => Volatile.Read(ref cancelRequested) != 0;
            private set => Volatile.Write(ref cancelRequested, value ? 1 : 0);
        }

        private int cancelRequested;

        public void Dispose()
        {
            cancellationRegistration.Dispose();
            PendingReadback = null;
            Target.Dispose();
        }
    }

    private sealed class SdlModelContactSheetGpuBackend(SdlGpuDevice gpu) : IModelContactSheetGpuBackend
    {
        public IModelContactSheetGpuTarget CreateTarget(int width, int height) =>
            new SdlTarget(gpu.CreateOffscreenTarget(width, height));

        public Task<GpuImageReadback> BeginReadback(IModelContactSheetGpuTarget target, RenderFrame frame)
        {
            if (target is not SdlTarget sdlTarget)
                throw new ArgumentException("The contact-sheet target does not belong to the SDL GPU backend.", nameof(target));
            GpuImageReadbackRequest readback = gpu.BeginOffscreenReadback(sdlTarget.Target, frame);
            return Task.Run(readback.Wait);
        }

        private sealed class SdlTarget(SdlGpuOffscreenTarget target) : IModelContactSheetGpuTarget
        {
            public SdlGpuOffscreenTarget Target { get; } = target;

            public void Dispose() => Target.Dispose();
        }
    }
}
