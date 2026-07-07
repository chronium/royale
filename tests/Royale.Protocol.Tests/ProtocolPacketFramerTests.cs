using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ProtocolPacketFramerTests
{
    [Fact]
    public void HeaderWritesAndReadsRoundTrip()
    {
        var header = new ProtocolPacketHeader(
            MajorVersion: 1,
            MinorVersion: 0,
            SessionId: 123,
            MessageType: ProtocolMessageType.ClientInput,
            Sequence: 42,
            AcknowledgedSequence: 41,
            AcknowledgementMask: 0b101);
        Span<byte> buffer = stackalloc byte[ProtocolConstants.PacketHeaderSize];

        bool wrote = ProtocolPacketFramer.TryWriteHeader(
            header,
            buffer,
            out int bytesWritten,
            out ProtocolFrameError writeError);
        bool read = ProtocolPacketFramer.TryReadHeader(
            buffer,
            out ProtocolPacketHeader readHeader,
            out ProtocolFrameError readError);

        Assert.True(wrote);
        Assert.Equal(ProtocolConstants.PacketHeaderSize, bytesWritten);
        Assert.Equal(ProtocolFrameError.None, writeError);
        Assert.True(read);
        Assert.Equal(ProtocolFrameError.None, readError);
        Assert.Equal(header, readHeader);
    }

    [Fact]
    public void PacketWritesAndReadsOpaquePayload()
    {
        var header = ProtocolPacketHeader.Create(
            sessionId: 88,
            messageType: ProtocolMessageType.ServerSnapshot,
            sequence: 9,
            acknowledgedSequence: 7,
            acknowledgementMask: 0b11);
        ReadOnlySpan<byte> payload = [0xDE, 0xAD, 0xBE, 0xEF];
        Span<byte> packet = stackalloc byte[ProtocolConstants.PacketHeaderSize + payload.Length];

        bool wrote = ProtocolPacketFramer.TryWritePacket(
            header,
            payload,
            packet,
            out int bytesWritten,
            out ProtocolFrameError writeError);
        bool read = ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader readHeader,
            out ReadOnlySpan<byte> readPayload,
            out ProtocolFrameError readError);

        Assert.True(wrote);
        Assert.Equal(packet.Length, bytesWritten);
        Assert.Equal(ProtocolFrameError.None, writeError);
        Assert.True(read);
        Assert.Equal(ProtocolFrameError.None, readError);
        Assert.Equal(header, readHeader);
        Assert.True(payload.SequenceEqual(readPayload));
    }

    [Fact]
    public void HeaderUsesExactLittleEndianWireLayout()
    {
        var header = new ProtocolPacketHeader(
            MajorVersion: 1,
            MinorVersion: 0,
            SessionId: 0x0102030405060708,
            MessageType: ProtocolMessageType.ServerSnapshot,
            Sequence: 0x11223344,
            AcknowledgedSequence: 0x55667788,
            AcknowledgementMask: 0x99AABBCC);
        Span<byte> buffer = stackalloc byte[ProtocolConstants.PacketHeaderSize];

        bool wrote = ProtocolPacketFramer.TryWriteHeader(
            header,
            buffer,
            out _,
            out ProtocolFrameError error);

        Assert.True(wrote);
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(
            [
                0x52, 0x4F, 0x59, 0x4C,
                0x01, 0x00,
                0x00, 0x00,
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
                0x05,
                0x44, 0x33, 0x22, 0x11,
                0x88, 0x77, 0x66, 0x55,
                0xCC, 0xBB, 0xAA, 0x99,
            ],
            buffer.ToArray());
    }

    [Fact]
    public void ReadHeaderRejectsTooShortPacket()
    {
        Span<byte> packet = stackalloc byte[ProtocolConstants.PacketHeaderSize - 1];

        bool read = ProtocolPacketFramer.TryReadHeader(
            packet,
            out ProtocolPacketHeader header,
            out ProtocolFrameError error);

        Assert.False(read);
        Assert.Equal(default, header);
        Assert.Equal(ProtocolFrameError.PacketTooShort, error);
    }

    [Fact]
    public void ReadHeaderRejectsInvalidMagic()
    {
        byte[] packet = ValidHeaderBytes();
        packet[0] = 0x00;

        bool read = ProtocolPacketFramer.TryReadHeader(
            packet,
            out _,
            out ProtocolFrameError error);

        Assert.False(read);
        Assert.Equal(ProtocolFrameError.InvalidMagic, error);
    }

    [Fact]
    public void ReadHeaderRejectsUnsupportedMajorVersion()
    {
        byte[] packet = ValidHeaderBytes();
        packet[4] = 0x02;

        bool read = ProtocolPacketFramer.TryReadHeader(
            packet,
            out _,
            out ProtocolFrameError error);

        Assert.False(read);
        Assert.Equal(ProtocolFrameError.UnsupportedMajorVersion, error);
    }

    [Fact]
    public void ReadHeaderRejectsUnknownMessageType()
    {
        byte[] packet = ValidHeaderBytes();
        packet[16] = 0x00;

        bool read = ProtocolPacketFramer.TryReadHeader(
            packet,
            out _,
            out ProtocolFrameError error);

        Assert.False(read);
        Assert.Equal(ProtocolFrameError.InvalidMessageType, error);
    }

    [Fact]
    public void WriteHeaderRejectsTooSmallDestination()
    {
        Span<byte> buffer = stackalloc byte[ProtocolConstants.PacketHeaderSize - 1];

        bool wrote = ProtocolPacketFramer.TryWriteHeader(
            ValidHeader(),
            buffer,
            out int bytesWritten,
            out ProtocolFrameError error);

        Assert.False(wrote);
        Assert.Equal(0, bytesWritten);
        Assert.Equal(ProtocolFrameError.DestinationTooSmall, error);
    }

    [Fact]
    public void WritePacketRejectsTooSmallDestination()
    {
        Span<byte> buffer = stackalloc byte[ProtocolConstants.PacketHeaderSize];
        ReadOnlySpan<byte> payload = [1];

        bool wrote = ProtocolPacketFramer.TryWritePacket(
            ValidHeader(),
            payload,
            buffer,
            out int bytesWritten,
            out ProtocolFrameError error);

        Assert.False(wrote);
        Assert.Equal(0, bytesWritten);
        Assert.Equal(ProtocolFrameError.DestinationTooSmall, error);
    }

    [Fact]
    public void AcknowledgementMaskCoversLatestAndPreviousThirtyTwoSequences()
    {
        uint acknowledgedSequence = 100;
        uint mask = 1u << 31;
        mask |= 1u << 0;

        Assert.True(ProtocolPacketFramer.IsSequenceAcknowledged(100, acknowledgedSequence, mask));
        Assert.True(ProtocolPacketFramer.IsSequenceAcknowledged(99, acknowledgedSequence, mask));
        Assert.True(ProtocolPacketFramer.IsSequenceAcknowledged(68, acknowledgedSequence, mask));
        Assert.False(ProtocolPacketFramer.IsSequenceAcknowledged(98, acknowledgedSequence, mask));
        Assert.False(ProtocolPacketFramer.IsSequenceAcknowledged(67, acknowledgedSequence, mask));
        Assert.False(ProtocolPacketFramer.IsSequenceAcknowledged(101, acknowledgedSequence, mask));
    }

    [Fact]
    public void AcknowledgementMaskHandlesSequenceWraparound()
    {
        uint acknowledgedSequence = 1;
        uint mask = 0b11;

        Assert.True(ProtocolPacketFramer.IsSequenceAcknowledged(1, acknowledgedSequence, mask));
        Assert.True(ProtocolPacketFramer.IsSequenceAcknowledged(0, acknowledgedSequence, mask));
        Assert.True(ProtocolPacketFramer.IsSequenceAcknowledged(uint.MaxValue, acknowledgedSequence, mask));
        Assert.False(ProtocolPacketFramer.IsSequenceAcknowledged(uint.MaxValue - 1, acknowledgedSequence, mask));
    }

    private static ProtocolPacketHeader ValidHeader() => ProtocolPacketHeader.Create(
        sessionId: 1,
        messageType: ProtocolMessageType.ClientHello,
        sequence: 2,
        acknowledgedSequence: 3,
        acknowledgementMask: 4);

    private static byte[] ValidHeaderBytes()
    {
        byte[] packet = new byte[ProtocolConstants.PacketHeaderSize];
        bool wrote = ProtocolPacketFramer.TryWriteHeader(
            ValidHeader(),
            packet,
            out _,
            out ProtocolFrameError error);

        Assert.True(wrote);
        Assert.Equal(ProtocolFrameError.None, error);
        return packet;
    }
}
