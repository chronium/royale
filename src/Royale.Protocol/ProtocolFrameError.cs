namespace Royale.Protocol;

public enum ProtocolFrameError
{
    None,
    DestinationTooSmall,
    PacketTooShort,
    InvalidMagic,
    UnsupportedMajorVersion,
    InvalidMessageType,
}
