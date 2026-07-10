using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class HandshakePayloadSerializerTests
{
    [Fact]
    public void ClientHelloRoundTrips()
    {
        var hello = new ClientHello("build-a", "content-b");
        Span<byte> buffer = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize];

        bool wrote = HandshakePayloadSerializer.TryWriteClientHello(hello, buffer, out int bytesWritten);
        bool read = HandshakePayloadSerializer.TryReadClientHello(buffer[..bytesWritten], out ClientHello? result);

        Assert.True(wrote);
        Assert.True(read);
        Assert.Equal(hello, result);
    }

    [Fact]
    public void ServerAcceptRoundTrips()
    {
        var accept = new ServerAccept(
            SessionId: 44,
            ConnectionId: 2,
            PlayerId: 9,
            ServerTick: 100,
            MapId: "graybox");
        Span<byte> buffer = stackalloc byte[HandshakePayloadSerializer.MaxServerAcceptPayloadSize];

        bool wrote = HandshakePayloadSerializer.TryWriteServerAccept(accept, buffer, out int bytesWritten);
        bool read = HandshakePayloadSerializer.TryReadServerAccept(buffer[..bytesWritten], out ServerAccept? result);

        Assert.True(wrote);
        Assert.True(read);
        Assert.Equal(accept, result);
    }

    [Fact]
    public void ServerRejectRoundTrips()
    {
        var reject = new ServerReject(ServerRejectReason.MatchUnavailable, "match roster is full");
        Span<byte> buffer = stackalloc byte[HandshakePayloadSerializer.MaxServerRejectPayloadSize];

        bool wrote = HandshakePayloadSerializer.TryWriteServerReject(reject, buffer, out int bytesWritten);
        bool read = HandshakePayloadSerializer.TryReadServerReject(buffer[..bytesWritten], out ServerReject? result);

        Assert.True(wrote);
        Assert.True(read);
        Assert.Equal(reject, result);
    }

    [Fact]
    public void ClientHelloRejectsOversizedBuildAndContentStrings()
    {
        Span<byte> buffer = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize + 1];
        string oversizedBuild = new('b', ProtocolConstants.MaxBuildIdLength + 1);
        string oversizedContent = new('c', ProtocolConstants.MaxContentVersionLength + 1);

        Assert.False(HandshakePayloadSerializer.TryWriteClientHello(
            new ClientHello(oversizedBuild, ProtocolConstants.ContentVersion),
            buffer,
            out _));
        Assert.False(HandshakePayloadSerializer.TryWriteClientHello(
            new ClientHello(ProtocolConstants.BuildId, oversizedContent),
            buffer,
            out _));
    }

    [Fact]
    public void ServerAcceptRejectsOversizedMapId()
    {
        Span<byte> buffer = stackalloc byte[HandshakePayloadSerializer.MaxServerAcceptPayloadSize + 1];
        string oversizedMapId = new('m', ProtocolConstants.MaxMapIdLength + 1);

        Assert.False(HandshakePayloadSerializer.TryWriteServerAccept(
            new ServerAccept(1, 1, 1, 0, oversizedMapId),
            buffer,
            out _));
    }

    [Fact]
    public void ServerRejectRejectsOversizedDetail()
    {
        Span<byte> buffer = stackalloc byte[HandshakePayloadSerializer.MaxServerRejectPayloadSize + 1];
        string oversizedDetail = new('d', ProtocolConstants.MaxRejectDetailLength + 1);

        Assert.False(HandshakePayloadSerializer.TryWriteServerReject(
            new ServerReject(ServerRejectReason.MalformedPacket, oversizedDetail),
            buffer,
            out _));
    }

    [Fact]
    public void ReadersRejectOversizedStringLengthPrefixes()
    {
        byte[] helloPayload =
        [
            (byte)(ProtocolConstants.MaxBuildIdLength + 1),
            .. Enumerable.Repeat((byte)'b', ProtocolConstants.MaxBuildIdLength + 1),
            1,
            (byte)'c',
        ];
        byte[] acceptPayload =
        [
            .. new byte[sizeof(ulong) + sizeof(uint) + sizeof(uint) + sizeof(ulong)],
            (byte)(ProtocolConstants.MaxMapIdLength + 1),
            .. Enumerable.Repeat((byte)'m', ProtocolConstants.MaxMapIdLength + 1),
        ];
        byte[] rejectPayload =
        [
            (byte)ServerRejectReason.MalformedPacket,
            (byte)(ProtocolConstants.MaxRejectDetailLength + 1),
            .. Enumerable.Repeat((byte)'d', ProtocolConstants.MaxRejectDetailLength + 1),
        ];

        Assert.False(HandshakePayloadSerializer.TryReadClientHello(helloPayload, out _));
        Assert.False(HandshakePayloadSerializer.TryReadServerAccept(acceptPayload, out _));
        Assert.False(HandshakePayloadSerializer.TryReadServerReject(rejectPayload, out _));
    }

    [Fact]
    public void ServerRejectReasonValuesAreStable()
    {
        Assert.Equal(1, (byte)ServerRejectReason.MalformedPacket);
        Assert.Equal(2, (byte)ServerRejectReason.UnexpectedMessageType);
        Assert.Equal(3, (byte)ServerRejectReason.UnsupportedProtocolVersion);
        Assert.Equal(4, (byte)ServerRejectReason.IncompatibleBuild);
        Assert.Equal(5, (byte)ServerRejectReason.IncompatibleContent);
        Assert.Equal(6, (byte)ServerRejectReason.AcceptFailed);
        Assert.Equal(7, (byte)ServerRejectReason.MatchUnavailable);
    }
}
