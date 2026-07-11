namespace Royale.Network.Transport;

public enum NetworkDisconnectReason
{
    ConnectionFailed,
    Timeout,
    HostUnreachable,
    NetworkUnreachable,
    RemoteConnectionClose,
    LocalDisconnect,
    ConnectionRejected,
    InvalidProtocol,
    UnknownHost,
    Reconnect,
    PeerToPeerConnection,
    PeerNotFound,
    Unknown,
}
