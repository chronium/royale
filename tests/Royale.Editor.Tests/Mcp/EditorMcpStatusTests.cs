using Royale.Editor.Mcp;

namespace Royale.Editor.Tests.Mcp;

public sealed class EditorMcpStatusTests
{
    [Fact]
    public void TracksAcceptedAndRejectedRequests()
    {
        var status = new EditorMcpStatus("http://127.0.0.1:51238/mcp");

        using (status.AcceptRequest())
        {
            EditorMcpStatusSnapshot active = status.Snapshot;
            Assert.Equal(1, active.ActiveRequests);
            Assert.Equal(1, active.TotalAcceptedRequests);
            Assert.NotNull(active.LastRequestActivityUtc);
        }

        status.RejectRequest("Rejected for test.");
        EditorMcpStatusSnapshot completed = status.Snapshot;
        Assert.Equal(0, completed.ActiveRequests);
        Assert.Equal(1, completed.TotalAcceptedRequests);
        Assert.Equal("Rejected for test.", completed.LastRejectedRequest?.Summary);
    }

    [Fact]
    public void TracksStateAndSanitizedError()
    {
        var status = new EditorMcpStatus("http://127.0.0.1:51238/mcp");

        status.SetState(EditorMcpServerState.Faulted, "Safe error message.");

        Assert.Equal(EditorMcpServerState.Faulted, status.Snapshot.State);
        Assert.Equal("Safe error message.", status.Snapshot.Error);
    }
}
