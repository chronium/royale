using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Royale.Editor.Mcp;

public sealed class EditorMcpServer : IAsyncDisposable
{
    public const string Path = "/mcp";
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly int port;
    private readonly EditorMainThreadDispatcher dispatcher;
    private readonly EditorMcpWorkspace workspace;
    private readonly ILogger logger;
    private WebApplication? application;

    public EditorMcpServer(
        int port,
        EditorMainThreadDispatcher dispatcher,
        EditorMcpWorkspace workspace,
        ILogger<EditorMcpServer> logger)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        this.port = port;
        this.dispatcher = dispatcher;
        this.workspace = workspace;
        this.logger = logger;
        Status = new($"http://127.0.0.1:{port}{Path}");
    }

    public EditorMcpStatus Status { get; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (application is not null)
            throw new InvalidOperationException("The editor MCP server has already started.");

        WebApplication app = BuildApplication();
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            application = app;
            Status.SetState(EditorMcpServerState.Listening);
            logger.LogInformation("Editor MCP server listening at {Endpoint}.", Status.Snapshot.Endpoint);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status.SetState(EditorMcpServerState.Stopped);
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            const string error = "The MCP endpoint could not start. The requested loopback port may already be in use.";
            Status.SetState(EditorMcpServerState.Faulted, error);
            await app.DisposeAsync().ConfigureAwait(false);
            logger.LogError(exception, "Editor MCP server failed to bind to IPv4 loopback port {Port}.", port);
            throw new InvalidOperationException($"{error} Port: {port}.", exception);
        }
    }

    public async Task StopAsync(TimeSpan? timeout = null)
    {
        WebApplication? app = application;
        if (app is null)
            return;

        Status.SetState(EditorMcpServerState.Stopping);
        using var cancellation = new CancellationTokenSource(timeout ?? DefaultShutdownTimeout);
        try
        {
            await app.StopAsync(cancellation.Token).ConfigureAwait(false);
            Status.SetState(EditorMcpServerState.Stopped);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            const string error = "The MCP endpoint did not stop within the shutdown timeout.";
            Status.SetState(EditorMcpServerState.Faulted, error);
            logger.LogWarning("Editor MCP server shutdown exceeded its timeout.");
        }
        finally
        {
            application = null;
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private WebApplication BuildApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        builder.Services.AddSingleton(dispatcher);
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "royale-editor",
                    Version = "1.0.0",
                };
            })
            .WithTools(new EditorMcpTools(dispatcher, workspace))
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
#pragma warning disable MCP9004
                options.EnableLegacySse = false;
#pragma warning restore MCP9004
            });

        WebApplication app = builder.Build();
        app.Use(ValidateAndTrackRequestAsync);
        app.MapMcp(Path);
        return app;
    }

    private async Task ValidateAndTrackRequestAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Headers.ContainsKey("Origin"))
        {
            Status.RejectRequest("Rejected a request carrying an Origin header.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        string host = context.Request.Host.Host;
        if (!string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            Status.RejectRequest("Rejected a request with a disallowed Host header.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using (Status.AcceptRequest())
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                const string error = "The MCP transport encountered an unexpected error.";
                Status.SetState(EditorMcpServerState.Faulted, error);
                logger.LogError(exception, "Editor MCP transport failed while handling a request.");
                throw;
            }
        }
    }
}
