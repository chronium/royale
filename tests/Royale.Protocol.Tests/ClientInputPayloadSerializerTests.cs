using System.Numerics;
using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ClientInputPayloadSerializerTests
{
    [Fact]
    public void InputPayloadRoundTripsOneCommand()
    {
        PlayerInputCommand command = ValidCommand(sequence: 7) with
        {
            Move = new Vector2(0.6f, 0.8f),
            YawRadians = 1.25f,
            PitchRadians = -0.25f,
            Buttons = InputButtons.Fire | InputButtons.Crouch,
        };

        PlayerInputCommand[] decoded = RoundTrip([command]);

        Assert.Equal(command, Assert.Single(decoded));
    }

    [Fact]
    public void InputPayloadRoundTripsFourCommands()
    {
        PlayerInputCommand[] commands =
        [
            ValidCommand(sequence: 4),
            ValidCommand(sequence: 3),
            ValidCommand(sequence: 2),
            ValidCommand(sequence: 1),
        ];

        PlayerInputCommand[] decoded = RoundTrip(commands);

        Assert.Equal(commands, decoded);
    }

    [Fact]
    public void InputPayloadUsesStableLittleEndianLayoutForRepresentativeValues()
    {
        PlayerInputCommand command = new(
            Sequence: 0x01020304,
            ClientTick: 0x0A0B0C0D,
            Move: new Vector2(0.5f, -0.25f),
            YawRadians: 3.25f,
            PitchRadians: -0.5f,
            Buttons: InputButtons.Fire | InputButtons.Crouch);

        byte[] payload = WriteCommands([command]);

        Assert.Equal(
            [
                0x01,
                0x04, 0x03, 0x02, 0x01,
                0x0D, 0x0C, 0x0B, 0x0A,
                0x00, 0x00, 0x00, 0x3F,
                0x00, 0x00, 0x80, 0xBE,
                0x00, 0x00, 0x50, 0x40,
                0x00, 0x00, 0x00, 0xBF,
                0x12, 0x00,
            ],
            payload);
    }

    [Fact]
    public void InputPayloadRejectsEmptyCommandBatch()
    {
        Span<byte> payload = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];

        Assert.False(ClientInputPayloadSerializer.TryWriteCommands([], payload, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
        Assert.False(ClientInputPayloadSerializer.TryReadCommands([0], out _));
    }

    [Fact]
    public void InputPayloadRejectsFiveCommandBatch()
    {
        PlayerInputCommand[] commands =
        [
            ValidCommand(sequence: 1),
            ValidCommand(sequence: 2),
            ValidCommand(sequence: 3),
            ValidCommand(sequence: 4),
            ValidCommand(sequence: 5),
        ];
        Span<byte> payload = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize + 1];

        Assert.False(ClientInputPayloadSerializer.TryWriteCommands(commands, payload, out _));
        Assert.False(ClientInputPayloadSerializer.TryReadCommands([5], out _));
    }

    [Fact]
    public void InputPayloadRejectsInvalidMovement()
    {
        AssertInvalidCommand(ValidCommand(sequence: 1) with
        {
            Move = new Vector2(2.0f, 0.0f),
        });
    }

    [Fact]
    public void InputPayloadRejectsInvalidPitch()
    {
        AssertInvalidCommand(ValidCommand(sequence: 1) with
        {
            PitchRadians = PlayerInputCommandValidation.MaxPitchRadians + 0.001f,
        });
    }

    [Fact]
    public void InputPayloadRejectsNonFiniteFloats()
    {
        AssertInvalidCommand(ValidCommand(sequence: 1) with
        {
            YawRadians = float.NaN,
        });
    }

    [Fact]
    public void InputPayloadRejectsUndefinedButtons()
    {
        AssertInvalidCommand(ValidCommand(sequence: 1) with
        {
            Buttons = (InputButtons)(1 << 15),
        });
    }

    [Fact]
    public void InputPayloadRejectsTruncatedPayload()
    {
        byte[] payload = WriteCommands([ValidCommand(sequence: 1)]);

        Assert.False(ClientInputPayloadSerializer.TryReadCommands(payload.AsSpan(0, payload.Length - 1), out _));
    }

    [Fact]
    public void InputPayloadRejectsTrailingBytes()
    {
        byte[] payload = WriteCommands([ValidCommand(sequence: 1)]);
        byte[] withTrailingByte = [.. payload, 0xFF];

        Assert.False(ClientInputPayloadSerializer.TryReadCommands(withTrailingByte, out _));
    }

    [Fact]
    public void InputPayloadRejectsDestinationTooSmall()
    {
        PlayerInputCommand command = ValidCommand(sequence: 1);
        Span<byte> destination = stackalloc byte[sizeof(byte) + ClientInputPayloadSerializer.PlayerInputCommandPayloadSize - 1];

        Assert.False(ClientInputPayloadSerializer.TryWriteCommands([command], destination, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    private static void AssertInvalidCommand(PlayerInputCommand command)
    {
        Span<byte> destination = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];

        Assert.False(ClientInputPayloadSerializer.TryWriteCommands([command], destination, out _));

        byte[] payload = WriteCommands([ValidCommand(sequence: command.Sequence)]);
        MutatePayloadCommand(payload, command);
        Assert.False(ClientInputPayloadSerializer.TryReadCommands(payload, out _));
    }

    private static void MutatePayloadCommand(byte[] payload, PlayerInputCommand command)
    {
        Span<byte> destination = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];
        PlayerInputCommand valid = ValidCommand(sequence: command.Sequence);
        Assert.True(ClientInputPayloadSerializer.TryWriteCommands([command with
        {
            Move = PlayerInputCommandValidation.IsValid(command) ? command.Move : valid.Move,
            YawRadians = float.IsFinite(command.YawRadians) ? command.YawRadians : valid.YawRadians,
            PitchRadians = command.PitchRadians >= PlayerInputCommandValidation.MinPitchRadians &&
                command.PitchRadians <= PlayerInputCommandValidation.MaxPitchRadians
                    ? command.PitchRadians
                    : valid.PitchRadians,
            Buttons = PlayerInputCommandValidation.AreButtonsDefined(command.Buttons)
                ? command.Buttons
                : valid.Buttons,
        }], destination, out _));

        if (command.Move.LengthSquared() > 1.1f)
        {
            WriteSingle(payload, offset: 9, command.Move.X);
            WriteSingle(payload, offset: 13, command.Move.Y);
        }
        else if (!float.IsFinite(command.YawRadians))
        {
            WriteSingle(payload, offset: 17, command.YawRadians);
        }
        else if (command.PitchRadians < PlayerInputCommandValidation.MinPitchRadians ||
            command.PitchRadians > PlayerInputCommandValidation.MaxPitchRadians)
        {
            WriteSingle(payload, offset: 21, command.PitchRadians);
        }
        else
        {
            payload[25] = (byte)((ushort)command.Buttons & 0xFF);
            payload[26] = (byte)(((ushort)command.Buttons >> 8) & 0xFF);
        }
    }

    private static void WriteSingle(byte[] payload, int offset, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        payload[offset] = (byte)(bits & 0xFF);
        payload[offset + 1] = (byte)((bits >> 8) & 0xFF);
        payload[offset + 2] = (byte)((bits >> 16) & 0xFF);
        payload[offset + 3] = (byte)((bits >> 24) & 0xFF);
    }

    private static PlayerInputCommand[] RoundTrip(PlayerInputCommand[] commands)
    {
        byte[] payload = WriteCommands(commands);

        Assert.True(ClientInputPayloadSerializer.TryReadCommands(payload, out PlayerInputCommand[] decoded));
        return decoded;
    }

    private static byte[] WriteCommands(PlayerInputCommand[] commands)
    {
        byte[] payload = new byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];

        Assert.True(ClientInputPayloadSerializer.TryWriteCommands(commands, payload, out int bytesWritten));
        return payload[..bytesWritten];
    }

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);
}
