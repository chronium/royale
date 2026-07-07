namespace Royale.Protocol;

public static class ProtocolConstants
{
    public const int Version = 1;
    public const ushort ProtocolMajorVersion = 1;
    public const ushort ProtocolMinorVersion = 0;
    public const uint PacketMagic = 0x4C594F52;
    public const int PacketHeaderSize = 29;
    public const int DefaultPort = 7777;
    public const string BuildId = "dev-build";
    public const string ContentVersion = "dev-content-1";
    public const int MaxBuildIdLength = 64;
    public const int MaxContentVersionLength = 64;
    public const int MaxMapIdLength = 64;
    public const int MaxRejectDetailLength = 128;
    public const int MaxSnapshotPlayers = 128;
    public const int MaxSnapshotWeaponIdLength = 64;
}
