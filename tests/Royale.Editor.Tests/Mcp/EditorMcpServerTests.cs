using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Mcp;

namespace Royale.Editor.Tests.Mcp;

public sealed class EditorMcpServerTests
{
    [Fact]
    public async Task OfficialClientListsExactStructuredToolSurfaceAndAnnotations()
    {
        await using EditorMcpServer server = CreateServer(GetAvailablePort());
        await server.StartAsync();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.Status.Snapshot.Endpoint),
            Name = "royale-editor-test",
            TransportMode = HttpTransportMode.StreamableHttp,
        });
        await using McpClient client = await McpClient.CreateAsync(transport);

        IList<McpClientTool> tools = await client.ListToolsAsync();

        string[] expectedNames =
        [
            "capture_model_contact_sheets", "create_entity", "delete_entity", "duplicate_entity", "get_editor_status", "get_entity", "get_map",
            "list_assets", "list_entities", "redo", "replace_entity", "save", "set_entity_transform",
            "set_map_name", "set_safe_zone", "set_world_bounds", "snap_entity_to_face", "undo", "validate_map",
        ];
        Assert.Equal(expectedNames, tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        Assert.All(tools, tool =>
        {
            Assert.Equal("object", tool.ProtocolTool.InputSchema.GetProperty("type").GetString());
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
            Assert.False(tool.ProtocolTool.Annotations?.OpenWorldHint);
        });
        foreach (string name in new[] { "capture_model_contact_sheets", "get_editor_status", "get_entity", "get_map", "list_assets", "list_entities", "validate_map" })
            Assert.True(tools.Single(tool => tool.Name == name).ProtocolTool.Annotations?.ReadOnlyHint);
        McpClientTool contactSheets = tools.Single(tool => tool.Name == "capture_model_contact_sheets");
        Assert.False(contactSheets.ProtocolTool.Annotations?.DestructiveHint);
        Assert.Contains("axis", contactSheets.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("viewSet").GetProperty("description").GetString());
        Assert.True(contactSheets.ProtocolTool.OutputSchema!.Value.GetProperty("properties").TryGetProperty("views", out _));
        Assert.False(tools.Single(tool => tool.Name == "create_entity").ProtocolTool.Annotations?.DestructiveHint);
        Assert.True(tools.Single(tool => tool.Name == "delete_entity").ProtocolTool.Annotations?.DestructiveHint);
        Assert.Equal(EditorMcpServerState.Listening, server.Status.Snapshot.State);
        Assert.True(server.Status.Snapshot.TotalAcceptedRequests >= 2);
    }

    [Fact]
    public async Task AllowsOnlyLocalHostValuesAndRejectsEveryOrigin()
    {
        await using EditorMcpServer server = CreateServer(GetAvailablePort());
        await server.StartAsync();
        using var client = new HttpClient();

        using HttpResponseMessage ipv4 = await client.GetAsync(server.Status.Snapshot.Endpoint);
        using var localhostRequest = new HttpRequestMessage(HttpMethod.Get, server.Status.Snapshot.Endpoint);
        localhostRequest.Headers.Host = $"localhost:{new Uri(server.Status.Snapshot.Endpoint).Port}";
        using HttpResponseMessage localhost = await client.SendAsync(localhostRequest);
        using var foreignHostRequest = new HttpRequestMessage(HttpMethod.Get, server.Status.Snapshot.Endpoint);
        foreignHostRequest.Headers.Host = "example.test";
        using HttpResponseMessage foreignHost = await client.SendAsync(foreignHostRequest);
        using var originRequest = new HttpRequestMessage(HttpMethod.Get, server.Status.Snapshot.Endpoint);
        originRequest.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1");
        using HttpResponseMessage origin = await client.SendAsync(originRequest);

        Assert.NotEqual(HttpStatusCode.Forbidden, ipv4.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, localhost.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignHost.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, origin.StatusCode);
        Assert.Equal(2, server.Status.Snapshot.TotalAcceptedRequests);
        Assert.Contains("Origin", server.Status.Snapshot.LastRejectedRequest?.Summary);
    }

    [Fact]
    public async Task ListensOnlyOnIpv4Loopback()
    {
        int port = GetAvailablePort();
        await using EditorMcpServer server = CreateServer(port);
        await server.StartAsync();

        using var ipv4 = new TcpClient(AddressFamily.InterNetwork);
        await ipv4.ConnectAsync(IPAddress.Loopback, port);
        using var ipv6 = new TcpClient(AddressFamily.InterNetworkV6);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ipv6.ConnectAsync(IPAddress.IPv6Loopback, port).WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task LegacySseAndCorsAreDisabled()
    {
        await using EditorMcpServer server = CreateServer(GetAvailablePort());
        await server.StartAsync();
        using var client = new HttpClient();

        using HttpResponseMessage legacySse = await client.GetAsync(server.Status.Snapshot.Endpoint + "/sse");
        using var browserRequest = new HttpRequestMessage(HttpMethod.Options, server.Status.Snapshot.Endpoint);
        browserRequest.Headers.TryAddWithoutValidation("Origin", "http://localhost:3000");
        using HttpResponseMessage browser = await client.SendAsync(browserRequest);

        Assert.Equal(HttpStatusCode.NotFound, legacySse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, browser.StatusCode);
        Assert.False(browser.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task BindFailureFaultsStartupWithSanitizedStatus()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int port = ((IPEndPoint)occupied.LocalEndpoint).Port;
        await using EditorMcpServer server = CreateServer(port);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => server.StartAsync());

        Assert.Contains("loopback port", exception.Message);
        Assert.Equal(EditorMcpServerState.Faulted, server.Status.Snapshot.State);
        Assert.DoesNotContain(nameof(SocketException), server.Status.Snapshot.Error);
    }

    [Fact]
    public async Task StartupCancellationPropagatesAndLeavesServerStopped()
    {
        await using EditorMcpServer server = CreateServer(GetAvailablePort());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.StartAsync(cancellation.Token));

        Assert.Equal(EditorMcpServerState.Stopped, server.Status.Snapshot.State);
        Assert.Null(server.Status.Snapshot.Error);
    }

    [Fact]
    public async Task StopsWithinBoundAndTransitionsToStopped()
    {
        await using EditorMcpServer server = CreateServer(GetAvailablePort());
        await server.StartAsync();
        Stopwatch stopwatch = Stopwatch.StartNew();

        await server.StopAsync(TimeSpan.FromSeconds(2));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.Equal(EditorMcpServerState.Stopped, server.Status.Snapshot.State);
    }

    private static EditorMcpServer CreateServer(int port) =>
        new(
            port,
            new EditorMainThreadDispatcher(),
            CreateWorkspace(),
            NullLogger<EditorMcpServer>.Instance);

    private static EditorMcpWorkspace CreateWorkspace()
    {
        var document = new EditorMapDocument(new GameMap
        {
            Id = "test",
            Name = "Test",
            WorldBounds = new MapBounds
            {
                Min = new MapVector3(-10, -1, -10),
                Max = new MapVector3(10, 5, 10),
            },
            SafeZone = new SafeZoneDefinition { Radius = 5 },
        }, null, null, false);
        return new EditorMcpWorkspace(
            () => document,
            () => null,
            () => null,
            () => null,
            () => null,
            () => false,
            _ => { },
            () => { },
            _ => { });
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
