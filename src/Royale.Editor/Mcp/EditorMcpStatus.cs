using System;
using System.Threading;

namespace Royale.Editor.Mcp;

public enum EditorMcpServerState
{
    Listening,
    Stopping,
    Stopped,
    Faulted,
}

public sealed record EditorMcpRequestActivity(DateTimeOffset TimestampUtc, string Summary);

public sealed record EditorMcpStatusSnapshot(
    EditorMcpServerState State,
    string Endpoint,
    int ActiveRequests,
    long TotalAcceptedRequests,
    DateTimeOffset? LastRequestActivityUtc,
    EditorMcpRequestActivity? LastRejectedRequest,
    string? Error);

public sealed class EditorMcpStatus(string endpoint)
{
    private readonly object sync = new();
    private EditorMcpServerState state = EditorMcpServerState.Stopped;
    private int activeRequests;
    private long totalAcceptedRequests;
    private DateTimeOffset? lastRequestActivityUtc;
    private EditorMcpRequestActivity? lastRejectedRequest;
    private string? error;

    public EditorMcpStatusSnapshot Snapshot
    {
        get
        {
            lock (sync)
            {
                return new(
                    state,
                    endpoint,
                    activeRequests,
                    totalAcceptedRequests,
                    lastRequestActivityUtc,
                    lastRejectedRequest,
                    error);
            }
        }
    }

    public IDisposable AcceptRequest()
    {
        lock (sync)
        {
            activeRequests++;
            totalAcceptedRequests++;
            lastRequestActivityUtc = DateTimeOffset.UtcNow;
        }

        return new AcceptedRequest(this);
    }

    public void RejectRequest(string summary)
    {
        lock (sync)
            lastRejectedRequest = new(DateTimeOffset.UtcNow, summary);
    }

    public void SetState(EditorMcpServerState value, string? sanitizedError = null)
    {
        lock (sync)
        {
            state = value;
            error = sanitizedError;
        }
    }

    private void CompleteRequest()
    {
        lock (sync)
        {
            activeRequests--;
            lastRequestActivityUtc = DateTimeOffset.UtcNow;
        }
    }

    private sealed class AcceptedRequest(EditorMcpStatus owner) : IDisposable
    {
        private EditorMcpStatus? status = owner;

        public void Dispose() => Interlocked.Exchange(ref status, null)?.CompleteRequest();
    }
}
