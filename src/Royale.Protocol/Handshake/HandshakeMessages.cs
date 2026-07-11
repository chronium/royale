namespace Royale.Protocol.Handshake;

public sealed record ClientHello(
    string BuildId,
    string ContentVersion);

public sealed record ServerAccept(
    ulong SessionId,
    uint ConnectionId,
    uint PlayerId,
    ulong ServerTick,
    string MapId);

public sealed record ServerReject(
    ServerRejectReason Reason,
    string Detail);

public enum ServerRejectReason : byte
{
    MalformedPacket = 1,
    UnexpectedMessageType = 2,
    UnsupportedProtocolVersion = 3,
    IncompatibleBuild = 4,
    IncompatibleContent = 5,
    AcceptFailed = 6,
    MatchUnavailable = 7,
}
