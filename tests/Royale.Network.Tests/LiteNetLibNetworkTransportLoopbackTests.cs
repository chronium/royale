using System.Net;
using System.Net.Sockets;
using Royale.Network;

namespace Royale.Network.Tests;

public sealed class LiteNetLibNetworkTransportLoopbackTests
{
    [Fact]
    public async Task MovesPacketsAndDisconnectEventsOverLoopback()
    {
        int serverPort = ReserveUdpPort();
        using LiteNetLibNetworkTransport server = new();
        using LiteNetLibNetworkTransport client = new();
        RecordingNetworkEventHandler serverEvents = new();
        RecordingNetworkEventHandler clientEvents = new();

        server.Start(serverPort);
        client.Start(0);

        NetworkPeerId clientPeer = client.Connect(new NetworkEndpoint("127.0.0.1", serverPort));

        await PumpUntilAsync(
            server,
            serverEvents,
            client,
            clientEvents,
            () => serverEvents.ConnectedPeers.Count == 1 && clientEvents.ConnectedPeers.Count == 1);

        NetworkPeerId serverPeer = serverEvents.ConnectedPeers.Single();
        byte[] clientPacket = [1, 2, 3, 4];
        byte[] serverPacket = [5, 6, 7];

        client.Send(clientPeer, clientPacket, NetworkDelivery.Unreliable);
        await PumpUntilAsync(
            server,
            serverEvents,
            client,
            clientEvents,
            () => serverEvents.Packets.Count == 1);

        ReceivedPacket receivedFromClient = serverEvents.Packets.Single();
        Assert.Equal(serverPeer, receivedFromClient.PeerId);
        Assert.Equal(NetworkDelivery.Unreliable, receivedFromClient.Delivery);
        Assert.Equal(0, receivedFromClient.Channel);
        Assert.Equal(clientPacket, receivedFromClient.Payload);

        server.Send(serverPeer, serverPacket, NetworkDelivery.ReliableOrdered, channel: 1);
        await PumpUntilAsync(
            server,
            serverEvents,
            client,
            clientEvents,
            () => clientEvents.Packets.Count == 1);

        ReceivedPacket receivedFromServer = clientEvents.Packets.Single();
        Assert.Equal(clientPeer, receivedFromServer.PeerId);
        Assert.Equal(NetworkDelivery.ReliableOrdered, receivedFromServer.Delivery);
        Assert.Equal(1, receivedFromServer.Channel);
        Assert.Equal(serverPacket, receivedFromServer.Payload);

        client.Disconnect(clientPeer);
        await PumpUntilAsync(
            server,
            serverEvents,
            client,
            clientEvents,
            () => serverEvents.DisconnectedPeers.Count == 1 || clientEvents.DisconnectedPeers.Count == 1);

        Assert.True(serverEvents.DisconnectedPeers.Count == 1 || clientEvents.DisconnectedPeers.Count == 1);
    }

    private static int ReserveUdpPort()
    {
        using UdpClient udpClient = new(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    private static async Task PumpUntilAsync(
        LiteNetLibNetworkTransport server,
        RecordingNetworkEventHandler serverEvents,
        LiteNetLibNetworkTransport client,
        RecordingNetworkEventHandler clientEvents,
        Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            server.Poll(serverEvents);
            client.Poll(clientEvents);
            await Task.Delay(10, timeout.Token);
        }
    }
}
