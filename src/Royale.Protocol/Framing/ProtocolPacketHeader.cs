namespace Royale.Protocol.Framing;

public readonly record struct ProtocolPacketHeader(
    ushort MajorVersion,
    ushort MinorVersion,
    ulong SessionId,
    ProtocolMessageType MessageType,
    uint Sequence,
    uint AcknowledgedSequence,
    uint AcknowledgementMask)
{
    public static ProtocolPacketHeader Create(
        ulong sessionId,
        ProtocolMessageType messageType,
        uint sequence,
        uint acknowledgedSequence,
        uint acknowledgementMask) => new(
            ProtocolConstants.ProtocolMajorVersion,
            ProtocolConstants.ProtocolMinorVersion,
            sessionId,
            messageType,
            sequence,
            acknowledgedSequence,
            acknowledgementMask);
}
