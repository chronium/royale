using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Royale.Protocol;

public static class ServerSnapshotPayloadSerializer
{
    private const byte NullableNone = 0;
    private const byte NullableSome = 1;

    private const int Vector3WireSize = sizeof(float) * 3;
    private const int NullableUInt32WireSize = sizeof(byte) + sizeof(uint);
    private const int NullableUInt64WireSize = sizeof(byte) + sizeof(ulong);

    public const int MaxPlayerSnapshotStatePayloadSize =
        sizeof(uint) +
        Vector3WireSize +
        Vector3WireSize +
        sizeof(float) +
        sizeof(float) +
        sizeof(int) +
        sizeof(int) +
        sizeof(byte) +
        MaxWeaponSnapshotStatePayloadSize;

    public const int MaxWeaponSnapshotStatePayloadSize =
        sizeof(byte) + ProtocolConstants.MaxSnapshotWeaponIdLength +
        sizeof(int) +
        sizeof(int) +
        sizeof(ulong) +
        NullableUInt64WireSize +
        sizeof(byte) +
        NullableUInt64WireSize;

    public const int MatchSnapshotStatePayloadSize =
        sizeof(byte) +
        sizeof(ulong) +
        sizeof(int) +
        NullableUInt32WireSize;

    public const int SafeZoneSnapshotStatePayloadSize =
        Vector3WireSize +
        sizeof(float) +
        sizeof(float) +
        sizeof(ulong);

    public const int MaxServerSnapshotPayloadSize =
        sizeof(ulong) +
        NullableUInt32WireSize +
        NullableUInt32WireSize +
        sizeof(ushort) +
        (ProtocolConstants.MaxSnapshotPlayers * MaxPlayerSnapshotStatePayloadSize) +
        MatchSnapshotStatePayloadSize +
        SafeZoneSnapshotStatePayloadSize;

    public static bool TryWriteSnapshot(
        ServerSnapshot snapshot,
        Span<byte> destination,
        out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        bytesWritten = 0;
        int offset = 0;

        if (snapshot.Players is null ||
            snapshot.Players.Count > ProtocolConstants.MaxSnapshotPlayers ||
            !Enum.IsDefined(snapshot.Match.Phase))
        {
            return false;
        }

        if (!TryWriteUInt64(snapshot.ServerTick, destination, ref offset) ||
            !TryWriteNullableUInt32(snapshot.LocalPlayerId, destination, ref offset) ||
            !TryWriteNullableUInt32(snapshot.AcknowledgedInputSequence, destination, ref offset) ||
            !TryWriteUInt16((ushort)snapshot.Players.Count, destination, ref offset))
        {
            return false;
        }

        foreach (PlayerSnapshotState player in snapshot.Players)
        {
            if (!TryWritePlayer(player, destination, ref offset))
                return false;
        }

        if (!TryWriteMatch(snapshot.Match, destination, ref offset) ||
            !TryWriteSafeZone(snapshot.SafeZone, destination, ref offset))
        {
            return false;
        }

        bytesWritten = offset;
        return true;
    }

    public static bool TryReadSnapshot(
        ReadOnlySpan<byte> source,
        out ServerSnapshot? snapshot)
    {
        snapshot = null;
        int offset = 0;

        if (!TryReadUInt64(source, ref offset, out ulong serverTick) ||
            !TryReadNullableUInt32(source, ref offset, out uint? localPlayerId) ||
            !TryReadNullableUInt32(source, ref offset, out uint? acknowledgedInputSequence) ||
            !TryReadUInt16(source, ref offset, out ushort playerCount) ||
            playerCount > ProtocolConstants.MaxSnapshotPlayers)
        {
            return false;
        }

        var players = new PlayerSnapshotState[playerCount];
        for (int i = 0; i < players.Length; i++)
        {
            if (!TryReadPlayer(source, ref offset, out players[i]))
                return false;
        }

        if (!TryReadMatch(source, ref offset, out MatchSnapshotState match) ||
            !TryReadSafeZone(source, ref offset, out SafeZoneSnapshotState safeZone) ||
            offset != source.Length)
        {
            return false;
        }

        snapshot = new ServerSnapshot(
            serverTick,
            localPlayerId,
            acknowledgedInputSequence,
            players,
            match,
            safeZone);
        return true;
    }

    private static bool TryWritePlayer(
        PlayerSnapshotState player,
        Span<byte> destination,
        ref int offset) =>
        TryWriteUInt32(player.PlayerId, destination, ref offset) &&
        TryWriteVector3(player.Position, destination, ref offset) &&
        TryWriteVector3(player.Velocity, destination, ref offset) &&
        TryWriteSingle(player.YawRadians, destination, ref offset) &&
        TryWriteSingle(player.PitchRadians, destination, ref offset) &&
        TryWriteInt32(player.CurrentHealth, destination, ref offset) &&
        TryWriteInt32(player.MaxHealth, destination, ref offset) &&
        TryWriteBoolean(player.Alive, destination, ref offset) &&
        TryWriteWeapon(player.Weapon, destination, ref offset);

    private static bool TryReadPlayer(
        ReadOnlySpan<byte> source,
        ref int offset,
        out PlayerSnapshotState player)
    {
        player = default;

        if (!TryReadUInt32(source, ref offset, out uint playerId) ||
            !TryReadVector3(source, ref offset, out Vector3 position) ||
            !TryReadVector3(source, ref offset, out Vector3 velocity) ||
            !TryReadSingle(source, ref offset, out float yawRadians) ||
            !TryReadSingle(source, ref offset, out float pitchRadians) ||
            !TryReadInt32(source, ref offset, out int currentHealth) ||
            !TryReadInt32(source, ref offset, out int maxHealth) ||
            !TryReadBoolean(source, ref offset, out bool alive) ||
            !TryReadWeapon(source, ref offset, out WeaponSnapshotState weapon))
        {
            return false;
        }

        player = new PlayerSnapshotState(
            playerId,
            position,
            velocity,
            yawRadians,
            pitchRadians,
            currentHealth,
            maxHealth,
            alive,
            weapon);
        return true;
    }

    private static bool TryWriteWeapon(
        WeaponSnapshotState weapon,
        Span<byte> destination,
        ref int offset) =>
        TryWriteString(weapon.WeaponId, ProtocolConstants.MaxSnapshotWeaponIdLength, destination, ref offset) &&
        TryWriteInt32(weapon.AmmoInMagazine, destination, ref offset) &&
        TryWriteInt32(weapon.ReserveAmmo, destination, ref offset) &&
        TryWriteUInt64(weapon.NextAllowedFireTick, destination, ref offset) &&
        TryWriteNullableUInt64(weapon.LastFiredTick, destination, ref offset) &&
        TryWriteBoolean(weapon.IsReloading, destination, ref offset) &&
        TryWriteNullableUInt64(weapon.ReloadCompleteTick, destination, ref offset);

    private static bool TryReadWeapon(
        ReadOnlySpan<byte> source,
        ref int offset,
        out WeaponSnapshotState weapon)
    {
        weapon = default;

        if (!TryReadString(source, ProtocolConstants.MaxSnapshotWeaponIdLength, ref offset, out string weaponId) ||
            !TryReadInt32(source, ref offset, out int ammoInMagazine) ||
            !TryReadInt32(source, ref offset, out int reserveAmmo) ||
            !TryReadUInt64(source, ref offset, out ulong nextAllowedFireTick) ||
            !TryReadNullableUInt64(source, ref offset, out ulong? lastFiredTick) ||
            !TryReadBoolean(source, ref offset, out bool isReloading) ||
            !TryReadNullableUInt64(source, ref offset, out ulong? reloadCompleteTick))
        {
            return false;
        }

        weapon = new WeaponSnapshotState(
            weaponId,
            ammoInMagazine,
            reserveAmmo,
            nextAllowedFireTick,
            lastFiredTick,
            isReloading,
            reloadCompleteTick);
        return true;
    }

    private static bool TryWriteMatch(
        MatchSnapshotState match,
        Span<byte> destination,
        ref int offset)
    {
        if (!Enum.IsDefined(match.Phase) || !TryWriteByte((byte)match.Phase, destination, ref offset))
            return false;

        return TryWriteUInt64(match.PhaseStartedTick, destination, ref offset) &&
            TryWriteInt32(match.LivingPlayerCount, destination, ref offset) &&
            TryWriteNullableUInt32(match.WinnerPlayerId, destination, ref offset);
    }

    private static bool TryReadMatch(
        ReadOnlySpan<byte> source,
        ref int offset,
        out MatchSnapshotState match)
    {
        match = default;

        if (!TryReadByte(source, ref offset, out byte phaseValue) ||
            !Enum.IsDefined((ServerSnapshotMatchPhase)phaseValue) ||
            !TryReadUInt64(source, ref offset, out ulong phaseStartedTick) ||
            !TryReadInt32(source, ref offset, out int livingPlayerCount) ||
            !TryReadNullableUInt32(source, ref offset, out uint? winnerPlayerId))
        {
            return false;
        }

        match = new MatchSnapshotState(
            (ServerSnapshotMatchPhase)phaseValue,
            phaseStartedTick,
            livingPlayerCount,
            winnerPlayerId);
        return true;
    }

    private static bool TryWriteSafeZone(
        SafeZoneSnapshotState safeZone,
        Span<byte> destination,
        ref int offset) =>
        TryWriteVector3(safeZone.Center, destination, ref offset) &&
        TryWriteSingle(safeZone.CurrentRadius, destination, ref offset) &&
        TryWriteSingle(safeZone.TargetRadius, destination, ref offset) &&
        TryWriteUInt64(safeZone.LastUpdatedTick, destination, ref offset);

    private static bool TryReadSafeZone(
        ReadOnlySpan<byte> source,
        ref int offset,
        out SafeZoneSnapshotState safeZone)
    {
        safeZone = default;

        if (!TryReadVector3(source, ref offset, out Vector3 center) ||
            !TryReadSingle(source, ref offset, out float currentRadius) ||
            !TryReadSingle(source, ref offset, out float targetRadius) ||
            !TryReadUInt64(source, ref offset, out ulong lastUpdatedTick))
        {
            return false;
        }

        safeZone = new SafeZoneSnapshotState(center, currentRadius, targetRadius, lastUpdatedTick);
        return true;
    }

    private static bool TryWriteVector3(Vector3 value, Span<byte> destination, ref int offset) =>
        TryWriteSingle(value.X, destination, ref offset) &&
        TryWriteSingle(value.Y, destination, ref offset) &&
        TryWriteSingle(value.Z, destination, ref offset);

    private static bool TryReadVector3(ReadOnlySpan<byte> source, ref int offset, out Vector3 value)
    {
        value = default;

        if (!TryReadSingle(source, ref offset, out float x) ||
            !TryReadSingle(source, ref offset, out float y) ||
            !TryReadSingle(source, ref offset, out float z))
        {
            return false;
        }

        value = new Vector3(x, y, z);
        return true;
    }

    private static bool TryWriteNullableUInt32(uint? value, Span<byte> destination, ref int offset)
    {
        if (!value.HasValue)
            return TryWriteByte(NullableNone, destination, ref offset);

        return TryWriteByte(NullableSome, destination, ref offset) &&
            TryWriteUInt32(value.Value, destination, ref offset);
    }

    private static bool TryReadNullableUInt32(ReadOnlySpan<byte> source, ref int offset, out uint? value)
    {
        value = null;

        if (!TryReadByte(source, ref offset, out byte presence))
            return false;

        if (presence == NullableNone)
            return true;

        if (presence != NullableSome || !TryReadUInt32(source, ref offset, out uint concreteValue))
            return false;

        value = concreteValue;
        return true;
    }

    private static bool TryWriteNullableUInt64(ulong? value, Span<byte> destination, ref int offset)
    {
        if (!value.HasValue)
            return TryWriteByte(NullableNone, destination, ref offset);

        return TryWriteByte(NullableSome, destination, ref offset) &&
            TryWriteUInt64(value.Value, destination, ref offset);
    }

    private static bool TryReadNullableUInt64(ReadOnlySpan<byte> source, ref int offset, out ulong? value)
    {
        value = null;

        if (!TryReadByte(source, ref offset, out byte presence))
            return false;

        if (presence == NullableNone)
            return true;

        if (presence != NullableSome || !TryReadUInt64(source, ref offset, out ulong concreteValue))
            return false;

        value = concreteValue;
        return true;
    }

    private static bool TryWriteString(string value, int maxByteLength, Span<byte> destination, ref int offset)
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

        if (!TryReadByte(source, ref offset, out byte byteCount) ||
            byteCount > maxByteLength ||
            source.Length - offset < byteCount)
        {
            return false;
        }

        value = Encoding.UTF8.GetString(source.Slice(offset, byteCount));
        offset += byteCount;
        return true;
    }

    private static bool TryWriteBoolean(bool value, Span<byte> destination, ref int offset) =>
        TryWriteByte(value ? (byte)1 : (byte)0, destination, ref offset);

    private static bool TryReadBoolean(ReadOnlySpan<byte> source, ref int offset, out bool value)
    {
        value = false;

        if (!TryReadByte(source, ref offset, out byte wireValue) || wireValue > 1)
            return false;

        value = wireValue == 1;
        return true;
    }

    private static bool TryWriteByte(byte value, Span<byte> destination, ref int offset)
    {
        if (destination.Length - offset < sizeof(byte))
            return false;

        destination[offset++] = value;
        return true;
    }

    private static bool TryReadByte(ReadOnlySpan<byte> source, ref int offset, out byte value)
    {
        value = 0;

        if (source.Length - offset < sizeof(byte))
            return false;

        value = source[offset++];
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

    private static bool TryWriteUInt64(ulong value, Span<byte> destination, ref int offset)
    {
        if (destination.Length - offset < sizeof(ulong))
            return false;

        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, sizeof(ulong)), value);
        offset += sizeof(ulong);
        return true;
    }

    private static bool TryReadUInt64(ReadOnlySpan<byte> source, ref int offset, out ulong value)
    {
        value = 0;

        if (source.Length - offset < sizeof(ulong))
            return false;

        value = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, sizeof(ulong)));
        offset += sizeof(ulong);
        return true;
    }

    private static bool TryWriteInt32(int value, Span<byte> destination, ref int offset)
    {
        if (destination.Length - offset < sizeof(int))
            return false;

        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), value);
        offset += sizeof(int);
        return true;
    }

    private static bool TryReadInt32(ReadOnlySpan<byte> source, ref int offset, out int value)
    {
        value = 0;

        if (source.Length - offset < sizeof(int))
            return false;

        value = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));
        offset += sizeof(int);
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
