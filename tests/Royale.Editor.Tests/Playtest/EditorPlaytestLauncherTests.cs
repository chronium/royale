using Royale.Editor.Playtest;

namespace Royale.Editor.Tests.Playtest;

public sealed class EditorPlaytestLauncherTests
{
    [Fact]
    public async Task ReadyServerStartsClientWithUnsplitOverrideArgumentsAndCapturesLogs()
    {
        var server = new FakeProcess(onStart: process =>
        {
            process.Emit("server output", error: false);
            process.Emit(EditorPlaytestLauncher.ServerReadyMarker + " map=test port=7777", error: false);
        });
        var client = new FakeProcess(onStart: process => process.Emit("client error", error: true));
        var factory = new FakeProcessFactory(server, client);
        var logs = new List<string>();
        using var launcher = new EditorPlaytestLauncher(logs.Add, factory, TimeSpan.FromSeconds(1));
        string root = Path.Combine(Path.GetTempPath(), "repo with spaces");

        await launcher.LaunchAsync(Request(root));

        Assert.True(client.Started);
        Assert.Collection(
            factory.StartInfos,
            serverInfo =>
            {
                Assert.Equal(Path.Combine(root, "launch", "server.sh"), serverInfo.FileName);
                Assert.Contains("/tmp/map with spaces.json", serverInfo.Arguments);
                Assert.Contains("/tmp/server assets", serverInfo.Arguments);
            },
            clientInfo =>
            {
                Assert.Equal(Path.Combine(root, "launch", "client-connected.sh"), clientInfo.FileName);
                Assert.Contains("/tmp/client assets", clientInfo.Arguments);
            });
        Assert.Contains("[server] server output", logs);
        Assert.Contains("[client:stderr] client error", logs);
    }

    [Fact]
    public async Task ReadinessTimeoutStopsServerAndDisposesArtifacts()
    {
        var server = new FakeProcess();
        var factory = new FakeProcessFactory(server);
        var artifacts = new TrackingDisposable();
        using var launcher = new EditorPlaytestLauncher(_ => { }, factory, TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<TimeoutException>(() => launcher.LaunchAsync(Request(artifacts: artifacts)));

        Assert.True(server.Killed);
        Assert.True(server.Disposed);
        Assert.True(artifacts.Disposed);
    }

    [Fact]
    public async Task EarlyServerExitStopsPairAndReportsExitCode()
    {
        var server = new FakeProcess(onStart: process => process.Exit(7));
        var factory = new FakeProcessFactory(server);
        using var launcher = new EditorPlaytestLauncher(_ => { }, factory, TimeSpan.FromSeconds(1));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => launcher.LaunchAsync(Request()));

        Assert.Contains("code 7", exception.Message);
        Assert.True(server.Disposed);
    }

    [Fact]
    public async Task RelaunchReplacesPairAndStopCleansCurrentArtifacts()
    {
        FakeProcess ReadyServer() => new(onStart: process =>
            process.Emit(EditorPlaytestLauncher.ServerReadyMarker, error: false));
        var firstServer = ReadyServer();
        var firstClient = new FakeProcess();
        var secondServer = ReadyServer();
        var secondClient = new FakeProcess();
        var firstArtifacts = new TrackingDisposable();
        var secondArtifacts = new TrackingDisposable();
        var factory = new FakeProcessFactory(firstServer, firstClient, secondServer, secondClient);
        using var launcher = new EditorPlaytestLauncher(_ => { }, factory, TimeSpan.FromSeconds(1));

        await launcher.LaunchAsync(Request(artifacts: firstArtifacts));
        await launcher.LaunchAsync(Request(artifacts: secondArtifacts));

        Assert.True(firstServer.Killed);
        Assert.True(firstClient.Killed);
        Assert.True(firstArtifacts.Disposed);
        Assert.False(secondArtifacts.Disposed);

        await launcher.StopAsync();
        Assert.True(secondServer.Killed);
        Assert.True(secondClient.Killed);
        Assert.True(secondArtifacts.Disposed);
    }

    [Fact]
    public async Task UnexpectedClientExitStopsCompletePair()
    {
        var server = new FakeProcess(onStart: process =>
            process.Emit(EditorPlaytestLauncher.ServerReadyMarker, error: false));
        var client = new FakeProcess();
        var factory = new FakeProcessFactory(server, client);
        var logs = new List<string>();
        using var launcher = new EditorPlaytestLauncher(logs.Add, factory, TimeSpan.FromSeconds(1));
        await launcher.LaunchAsync(Request());

        client.Exit(3);
        await WaitUntilAsync(() => server.Killed);

        Assert.True(server.Killed);
        Assert.Contains(logs, line => line.Contains("client exited unexpectedly with code 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisposeDuringReadinessWaitStopsServerAndCleansArtifacts()
    {
        var server = new FakeProcess();
        var artifacts = new TrackingDisposable();
        var launcher = new EditorPlaytestLauncher(
            _ => { },
            new FakeProcessFactory(server),
            TimeSpan.FromSeconds(30));

        Task launch = launcher.LaunchAsync(Request(artifacts: artifacts));
        await WaitUntilAsync(() => server.Started);
        launcher.Dispose();
        await Assert.ThrowsAnyAsync<Exception>(() => launch);

        Assert.True(server.Killed);
        Assert.True(artifacts.Disposed);
    }

    [Fact]
    public async Task StopDuringReadinessWaitCancelsStartupImmediately()
    {
        var server = new FakeProcess();
        var launcher = new EditorPlaytestLauncher(
            _ => { },
            new FakeProcessFactory(server),
            TimeSpan.FromSeconds(30));
        Task launch = launcher.LaunchAsync(Request());
        await WaitUntilAsync(() => server.Started);

        await launcher.StopAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => launch);

        Assert.True(server.Killed);
        launcher.Dispose();
    }

    private static EditorPlaytestRequest Request(string repositoryRoot = "/tmp/repo", IDisposable? artifacts = null) =>
        new(
            repositoryRoot,
            "test-map",
            "/tmp/map with spaces.json",
            "/tmp/client assets",
            "/tmp/server assets",
            "127.0.0.1",
            7777,
            artifacts);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(5);
        Assert.True(condition());
    }

    private sealed class FakeProcessFactory(params FakeProcess[] processes) : IPlaytestProcessFactory
    {
        private readonly Queue<FakeProcess> pending = new(processes);

        public List<PlaytestProcessStartInfo> StartInfos { get; } = [];

        public IPlaytestProcess Create(PlaytestProcessStartInfo startInfo)
        {
            StartInfos.Add(startInfo);
            return pending.Dequeue();
        }
    }

    private sealed class FakeProcess(Action<FakeProcess>? onStart = null) : IPlaytestProcess
    {
        public event Action<string, bool>? OutputReceived;

        public event Action<int>? Exited;

        public bool HasExited { get; private set; }

        public bool Started { get; private set; }

        public bool Killed { get; private set; }

        public bool Disposed { get; private set; }

        public void Start()
        {
            Started = true;
            onStart?.Invoke(this);
        }

        public void Emit(string line, bool error) => OutputReceived?.Invoke(line, error);

        public void Exit(int exitCode)
        {
            HasExited = true;
            Exited?.Invoke(exitCode);
        }

        public void KillTree()
        {
            Killed = true;
            HasExited = true;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
