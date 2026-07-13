namespace Royale.Editor.Playtest;

public sealed class EditorPlaytestLauncher : IDisposable
{
    public const string ServerReadyMarker = "ROYALE_SERVER_READY";
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromSeconds(15);

    private readonly IPlaytestProcessFactory processFactory;
    private readonly Action<string> log;
    private readonly TimeSpan readyTimeout;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private IPlaytestProcess? server;
    private IPlaytestProcess? client;
    private IDisposable? artifacts;
    private CancellationTokenSource? launchCancellation;
    private bool stopping;
    private bool disposed;

    public EditorPlaytestLauncher(
        Action<string> log,
        IPlaytestProcessFactory? processFactory = null,
        TimeSpan? readyTimeout = null)
    {
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.processFactory = processFactory ?? new SystemPlaytestProcessFactory();
        this.readyTimeout = readyTimeout ?? DefaultReadyTimeout;
    }

    public bool IsRunning => server is not null || client is not null;

    public async Task LaunchAsync(EditorPlaytestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        launchCancellation?.Cancel();
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            StopProcesses();
            artifacts?.Dispose();
            artifacts = request.Artifacts;
            launchCancellation?.Dispose();
            launchCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverExited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            server = CreateServer(request);
            server.OutputReceived += (line, error) =>
            {
                log($"[server{(error ? ":stderr" : string.Empty)}] {line}");
                if (!error && line.StartsWith(ServerReadyMarker, StringComparison.Ordinal))
                    ready.TrySetResult();
            };
            server.Exited += exitCode => serverExited.TrySetResult(exitCode);
            server.Start();
            log("Starting playtest server.");

            Task completed = await Task.WhenAny(
                ready.Task,
                serverExited.Task,
                Task.Delay(readyTimeout, launchCancellation.Token)).ConfigureAwait(false);
            if (completed == serverExited.Task)
                throw new InvalidOperationException($"Playtest server exited before readiness with code {await serverExited.Task.ConfigureAwait(false)}.");
            if (completed != ready.Task)
                throw new TimeoutException($"Playtest server did not become ready within {readyTimeout.TotalSeconds:0} seconds.");
            if (server.HasExited)
                throw new InvalidOperationException("Playtest server exited immediately after reporting readiness.");

            client = CreateClient(request);
            client.OutputReceived += (line, error) =>
                log($"[client{(error ? ":stderr" : string.Empty)}] {line}");
            client.Exited += exitCode => _ = HandleUnexpectedExitAsync("client", exitCode);
            server.Exited += exitCode => _ = HandleUnexpectedExitAsync("server", exitCode);
            client.Start();
            launchCancellation.Dispose();
            launchCancellation = null;
            log("Playtest client started.");
        }
        catch (Exception exception)
        {
            StopProcesses();
            artifacts?.Dispose();
            artifacts = null;
            launchCancellation?.Dispose();
            launchCancellation = null;
            log($"Playtest launch failed: {exception.Message}");
            throw;
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async Task StopAsync()
    {
        launchCancellation?.Cancel();
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            StopProcesses();
            artifacts?.Dispose();
            artifacts = null;
            launchCancellation?.Dispose();
            launchCancellation = null;
            log("Playtest stopped.");
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task HandleUnexpectedExitAsync(string name, int exitCode)
    {
        if (stopping || disposed)
            return;

        log($"Playtest {name} exited unexpectedly with code {exitCode}; stopping the playtest pair.");
        await StopAsync().ConfigureAwait(false);
    }

    private IPlaytestProcess CreateServer(EditorPlaytestRequest request) =>
        processFactory.Create(new PlaytestProcessStartInfo(
            "server",
            Path.Combine(request.RepositoryRoot, "launch", "server.sh"),
            request.RepositoryRoot,
            [
                request.MapId,
                "--map-file", request.MapFile,
                "--asset-root", request.ServerAssetRoot,
                "--port", request.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ]));

    private IPlaytestProcess CreateClient(EditorPlaytestRequest request) =>
        processFactory.Create(new PlaytestProcessStartInfo(
            "client",
            Path.Combine(request.RepositoryRoot, "launch", "client-connected.sh"),
            request.RepositoryRoot,
            [
                "--connect", request.Host,
                "--port", request.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--map", request.MapId,
                "--map-file", request.MapFile,
                "--asset-root", request.ClientAssetRoot,
            ]));

    private void StopProcesses()
    {
        stopping = true;
        try
        {
            KillAndDispose(client);
            client = null;
            KillAndDispose(server);
            server = null;
        }
        finally
        {
            stopping = false;
        }
    }

    private static void KillAndDispose(IPlaytestProcess? process)
    {
        if (process is null)
            return;

        try
        {
            process.KillTree();
        }
        finally
        {
            process.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        shutdown.Cancel();
        launchCancellation?.Cancel();
        lifecycle.Wait();
        try
        {
            disposed = true;
            StopProcesses();
            artifacts?.Dispose();
            artifacts = null;
            launchCancellation?.Dispose();
            launchCancellation = null;
        }
        finally
        {
            lifecycle.Release();
        }
    }
}
