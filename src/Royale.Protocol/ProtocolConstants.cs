namespace Royale.Protocol;

public static class ProtocolConstants
{
    public const int Version = 1;
    public const ushort ProtocolMajorVersion = 1;
    public const ushort ProtocolMinorVersion = 0;
    public const uint PacketMagic = 0x4C594F52;
    public const int PacketHeaderSize = 29;
    public const int DefaultPort = 7777;
}
