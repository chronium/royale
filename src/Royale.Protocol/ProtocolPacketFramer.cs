using System.Buffers.Binary;

namespace Royale.Protocol;

public static class ProtocolPacketFramer
{
    private const int MagicOffset = 0;
    private const int MajorVersionOffset = MagicOffset + sizeof(uint);
    private const int MinorVersionOffset = MajorVersionOffset + sizeof(ushort);
    private const int SessionIdOffset = MinorVersionOffset + sizeof(ushort);
    private const int MessageTypeOffset = SessionIdOffset + sizeof(ulong);
    private const int SequenceOffset = MessageTypeOffset + sizeof(byte);
    private const int AcknowledgedSequenceOffset = SequenceOffset + sizeof(uint);
    private const int AcknowledgementMaskOffset = AcknowledgedSequenceOffset + sizeof(uint);

    public static bool TryWriteHeader(
        ProtocolPacketHeader header,
        Span<byte> destination,
        out int bytesWritten,
        out ProtocolFrameError error)
    {
        bytesWritten = 0;

        if (destination.Length < ProtocolConstants.PacketHeaderSize)
        {
            error = ProtocolFrameError.DestinationTooSmall;
            return false;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(
            destination.Slice(MagicOffset, sizeof(uint)),
            ProtocolConstants.PacketMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(
            destination.Slice(MajorVersionOffset, sizeof(ushort)),
            header.MajorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(
            destination.Slice(MinorVersionOffset, sizeof(ushort)),
            header.MinorVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(
            destination.Slice(SessionIdOffset, sizeof(ulong)),
            header.SessionId);
        destination[MessageTypeOffset] = (byte)header.MessageType;
        BinaryPrimitives.WriteUInt32LittleEndian(
            destination.Slice(SequenceOffset, sizeof(uint)),
            header.Sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(
            destination.Slice(AcknowledgedSequenceOffset, sizeof(uint)),
            header.AcknowledgedSequence);
        BinaryPrimitives.WriteUInt32LittleEndian(
            destination.Slice(AcknowledgementMaskOffset, sizeof(uint)),
            header.AcknowledgementMask);

        bytesWritten = ProtocolConstants.PacketHeaderSize;
        error = ProtocolFrameError.None;
        return true;
    }

    public static bool TryReadHeader(
        ReadOnlySpan<byte> source,
        out ProtocolPacketHeader header,
        out ProtocolFrameError error)
    {
        header = default;

        if (source.Length < ProtocolConstants.PacketHeaderSize)
        {
            error = ProtocolFrameError.PacketTooShort;
            return false;
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(MagicOffset, sizeof(uint)));
        if (magic != ProtocolConstants.PacketMagic)
        {
            error = ProtocolFrameError.InvalidMagic;
            return false;
        }

        ushort majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(MajorVersionOffset, sizeof(ushort)));
        if (majorVersion != ProtocolConstants.ProtocolMajorVersion)
        {
            error = ProtocolFrameError.UnsupportedMajorVersion;
            return false;
        }

        byte messageTypeValue = source[MessageTypeOffset];
        if (!Enum.IsDefined(typeof(ProtocolMessageType), messageTypeValue))
        {
            error = ProtocolFrameError.InvalidMessageType;
            return false;
        }

        header = new ProtocolPacketHeader(
            majorVersion,
            BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(MinorVersionOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(SessionIdOffset, sizeof(ulong))),
            (ProtocolMessageType)messageTypeValue,
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(SequenceOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(AcknowledgedSequenceOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(AcknowledgementMaskOffset, sizeof(uint))));

        error = ProtocolFrameError.None;
        return true;
    }

    public static bool TryWritePacket(
        ProtocolPacketHeader header,
        ReadOnlySpan<byte> payload,
        Span<byte> destination,
        out int bytesWritten,
        out ProtocolFrameError error)
    {
        bytesWritten = 0;
        int packetSize = ProtocolConstants.PacketHeaderSize + payload.Length;

        if (destination.Length < packetSize)
        {
            error = ProtocolFrameError.DestinationTooSmall;
            return false;
        }

        if (!TryWriteHeader(header, destination, out int headerBytesWritten, out error))
        {
            return false;
        }

        payload.CopyTo(destination.Slice(headerBytesWritten, payload.Length));
        bytesWritten = packetSize;
        error = ProtocolFrameError.None;
        return true;
    }

    public static bool TryReadPacket(
        ReadOnlySpan<byte> packet,
        out ProtocolPacketHeader header,
        out ReadOnlySpan<byte> payload,
        out ProtocolFrameError error)
    {
        payload = default;

        if (!TryReadHeader(packet, out header, out error))
        {
            return false;
        }

        payload = packet[ProtocolConstants.PacketHeaderSize..];
        error = ProtocolFrameError.None;
        return true;
    }

    public static bool IsSequenceAcknowledged(
        uint sequence,
        uint acknowledgedSequence,
        uint acknowledgementMask)
    {
        if (sequence == acknowledgedSequence)
        {
            return true;
        }

        uint distanceBehind = unchecked(acknowledgedSequence - sequence);
        if (distanceBehind is 0 or > 32)
        {
            return false;
        }

        uint bit = 1u << ((int)distanceBehind - 1);
        return (acknowledgementMask & bit) != 0;
    }
}
