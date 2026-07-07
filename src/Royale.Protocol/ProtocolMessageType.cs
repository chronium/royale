namespace Royale.Protocol;

public enum ProtocolMessageType : byte
{
    ClientHello = 1,
    ServerAccept = 2,
    ServerReject = 3,
    ClientInput = 4,
    ServerSnapshot = 5,
    ServerEvent = 6,
    ClientDisconnect = 7,
    ServerDisconnect = 8,
}
