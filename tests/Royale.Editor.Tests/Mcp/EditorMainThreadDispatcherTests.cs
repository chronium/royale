using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Royale.Editor.Mcp;

namespace Royale.Editor.Tests.Mcp;

public sealed class EditorMainThreadDispatcherTests
{
    [Fact]
    public async Task ExecutesQueuedWorkInOrderAndPropagatesResults()
    {
        var dispatcher = new EditorMainThreadDispatcher();
        dispatcher.BindToCurrentThread();
        List<int> order = [];

        Task<int> first = QueueFromWorker(dispatcher, () =>
        {
            order.Add(1);
            return 10;
        });
        Task<int> second = QueueFromWorker(dispatcher, () =>
        {
            order.Add(2);
            return 20;
        });

        dispatcher.ExecutePending();

        Assert.Equal(10, await first);
        Assert.Equal(20, await second);
        Assert.Equal([1, 2], order);
    }

    [Fact]
    public async Task PropagatesExceptions()
    {
        var dispatcher = new EditorMainThreadDispatcher();
        dispatcher.BindToCurrentThread();
        Task<int> work = QueueFromWorker<int>(
            dispatcher,
            () => throw new InvalidOperationException("test failure"));

        dispatcher.ExecutePending();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => work);
        Assert.Equal("test failure", exception.Message);
    }

    [Fact]
    public async Task CancelsQueuedWorkWithoutExecutingIt()
    {
        var dispatcher = new EditorMainThreadDispatcher();
        dispatcher.BindToCurrentThread();
        using var cancellation = new CancellationTokenSource();
        bool executed = false;
        Task<int> work = QueueFromWorker(
            dispatcher,
            () =>
            {
                executed = true;
                return 1;
            },
            cancellation.Token);

        cancellation.Cancel();
        dispatcher.ExecutePending();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.False(executed);
    }

    [Fact]
    public async Task ExecutesReentrantMainThreadWorkImmediately()
    {
        var dispatcher = new EditorMainThreadDispatcher();
        dispatcher.BindToCurrentThread();

        int value = await dispatcher.DispatchAsync(() =>
            dispatcher.DispatchAsync(() => 42).GetAwaiter().GetResult());

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task StopFailsPendingAndFutureWork()
    {
        var dispatcher = new EditorMainThreadDispatcher();
        dispatcher.BindToCurrentThread();
        Task<int> pending = QueueFromWorker(dispatcher, () => 1);

        dispatcher.Stop();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => dispatcher.DispatchAsync(() => 2));
    }

    [Fact]
    public async Task QueuedWorkDoesNotRunContinuationsOnMainThread()
    {
        var dispatcher = new EditorMainThreadDispatcher();
        Task<int> work = dispatcher.DispatchAsync(() => 1);
        var continuationThread = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = work.ContinueWith(
            _ => continuationThread.SetResult(Environment.CurrentManagedThreadId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        int mainThreadId = 0;
        var mainThread = new Thread(() =>
        {
            mainThreadId = Environment.CurrentManagedThreadId;
            dispatcher.BindToCurrentThread();
            dispatcher.ExecutePending();
        });
        mainThread.Start();
        mainThread.Join();

        Assert.NotEqual(mainThreadId, await continuationThread.Task);
    }

    private static Task<T> QueueFromWorker<T>(
        EditorMainThreadDispatcher dispatcher,
        Func<T> operation,
        CancellationToken cancellationToken = default) =>
        QueueFromOtherThread(dispatcher, operation, cancellationToken);

    private static Task<T> QueueFromOtherThread<T>(
        EditorMainThreadDispatcher dispatcher,
        Func<T> operation,
        CancellationToken cancellationToken)
    {
        Task<T>? work = null;
        var thread = new Thread(() => work = dispatcher.DispatchAsync(operation, cancellationToken));
        thread.Start();
        thread.Join();
        return work!;
    }
}
