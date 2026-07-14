using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Mcp;

namespace Royale.Editor.Tests.Mcp;

public sealed class EditorMcpContactSheetToolTests
{
    [Theory]
    [InlineData(null, "both", 2, 14)]
    [InlineData("axis", "axis", 1, 6)]
    [InlineData("diagonal", "diagonal", 1, 8)]
    [InlineData("both", "both", 2, 14)]
    public async Task ReturnsRequestedPngBlocksStructuredMetadataAndContentIndexes(
        string? requested,
        string expected,
        int imageCount,
        int viewCount)
    {
        var capture = new FakeCaptureService();
        EditorMcpTools tools = CreateTools(capture);

        CallToolResult result = await tools.CaptureModelContactSheets("crate", requested ?? "both");

        Assert.Equal(expected, capture.LastViewSet);
        Assert.Equal(imageCount, result.Content.Count);
        Assert.All(result.Content, item =>
        {
            ImageContentBlock image = Assert.IsType<ImageContentBlock>(item);
            Assert.Equal("image/png", image.MimeType);
            Assert.Equal([Role.User, Role.Assistant], image.Annotations?.Audience);
            Assert.Equal(1.0f, image.Annotations?.Priority);
        });
        EditorMcpContactSheetResult metadata = result.StructuredContent!.Value.Deserialize<EditorMcpContactSheetResult>()!;
        Assert.Equal("crate", metadata.AssetId);
        Assert.Equal("fingerprint", metadata.ManifestFingerprint);
        Assert.Equal(viewCount, metadata.Views.Count);
        Assert.Equal(Enumerable.Range(0, imageCount), metadata.Sheets.Select(sheet => sheet.ContentIndex));
        Assert.All(metadata.Views, view => Assert.InRange(view.ContentIndex, 0, imageCount - 1));
    }

    [Fact]
    public async Task BusyGpuFailureAndCancellationPropagateAsControlledToolOutcomes()
    {
        var busy = new FakeCaptureService { Failure = new InvalidOperationException("capture is busy") };
        McpException busyError = await Assert.ThrowsAsync<McpException>(() =>
            CreateTools(busy).CaptureModelContactSheets("crate", "axis"));
        Assert.Contains("busy", busyError.Message);

        var gpu = new FakeCaptureService { Failure = new InvalidOperationException("GPU readback failed") };
        McpException gpuError = await Assert.ThrowsAsync<McpException>(() =>
            CreateTools(gpu).CaptureModelContactSheets("crate", "axis"));
        Assert.Contains("GPU readback failed", gpuError.Message);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTools(new FakeCaptureService()).CaptureModelContactSheets("crate", "both", cancellation.Token));
    }

    private static EditorMcpTools CreateTools(IModelContactSheetCaptureService capture)
    {
        var dispatcher = new EditorMainThreadDispatcher();
        dispatcher.BindToCurrentThread();
        var document = new EditorMapDocument(new GameMap
        {
            Id = "test",
            Name = "Test",
            WorldBounds = new MapBounds
            {
                Min = new MapVector3(-1, -1, -1),
                Max = new MapVector3(1, 1, 1),
            },
            SafeZone = new SafeZoneDefinition { Radius = 1 },
        }, null, null, false);
        var workspace = new EditorMcpWorkspace(
            () => document,
            () => null,
            () => null,
            () => null,
            () => null,
            () => false,
            _ => { },
            () => { },
            _ => { },
            () => capture);
        return new EditorMcpTools(dispatcher, workspace);
    }

    private sealed class FakeCaptureService : IModelContactSheetCaptureService
    {
        public Exception? Failure { get; init; }
        public string? LastViewSet { get; private set; }

        public Task<ModelContactSheetCapture> CaptureAsync(
            string assetId,
            string? viewSet,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastViewSet = viewSet;
            if (Failure is not null)
                throw Failure;
            bool both = viewSet == "both";
            bool axis = viewSet is "axis" or "both";
            int count = both ? 2 : 1;
            var sheets = new List<EditorMcpContactSheetInfo>();
            var views = new List<EditorMcpContactSheetView>();
            if (axis)
            {
                sheets.Add(new EditorMcpContactSheetInfo("axis", new EditorMcpDimensions(1152, 768), 0));
                AddViews(views, "axis", 6, 0);
            }
            if (viewSet is "diagonal" or "both")
            {
                int contentIndex = both ? 1 : 0;
                sheets.Add(new EditorMcpContactSheetInfo("diagonal", new EditorMcpDimensions(1536, 768), contentIndex));
                AddViews(views, "diagonal", 8, contentIndex);
            }
            var metadata = new EditorMcpContactSheetResult(
                assetId,
                "fingerprint",
                new EditorMcpBounds(new EditorMcpVector3(-1, -1, -1), new EditorMcpVector3(1, 1, 1)),
                4.0f,
                new EditorMcpDimensions(384, 384),
                sheets,
                views);
            return Task.FromResult(new ModelContactSheetCapture(metadata,
                Enumerable.Range(0, count).Select(_ => new byte[] { 137, 80, 78, 71 }).ToArray()));
        }

        private static void AddViews(List<EditorMcpContactSheetView> views, string set, int count, int contentIndex)
        {
            for (int index = 0; index < count; index++)
                views.Add(new EditorMcpContactSheetView(
                    set,
                    index.ToString(),
                    index / (count / 2),
                    index % (count / 2),
                    contentIndex,
                    new EditorMcpVector3(1, 0, 0),
                    new EditorMcpVector3(0, 1, 0)));
        }
    }
}
