using System.Buffers.Binary;
using System.Text;

namespace Royale.Protocol;

public static class HandshakePayloadSerializer
{
    public const int MaxClientHelloPayloadSize =
        sizeof(byte) + ProtocolConstants.MaxBuildIdLength +
        sizeof(byte) + ProtocolConstants.MaxContentVersionLength;

    public const int MaxServerAcceptPayloadSize =
        sizeof(ulong) +
        sizeof(uint) +
        sizeof(uint) +
        sizeof(ulong) +
        sizeof(byte) + ProtocolConstants.MaxMapIdLength;

    public const int MaxServerRejectPayloadSize =
        sizeof(byte) +
        sizeof(byte) + ProtocolConstants.MaxRejectDetailLength;

    public static bool TryWriteClientHello(
        ClientHello hello,
        Span<byte> destination,
        out int bytesWritten)
    {
        bytesWritten = 0;
        ArgumentNullException.ThrowIfNull(hello);

        if (!TryWriteString(hello.BuildId, ProtocolConstants.MaxBuildIdLength, destination, ref bytesWritten))
            return false;

        return TryWriteString(
            hello.ContentVersion,
            ProtocolConstants.MaxContentVersionLength,
            destination,
            ref bytesWritten);
    }

    public static bool TryReadClientHello(
        ReadOnlySpan<byte> source,
        out ClientHello? hello)
    {
        hello = null;
        int offset = 0;

        if (!TryReadString(source, ProtocolConstants.MaxBuildIdLength, ref offset, out string? buildId) ||
            !TryReadString(source, ProtocolConstants.MaxContentVersionLength, ref offset, out string? contentVersion) ||
            offset != source.Length)
        {
            return false;
        }

        hello = new ClientHello(buildId, contentVersion);
        return true;
    }

    public static bool TryWriteServerAccept(
        ServerAccept accept,
        Span<byte> destination,
        out int bytesWritten)
    {
        bytesWritten = 0;
        ArgumentNullException.ThrowIfNull(accept);

        const int fixedSize = sizeof(ulong) + sizeof(uint) + sizeof(uint) + sizeof(ulong);
        if (destination.Length < fixedSize)
            return false;

        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(bytesWritten, sizeof(ulong)), accept.SessionId);
        bytesWritten += sizeof(ulong);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(bytesWritten, sizeof(uint)), accept.ConnectionId);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(bytesWritten, sizeof(uint)), accept.PlayerId);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(bytesWritten, sizeof(ulong)), accept.ServerTick);
        bytesWritten += sizeof(ulong);

        return TryWriteString(accept.MapId, ProtocolConstants.MaxMapIdLength, destination, ref bytesWritten);
    }

    public static bool TryReadServerAccept(
        ReadOnlySpan<byte> source,
        out ServerAccept? accept)
    {
        accept = null;
        int offset = 0;
        const int fixedSize = sizeof(ulong) + sizeof(uint) + sizeof(uint) + sizeof(ulong);

        if (source.Length < fixedSize)
            return false;

        ulong sessionId = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, sizeof(ulong)));
        offset += sizeof(ulong);
        uint connectionId = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        uint playerId = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        ulong serverTick = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, sizeof(ulong)));
        offset += sizeof(ulong);

        if (!TryReadString(source, ProtocolConstants.MaxMapIdLength, ref offset, out string? mapId) ||
            offset != source.Length)
        {
            return false;
        }

        accept = new ServerAccept(sessionId, connectionId, playerId, serverTick, mapId);
        return true;
    }

    public static bool TryWriteServerReject(
        ServerReject reject,
        Span<byte> destination,
        out int bytesWritten)
    {
        bytesWritten = 0;
        ArgumentNullException.ThrowIfNull(reject);

        if (!Enum.IsDefined(reject.Reason) || destination.Length < sizeof(byte))
            return false;

        destination[bytesWritten++] = (byte)reject.Reason;
        return TryWriteString(reject.Detail, ProtocolConstants.MaxRejectDetailLength, destination, ref bytesWritten);
    }

    public static bool TryReadServerReject(
        ReadOnlySpan<byte> source,
        out ServerReject? reject)
    {
        reject = null;
        int offset = 0;

        if (source.Length < sizeof(byte) || !Enum.IsDefined(typeof(ServerRejectReason), source[offset]))
            return false;

        ServerRejectReason reason = (ServerRejectReason)source[offset++];

        if (!TryReadString(source, ProtocolConstants.MaxRejectDetailLength, ref offset, out string? detail) ||
            offset != source.Length)
        {
            return false;
        }

        reject = new ServerReject(reason, detail);
        return true;
    }

    private static bool TryWriteString(
        string value,
        int maxByteLength,
        Span<byte> destination,
        ref int offset)
    {
        ArgumentNullException.ThrowIfNull(value);

        int byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount > maxByteLength ||
            byteCount > byte.MaxValue ||
            destination.Length - offset < sizeof(byte) + byteCount)
        {
            return false;
        }

        destination[offset++] = (byte)byteCount;
        int written = Encoding.UTF8.GetBytes(value, destination[offset..]);
        offset += written;
        return true;
    }

    private static bool TryReadString(
        ReadOnlySpan<byte> source,
        int maxByteLength,
        ref int offset,
        out string value)
    {
        value = string.Empty;

        if (offset >= source.Length)
            return false;

        int byteCount = source[offset++];
        if (byteCount > maxByteLength || source.Length - offset < byteCount)
            return false;

        value = Encoding.UTF8.GetString(source.Slice(offset, byteCount));
        offset += byteCount;
        return true;
    }
}
