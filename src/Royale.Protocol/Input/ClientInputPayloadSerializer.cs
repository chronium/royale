using System.Buffers.Binary;
using System.Numerics;
using Royale.Protocol.Framing;

namespace Royale.Protocol.Input;

public static class ClientInputPayloadSerializer
{
    public const int PlayerInputCommandPayloadSize =
        sizeof(uint) +
        sizeof(uint) +
        sizeof(float) +
        sizeof(float) +
        sizeof(float) +
        sizeof(float) +
        sizeof(ushort);

    public const int MaxClientInputPayloadSize =
        sizeof(byte) + (ProtocolConstants.MaxClientInputCommandsPerPacket * PlayerInputCommandPayloadSize);

    public static bool TryWriteCommands(
        ReadOnlySpan<PlayerInputCommand> commands,
        Span<byte> destination,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if (commands.Length is 0 or > ProtocolConstants.MaxClientInputCommandsPerPacket)
            return false;

        int requiredBytes = sizeof(byte) + (commands.Length * PlayerInputCommandPayloadSize);
        if (destination.Length < requiredBytes)
            return false;

        int offset = 0;
        destination[offset++] = (byte)commands.Length;

        foreach (PlayerInputCommand command in commands)
        {
            if (!PlayerInputCommandValidation.IsValid(command) ||
                !TryWriteCommand(command, destination, ref offset))
            {
                return false;
            }
        }

        bytesWritten = offset;
        return true;
    }

    public static bool TryReadCommands(
        ReadOnlySpan<byte> source,
        out PlayerInputCommand[] commands)
    {
        commands = [];

        if (source.Length < sizeof(byte))
            return false;

        int offset = 0;
        byte commandCount = source[offset++];
        if (commandCount is 0 or > ProtocolConstants.MaxClientInputCommandsPerPacket)
            return false;

        int expectedBytes = sizeof(byte) + (commandCount * PlayerInputCommandPayloadSize);
        if (source.Length != expectedBytes)
            return false;

        commands = new PlayerInputCommand[commandCount];
        for (int i = 0; i < commands.Length; i++)
        {
            if (!TryReadCommand(source, ref offset, out PlayerInputCommand command) ||
                !PlayerInputCommandValidation.IsValid(command))
            {
                commands = [];
                return false;
            }

            commands[i] = command;
        }

        return offset == source.Length;
    }

    private static bool TryWriteCommand(PlayerInputCommand command, Span<byte> destination, ref int offset) =>
        TryWriteUInt32(command.Sequence, destination, ref offset) &&
        TryWriteUInt32(command.ClientTick, destination, ref offset) &&
        TryWriteVector2(command.Move, destination, ref offset) &&
        TryWriteSingle(command.YawRadians, destination, ref offset) &&
        TryWriteSingle(command.PitchRadians, destination, ref offset) &&
        TryWriteUInt16((ushort)command.Buttons, destination, ref offset);

    private static bool TryReadCommand(
        ReadOnlySpan<byte> source,
        ref int offset,
        out PlayerInputCommand command)
    {
        command = default;

        if (!TryReadUInt32(source, ref offset, out uint sequence) ||
            !TryReadUInt32(source, ref offset, out uint clientTick) ||
            !TryReadVector2(source, ref offset, out Vector2 move) ||
            !TryReadSingle(source, ref offset, out float yawRadians) ||
            !TryReadSingle(source, ref offset, out float pitchRadians) ||
            !TryReadUInt16(source, ref offset, out ushort buttons))
        {
            return false;
        }

        command = new PlayerInputCommand(
            sequence,
            clientTick,
            move,
            yawRadians,
            pitchRadians,
            (InputButtons)buttons);
        return true;
    }

    private static bool TryWriteVector2(Vector2 value, Span<byte> destination, ref int offset) =>
        TryWriteSingle(value.X, destination, ref offset) &&
        TryWriteSingle(value.Y, destination, ref offset);

    private static bool TryReadVector2(ReadOnlySpan<byte> source, ref int offset, out Vector2 value)
    {
        value = default;

        if (!TryReadSingle(source, ref offset, out float x) ||
            !TryReadSingle(source, ref offset, out float y))
        {
            return false;
        }

        value = new Vector2(x, y);
        return true;
    }

    private static bool TryWriteUInt16(ushort value, Span<byte> destination, ref int offset)
    {
        if (destination.Length - offset < sizeof(ushort))
            return false;

        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
        return true;
    }

    private static bool TryReadUInt16(ReadOnlySpan<byte> source, ref int offset, out ushort value)
    {
        value = 0;

        if (source.Length - offset < sizeof(ushort))
            return false;

        value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));
        offset += sizeof(ushort);
        return true;
    }

    private static bool TryWriteUInt32(uint value, Span<byte> destination, ref int offset)
    {
        if (destination.Length - offset < sizeof(uint))
            return false;

        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
        offset += sizeof(uint);
        return true;
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> source, ref int offset, out uint value)
    {
        value = 0;

        if (source.Length - offset < sizeof(uint))
            return false;

        value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        return true;
    }

    private static bool TryWriteSingle(float value, Span<byte> destination, ref int offset) =>
        TryWriteUInt32(BitConverter.SingleToUInt32Bits(value), destination, ref offset);

    private static bool TryReadSingle(ReadOnlySpan<byte> source, ref int offset, out float value)
    {
        value = 0.0f;

        if (!TryReadUInt32(source, ref offset, out uint bits))
            return false;

        value = BitConverter.UInt32BitsToSingle(bits);
        return true;
    }
}
