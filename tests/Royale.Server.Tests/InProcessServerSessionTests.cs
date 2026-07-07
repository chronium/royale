using System.Numerics;
using Royale.Content;
using Royale.Protocol;
using Royale.Server;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class InProcessServerSessionTests
{
    [Fact]
    public void ConnectClientCreatesAuthoritativePlayerAndInitialSnapshot()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);

        InProcessClientConnection client = session.ConnectClient();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(ContentCatalog.DefaultMapId, session.MapId);
        Assert.Equal(new ServerConnectionId(1), client.ConnectionId);
        Assert.Equal(new ServerPlayerId(1), client.PlayerId);
        Assert.Equal(1, session.ConnectedClientCount);
        Assert.Equal(1, session.ActivePlayerCount);
        Assert.Equal(0UL, snapshot.ServerTick);
        Assert.Equal(client.PlayerId.Value, snapshot.LocalPlayerId);
        Assert.Null(snapshot.AcknowledgedInputSequence);
        Assert.Equal(1, snapshot.Match.LivingPlayerCount);
        Assert.Equal(client.PlayerId.Value, Assert.Single(snapshot.Players).PlayerId);
        Assert.False(session.TryDequeueSnapshot(client, out _));
    }

    [Fact]
    public void StepProducesSnapshotWithAdvancedServerTick()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(1UL, session.CurrentTick);
        Assert.Equal(1UL, snapshot.ServerTick);
        Assert.Equal(client.PlayerId.Value, snapshot.LocalPlayerId);
    }

    [Fact]
    public void ValidQueuedCommandUpdatesTopLevelAcknowledgement()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);

        Assert.True(session.TryEnqueueInputCommand(client, ValidCommand(sequence: 7)));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(7U, snapshot.AcknowledgedInputSequence);
    }

    [Fact]
    public void InvalidQueuedCommandIsRejectedAndNotAcknowledged()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);
        PlayerInputCommand invalidCommand = ValidCommand(sequence: 9) with
        {
            Move = new Vector2(2.0f, 0.0f),
        };

        Assert.False(session.TryEnqueueInputCommand(client, invalidCommand));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Null(snapshot.AcknowledgedInputSequence);
    }

    [Fact]
    public void TwoClientsReceiveSeparateLocalPlayerIdsAndAcknowledgements()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        _ = session.DrainSnapshots(second);

        Assert.True(session.TryEnqueueInputCommand(first, ValidCommand(sequence: 10)));
        Assert.True(session.TryEnqueueInputCommand(second, ValidCommand(sequence: 20)));

        session.Step();
        ServerSnapshot firstSnapshot = DequeueSnapshot(session, first);
        ServerSnapshot secondSnapshot = DequeueSnapshot(session, second);

        Assert.Equal(new ServerPlayerId(1), first.PlayerId);
        Assert.Equal(new ServerPlayerId(2), second.PlayerId);
        Assert.Equal(first.PlayerId.Value, firstSnapshot.LocalPlayerId);
        Assert.Equal(second.PlayerId.Value, secondSnapshot.LocalPlayerId);
        Assert.Equal(10U, firstSnapshot.AcknowledgedInputSequence);
        Assert.Equal(20U, secondSnapshot.AcknowledgedInputSequence);
        Assert.Equal(2, firstSnapshot.Players.Count);
        Assert.Equal(2, secondSnapshot.Players.Count);
    }

    [Fact]
    public void DisconnectRemovesAuthoritativePlayerAndRejectsFurtherUseOfHandle()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();

        session.DisconnectClient(client);

        Assert.Equal(0, session.ConnectedClientCount);
        Assert.Equal(0, session.ActivePlayerCount);
        Assert.Throws<InvalidOperationException>(
            () => session.TryEnqueueInputCommand(client, ValidCommand(sequence: 1)));
        Assert.Throws<InvalidOperationException>(
            () => session.TryDequeueSnapshot(client, out _));
    }

    [Fact]
    public void DisposeDisposesWrappedSimulationAndRejectsOperations()
    {
        InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();

        session.Dispose();

        Assert.True(session.IsDisposed);
        Assert.True(session.IsSimulationDisposed);
        Assert.Throws<ObjectDisposedException>(() => session.ConnectClient());
        Assert.Throws<ObjectDisposedException>(
            () => session.TryEnqueueInputCommand(client, ValidCommand(sequence: 1)));
        Assert.Throws<ObjectDisposedException>(() => session.Step());
        Assert.Throws<ObjectDisposedException>(() => session.DisconnectClient(client));
    }

    private static ServerSnapshot DequeueSnapshot(
        InProcessServerSession session,
        InProcessClientConnection client)
    {
        Assert.True(session.TryDequeueSnapshot(client, out ServerSnapshot? snapshot));
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);
}
