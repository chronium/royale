using LiteNetLib;

namespace Royale.Network.Transport;

internal static class NetworkDisconnectReasonMappings
{
    public static NetworkDisconnectReason ToNetworkDisconnectReason(this DisconnectReason reason) =>
        reason switch
        {
            DisconnectReason.ConnectionFailed => NetworkDisconnectReason.ConnectionFailed,
            DisconnectReason.Timeout => NetworkDisconnectReason.Timeout,
            DisconnectReason.HostUnreachable => NetworkDisconnectReason.HostUnreachable,
            DisconnectReason.NetworkUnreachable => NetworkDisconnectReason.NetworkUnreachable,
            DisconnectReason.RemoteConnectionClose => NetworkDisconnectReason.RemoteConnectionClose,
            DisconnectReason.DisconnectPeerCalled => NetworkDisconnectReason.LocalDisconnect,
            DisconnectReason.ConnectionRejected => NetworkDisconnectReason.ConnectionRejected,
            DisconnectReason.InvalidProtocol => NetworkDisconnectReason.InvalidProtocol,
            DisconnectReason.UnknownHost => NetworkDisconnectReason.UnknownHost,
            DisconnectReason.Reconnect => NetworkDisconnectReason.Reconnect,
            DisconnectReason.PeerToPeerConnection => NetworkDisconnectReason.PeerToPeerConnection,
            DisconnectReason.PeerNotFound => NetworkDisconnectReason.PeerNotFound,
            _ => NetworkDisconnectReason.Unknown,
        };
}
