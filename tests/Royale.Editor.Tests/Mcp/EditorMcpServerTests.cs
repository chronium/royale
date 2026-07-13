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
using Royale.Editor.Mcp;

namespace Royale.Editor.Tests.Mcp;

public sealed class EditorMcpServerTests
{
    [Fact]
    public async Task OfficialClientInitializesAndListsNoTools()
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

        Assert.Empty(tools);
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
        new(port, new EditorMainThreadDispatcher(), NullLogger<EditorMcpServer>.Instance);

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
