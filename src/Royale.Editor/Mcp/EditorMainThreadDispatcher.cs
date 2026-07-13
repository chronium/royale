using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Royale.Editor.Mcp;

public sealed class EditorMainThreadDispatcher
{
    private readonly object sync = new();
    private readonly Queue<WorkItem> pending = [];
    private int mainThreadId;
    private bool stopped;

    public void BindToCurrentThread()
    {
        int currentThreadId = Environment.CurrentManagedThreadId;
        lock (sync)
        {
            if (mainThreadId != 0 && mainThreadId != currentThreadId)
                throw new InvalidOperationException("The editor dispatcher is already bound to another thread.");

            mainThreadId = currentThreadId;
        }
    }

    public Task<T> DispatchAsync<T>(Func<T> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var item = new WorkItem<T>(operation, cancellationToken);
        bool executeNow;
        bool reject;
        lock (sync)
        {
            reject = stopped;
            executeNow = mainThreadId != 0 && Environment.CurrentManagedThreadId == mainThreadId;
            if (!reject && !executeNow)
                pending.Enqueue(item);
        }

        if (reject)
            item.Fail(CreateStoppedException());
        else if (executeNow)
            item.Execute();

        return item.Task;
    }

    public void ExecutePending()
    {
        EnsureMainThread();

        while (true)
        {
            WorkItem? item;
            lock (sync)
            {
                if (!pending.TryDequeue(out item))
                    return;
            }

            item.Execute();
        }
    }

    public void Stop()
    {
        WorkItem[] abandoned;
        lock (sync)
        {
            if (stopped)
                return;

            stopped = true;
            abandoned = [.. pending];
            pending.Clear();
        }

        ObjectDisposedException exception = CreateStoppedException();
        foreach (WorkItem item in abandoned)
            item.Fail(exception);
    }

    private void EnsureMainThread()
    {
        if (mainThreadId == 0)
            throw new InvalidOperationException("The editor dispatcher has not been bound to its main thread.");
        if (Environment.CurrentManagedThreadId != mainThreadId)
            throw new InvalidOperationException("Pending editor work can only execute on the editor main thread.");
    }

    private static ObjectDisposedException CreateStoppedException() =>
        new(nameof(EditorMainThreadDispatcher), "The editor is shutting down and cannot execute queued MCP work.");

    private abstract class WorkItem
    {
        public abstract void Execute();
        public abstract void Fail(Exception exception);
    }

    private sealed class WorkItem<T> : WorkItem
    {
        private readonly Func<T> operation;
        private readonly CancellationToken cancellationToken;
        private readonly TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration cancellationRegistration;

        public WorkItem(Func<T> operation, CancellationToken cancellationToken)
        {
            this.operation = operation;
            this.cancellationToken = cancellationToken;
            cancellationRegistration = cancellationToken.Register(
                static state => ((WorkItem<T>)state!).Cancel(),
                this);
        }

        public Task<T> Task => completion.Task;

        public override void Execute()
        {
            if (completion.Task.IsCompleted)
            {
                cancellationRegistration.Dispose();
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                cancellationRegistration.Dispose();
            }
        }

        public override void Fail(Exception exception)
        {
            completion.TrySetException(exception);
            cancellationRegistration.Dispose();
        }

        private void Cancel() => completion.TrySetCanceled(cancellationToken);
    }
}
