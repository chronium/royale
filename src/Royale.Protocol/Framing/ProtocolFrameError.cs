namespace Royale.Protocol.Framing;

public enum ProtocolFrameError
{
    None,
    DestinationTooSmall,
    PacketTooShort,
    InvalidMagic,
    UnsupportedMajorVersion,
    InvalidMessageType,
}
