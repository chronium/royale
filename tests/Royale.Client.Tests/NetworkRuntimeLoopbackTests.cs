using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Royale.Client.Networking;
using Royale.Content;
using Royale.Network;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation.Movement;

namespace Royale.Client.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class NetworkRuntimeLoopbackTests
{
    [Fact]
    public async Task ClientInputMovesAuthoritativePlayerOverUdpRuntime()
    {
        int serverPort = ReserveUdpPort();
        using LiteNetLibNetworkTransport serverTransport = new();
        using LiteNetLibNetworkTransport clientTransport = new();
        serverTransport.Start(serverPort);
        clientTransport.Start(0);
        GameMap map = CreateOpenArenaMap();
        using var server = new NetworkServerRuntime(
            serverTransport,
            InProcessServerSession.Create(map));
        using var client = new NetworkClientRuntime(
            clientTransport,
            new NetworkEndpoint("127.0.0.1", serverPort),
            loadPredictionMap: requestedMapId => requestedMapId == map.Id
                ? map
                : throw new InvalidOperationException($"Unexpected map id '{requestedMapId}'."));

        await PumpUntilAsync(
            server,
            client,
            () => client.Accepted && client.State.TryGetLocalPlayer(out _));
        Assert.True(client.State.TryGetLocalPlayer(out PlayerSnapshotState initialPlayer));
        Assert.True(client.PredictionActive);
        Assert.True(client.RemoteSnapshotBufferCount > 0);
        Assert.Equal(RemoteSnapshotInterpolator.DefaultInterpolationDelayTicks, client.RemoteInterpolationDelayTicks);

        var moveForward = new PlayerInputSample(
            new Vector2(0.0f, 1.0f),
            Jump: false,
            Fire: false,
            LookDelta: Vector2.Zero);

        Assert.True(client.FixedUpdate(moveForward, clientTick: 1));
        Assert.True(client.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedPlayer));
        Assert.True(
            Vector3.Distance(
                new Vector3(predictedPlayer.Position.X, 0.0f, predictedPlayer.Position.Z),
                new Vector3(initialPlayer.Position.X, 0.0f, initialPlayer.Position.Z)) > 0.05f);

        for (ulong tick = 2; tick <= 30; tick++)
        {
            Assert.True(client.FixedUpdate(moveForward, tick));
            client.Poll();
            server.Step();
            await Task.Delay(2);
        }

        await PumpUntilAsync(
            server,
            client,
            () =>
            {
                if (!client.State.TryGetLocalPlayer(out PlayerSnapshotState movedPlayer))
                    return false;

                return client.State.AcknowledgedInputSequence is not null &&
                    Vector3.Distance(
                        new Vector3(movedPlayer.Position.X, 0.0f, movedPlayer.Position.Z),
                        new Vector3(initialPlayer.Position.X, 0.0f, initialPlayer.Position.Z)) > 0.05f;
            });

        Assert.True(client.State.TryGetLocalPlayer(out PlayerSnapshotState finalPlayer));
        Assert.NotNull(client.State.AcknowledgedInputSequence);
        Assert.True(
            Vector3.Distance(
                new Vector3(finalPlayer.Position.X, 0.0f, finalPlayer.Position.Z),
                new Vector3(initialPlayer.Position.X, 0.0f, initialPlayer.Position.Z)) > 0.05f);

        PlayerInputSample crouch = moveForward with { Crouch = true };
        Assert.True(client.FixedUpdate(crouch, clientTick: 31));
        Assert.True(client.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedCrouch));
        Assert.True(predictedCrouch.Crouched);

        await PumpUntilAsync(
            server,
            client,
            () => client.State.TryGetLocalPlayer(out PlayerSnapshotState authoritativeCrouch) &&
                authoritativeCrouch.Crouched);

        Assert.True(client.State.TryGetLocalPlayer(out PlayerSnapshotState crouchedPlayer));
        Assert.True(crouchedPlayer.Crouched);
    }

    private static async Task PumpUntilAsync(
        NetworkServerRuntime server,
        NetworkClientRuntime client,
        Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            server.Step();
            client.Poll();
            await Task.Delay(10, timeout.Token);
        }
    }

    private static int ReserveUdpPort()
    {
        using UdpClient udpClient = new(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    private static GameMap CreateOpenArenaMap() => new()
    {
        Id = "network-runtime-loopback",
        Name = "Network Runtime Loopback",
        SpawnPoints =
        [
            new MapSpawnPoint
            {
                Id = "spawn-a",
                Position = new MapVector3(0.0f, 0.0f, 0.0f),
            },
        ],
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "floor",
                Position = new MapVector3(0.0f, -0.1f, 0.0f),
                Size = new MapVector3(30.0f, 0.2f, 30.0f),
            },
        ],
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = 50.0f,
        },
    };
}
