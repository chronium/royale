using System.Numerics;
using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ServerSnapshotTests
{
    [Fact]
    public void ServerSnapshotCarriesRecipientAckAndStateSections()
    {
        var weapon = new WeaponSnapshotState(
            WeaponId: "default-rifle",
            AmmoInMagazine: 27,
            ReserveAmmo: 90,
            NextAllowedFireTick: 130,
            LastFiredTick: 120,
            IsReloading: false,
            ReloadCompleteTick: null);
        var player = new PlayerSnapshotState(
            PlayerId: 7,
            Kind: ServerSnapshotPlayerKind.Human,
            Position: new Vector3(1.0f, 2.0f, 3.0f),
            Velocity: new Vector3(4.0f, 5.0f, 6.0f),
            YawRadians: 0.5f,
            PitchRadians: -0.25f,
            CurrentHealth: 80,
            MaxHealth: 100,
            Alive: true,
            Weapon: weapon,
            LastProcessedInputSequence: 19,
            LastProcessedInputClientTick: 119);
        var match = new MatchSnapshotState(
            ServerSnapshotMatchPhase.Playing,
            PhaseStartedTick: 60,
            LivingPlayerCount: 3,
            WinnerPlayerId: null);
        var safeZone = new SafeZoneSnapshotState(
            Center: new Vector3(10.0f, 0.0f, -5.0f),
            CurrentRadius: 100.0f,
            TargetRadius: 50.0f,
            LastUpdatedTick: 90);

        var snapshot = new ServerSnapshot(
            ServerTick: 123,
            LocalPlayerId: 7,
            AcknowledgedInputSequence: 42,
            Players: [player],
            Match: match,
            SafeZone: safeZone);

        Assert.Equal(123UL, snapshot.ServerTick);
        Assert.Equal(7U, snapshot.LocalPlayerId);
        Assert.Equal(42U, snapshot.AcknowledgedInputSequence);
        Assert.Equal(19U, Assert.Single(snapshot.Players).LastProcessedInputSequence);
        Assert.Equal(119U, Assert.Single(snapshot.Players).LastProcessedInputClientTick);
        Assert.Equal(player, Assert.Single(snapshot.Players));
        Assert.Equal(match, snapshot.Match);
        Assert.Equal(safeZone, snapshot.SafeZone);
    }

    [Fact]
    public void PlayerSnapshotCarriesTransformHealthAndWeaponState()
    {
        var weapon = new WeaponSnapshotState(
            WeaponId: "default-rifle",
            AmmoInMagazine: 10,
            ReserveAmmo: 20,
            NextAllowedFireTick: 30,
            LastFiredTick: null,
            IsReloading: true,
            ReloadCompleteTick: 40);

        var player = new PlayerSnapshotState(
            PlayerId: 2,
            Kind: ServerSnapshotPlayerKind.Bot,
            Position: new Vector3(1.0f, 0.0f, 2.0f),
            Velocity: new Vector3(0.1f, 0.0f, -0.2f),
            YawRadians: 1.25f,
            PitchRadians: 0.75f,
            CurrentHealth: 25,
            MaxHealth: 100,
            Alive: false,
            Weapon: weapon,
            LastProcessedInputSequence: 9,
            LastProcessedInputClientTick: 109);

        Assert.Equal(2U, player.PlayerId);
        Assert.Equal(ServerSnapshotPlayerKind.Bot, player.Kind);
        Assert.Equal(new Vector3(1.0f, 0.0f, 2.0f), player.Position);
        Assert.Equal(new Vector3(0.1f, 0.0f, -0.2f), player.Velocity);
        Assert.Equal(1.25f, player.YawRadians);
        Assert.Equal(0.75f, player.PitchRadians);
        Assert.Equal(25, player.CurrentHealth);
        Assert.Equal(100, player.MaxHealth);
        Assert.False(player.Alive);
        Assert.Equal(weapon, player.Weapon);
        Assert.Equal(9U, player.LastProcessedInputSequence);
        Assert.Equal(109U, player.LastProcessedInputClientTick);
    }

    [Fact]
    public void SnapshotPayloadRoundTripsFullRecipientState()
    {
        ServerSnapshot snapshot = CreateSnapshot(
            localPlayerId: 7,
            acknowledgedInputSequence: 42,
            players:
            [
                CreatePlayer(7, "rifle", alive: true, lastFiredTick: 120, reloadCompleteTick: null) with
                {
                    LastProcessedInputSequence = 42,
                    LastProcessedInputClientTick = 142,
                },
                CreatePlayer(8, "shotgun-\u2603", alive: false, lastFiredTick: null, reloadCompleteTick: 160),
            ],
            match: new MatchSnapshotState(
                ServerSnapshotMatchPhase.Finished,
                PhaseStartedTick: 60,
                LivingPlayerCount: 1,
                WinnerPlayerId: 7),
            safeZone: new SafeZoneSnapshotState(
                new Vector3(10.0f, 0.0f, -5.0f),
                CurrentRadius: 100.0f,
                TargetRadius: 50.0f,
                LastUpdatedTick: 90));
        byte[] payload = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];

        Assert.True(ServerSnapshotPayloadSerializer.TryWriteSnapshot(snapshot, payload, out int bytesWritten));
        Assert.True(ServerSnapshotPayloadSerializer.TryReadSnapshot(
            payload.AsSpan(0, bytesWritten),
            out ServerSnapshot? decoded));

        Assert.NotNull(decoded);
        AssertSnapshotEqual(snapshot, decoded!);
    }

    [Fact]
    public void SnapshotPayloadRoundTripsWithoutRecipientIdentity()
    {
        ServerSnapshot snapshot = CreateSnapshot(
            localPlayerId: null,
            acknowledgedInputSequence: null,
            players: [],
            match: new MatchSnapshotState(
                ServerSnapshotMatchPhase.WaitingForPlayers,
                PhaseStartedTick: 0,
                LivingPlayerCount: 0,
                WinnerPlayerId: null),
            safeZone: new SafeZoneSnapshotState(Vector3.Zero, 0.0f, 0.0f, 0));

        ServerSnapshot decoded = RoundTrip(snapshot);

        AssertSnapshotEqual(snapshot, decoded);
    }

    [Theory]
    [InlineData(ServerSnapshotMatchPhase.WaitingForPlayers)]
    [InlineData(ServerSnapshotMatchPhase.Playing)]
    [InlineData(ServerSnapshotMatchPhase.Finished)]
    [InlineData(ServerSnapshotMatchPhase.Countdown)]
    [InlineData(ServerSnapshotMatchPhase.Resetting)]
    public void SnapshotPayloadRoundTripsEveryMatchPhase(ServerSnapshotMatchPhase phase)
    {
        ServerSnapshot snapshot = CreateMinimalSnapshot() with
        {
            Match = DefaultMatch() with { Phase = phase },
        };

        ServerSnapshot decoded = RoundTrip(snapshot);

        Assert.Equal(phase, decoded.Match.Phase);
    }

    [Theory]
    [InlineData(ServerSnapshotMatchPhase.Playing, 1)]
    [InlineData(ServerSnapshotMatchPhase.Finished, 2)]
    public void PlayingAndFinishedRetainStableWireValues(
        ServerSnapshotMatchPhase phase,
        byte expectedWireValue)
    {
        ServerSnapshot snapshot = CreateMinimalSnapshot() with
        {
            Match = DefaultMatch() with { Phase = phase },
        };
        byte[] payload = WriteSnapshot(snapshot);

        Assert.Equal(expectedWireValue, payload[FindMatchPhaseOffset(payload)]);
    }

    [Theory]
    [InlineData(ServerSnapshotPlayerKind.Human, 0)]
    [InlineData(ServerSnapshotPlayerKind.Bot, 1)]
    public void PlayerKindsRetainStableWireValues(ServerSnapshotPlayerKind kind, byte expectedWireValue)
    {
        PlayerSnapshotState player = CreatePlayer(
            1,
            "rifle",
            alive: true,
            lastFiredTick: null,
            reloadCompleteTick: null) with { Kind = kind };
        byte[] payload = WriteSnapshot(CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [player],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone()));

        const int firstPlayerKindOffset = sizeof(ulong) + 5 + 1 + sizeof(ushort) + sizeof(uint);
        Assert.Equal(expectedWireValue, payload[firstPlayerKindOffset]);
    }

    [Fact]
    public void SnapshotPayloadRejectsInvalidPlayerKindForWriteAndRead()
    {
        PlayerSnapshotState invalidPlayer = CreatePlayer(
            1,
            "rifle",
            alive: true,
            lastFiredTick: null,
            reloadCompleteTick: null) with { Kind = (ServerSnapshotPlayerKind)0xFF };
        ServerSnapshot invalidSnapshot = CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [invalidPlayer],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone());
        byte[] destination = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];

        Assert.False(ServerSnapshotPayloadSerializer.TryWriteSnapshot(invalidSnapshot, destination, out int bytesWritten));
        Assert.Equal(0, bytesWritten);

        byte[] payload = WriteSnapshot(invalidSnapshot with
        {
            Players = [invalidPlayer with { Kind = ServerSnapshotPlayerKind.Human }],
        });
        const int firstPlayerKindOffset = sizeof(ulong) + 5 + 1 + sizeof(ushort) + sizeof(uint);
        payload[firstPlayerKindOffset] = 0xFF;
        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out _));
    }

    [Fact]
    public void SnapshotPayloadUsesStableLittleEndianLayoutForRepresentativeValues()
    {
        ServerSnapshot snapshot = CreateSnapshot(
            localPlayerId: 0x0A0B0C0D,
            acknowledgedInputSequence: 0x01020304,
            players: [],
            match: new MatchSnapshotState(
                ServerSnapshotMatchPhase.Playing,
                PhaseStartedTick: 0x1122334455667788,
                LivingPlayerCount: -2,
                WinnerPlayerId: null),
            safeZone: new SafeZoneSnapshotState(
                new Vector3(1.0f, -2.5f, 3.25f),
                CurrentRadius: 4.5f,
                TargetRadius: -8.0f,
                LastUpdatedTick: 0x99AABBCCDDEEFF00));
        byte[] payload = WriteSnapshot(snapshot);

        Assert.Equal(
            [
                0x7B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x01, 0x0D, 0x0C, 0x0B, 0x0A,
                0x01, 0x04, 0x03, 0x02, 0x01,
                0x00, 0x00,
                0x01,
                0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,
                0xFE, 0xFF, 0xFF, 0xFF,
                0x00,
                0x00, 0x00, 0x80, 0x3F,
                0x00, 0x00, 0x20, 0xC0,
                0x00, 0x00, 0x50, 0x40,
                0x00, 0x00, 0x90, 0x40,
                0x00, 0x00, 0x00, 0xC1,
                0x00, 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99,
            ],
            payload);
    }

    [Fact]
    public void SnapshotPayloadRejectsInvalidMatchPhase()
    {
        byte[] payload = WriteSnapshot(CreateMinimalSnapshot());
        int matchPhaseOffset = FindMatchPhaseOffset(payload);
        payload[matchPhaseOffset] = 0xFF;

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsInvalidNullableMarker()
    {
        byte[] payload = WriteSnapshot(CreateMinimalSnapshot());
        payload[sizeof(ulong)] = 2;

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsInvalidBooleanMarker()
    {
        byte[] payload = WriteSnapshot(CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [CreatePlayer(1, "rifle", alive: true, lastFiredTick: null, reloadCompleteTick: null)],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone()));
        int aliveOffset = 8 + 5 + 1 + 2 + 4 + 1 + 12 + 12 + 4 + 4 + 4 + 4;
        payload[aliveOffset] = 2;

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out _));
    }

    [Fact]
    public void SnapshotPayloadRoundTripsAuthoritativeCrouchedState()
    {
        PlayerSnapshotState crouched = CreatePlayer(1, "rifle", true, null, null) with { Crouched = true };
        ServerSnapshot decoded = RoundTrip(CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [crouched],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone()));

        Assert.True(Assert.Single(decoded.Players).Crouched);
    }

    [Fact]
    public void SnapshotPayloadRejectsMalformedCrouchedBoolean()
    {
        byte[] payload = WriteSnapshot(CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [CreatePlayer(1, "rifle", true, null, null)],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone()));
        int crouchedOffset = FindMatchPhaseOffset(payload) - 1;
        payload[crouchedOffset] = 2;

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsTruncatedPayload()
    {
        byte[] payload = WriteSnapshot(CreateMinimalSnapshot());

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload.AsSpan(0, payload.Length - 1), out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsPlayerTruncatedBeforeParticipantKind()
    {
        byte[] payload = WriteSnapshot(CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [CreatePlayer(1, "rifle", true, null, null)],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone()));
        const int firstPlayerKindOffset = sizeof(ulong) + 5 + 1 + sizeof(ushort) + sizeof(uint);

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(
            payload.AsSpan(0, firstPlayerKindOffset),
            out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsTrailingBytes()
    {
        byte[] payload = WriteSnapshot(CreateMinimalSnapshot());
        byte[] withTrailingByte = [.. payload, 0xFF];

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(withTrailingByte, out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsOversizedPlayerCount()
    {
        byte[] payload = new byte[8 + 1 + 1 + 2];
        payload[10] = (byte)(ProtocolConstants.MaxSnapshotPlayers + 1);

        Assert.False(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out _));
    }

    [Fact]
    public void SnapshotPayloadRejectsOversizedWeaponId()
    {
        string oversizedWeaponId = new('w', ProtocolConstants.MaxSnapshotWeaponIdLength + 1);
        ServerSnapshot snapshot = CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: null,
            players: [CreatePlayer(1, oversizedWeaponId, alive: true, lastFiredTick: null, reloadCompleteTick: null)],
            match: DefaultMatch(),
            safeZone: DefaultSafeZone());
        byte[] payload = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];

        Assert.False(ServerSnapshotPayloadSerializer.TryWriteSnapshot(snapshot, payload, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void SnapshotPayloadRejectsDestinationTooSmall()
    {
        ServerSnapshot snapshot = CreateMinimalSnapshot();
        Span<byte> destination = stackalloc byte[1];

        Assert.False(ServerSnapshotPayloadSerializer.TryWriteSnapshot(snapshot, destination, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void SnapshotPayloadConstantsBoundFullPayloads()
    {
        Assert.Equal(128, ProtocolConstants.MaxSnapshotPlayers);
        Assert.Equal(64, ProtocolConstants.MaxSnapshotWeaponIdLength);
        Assert.Equal(157, ServerSnapshotPayloadSerializer.MaxPlayerSnapshotStatePayloadSize);
        Assert.Equal(20162, ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize);
        Assert.True(ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize > ProtocolConstants.PacketHeaderSize);

        string maximumWeaponId = new('w', ProtocolConstants.MaxSnapshotWeaponIdLength);
        PlayerSnapshotState[] players = Enumerable.Range(1, ProtocolConstants.MaxSnapshotPlayers)
            .Select(index => CreatePlayer(
                (uint)index,
                maximumWeaponId,
                alive: true,
                lastFiredTick: 1,
                reloadCompleteTick: 2) with
                {
                    LastProcessedInputSequence = 3,
                    LastProcessedInputClientTick = 4,
                })
            .ToArray();
        ServerSnapshot maximumSnapshot = CreateSnapshot(
            localPlayerId: 1,
            acknowledgedInputSequence: 2,
            players,
            match: DefaultMatch() with { WinnerPlayerId = 1 },
            safeZone: DefaultSafeZone());
        byte[] maximumPayload = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];

        Assert.True(ServerSnapshotPayloadSerializer.TryWriteSnapshot(
            maximumSnapshot,
            maximumPayload,
            out int bytesWritten));
        Assert.Equal(maximumPayload.Length, bytesWritten);
    }

    private static ServerSnapshot RoundTrip(ServerSnapshot snapshot)
    {
        byte[] payload = WriteSnapshot(snapshot);

        Assert.True(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out ServerSnapshot? decoded));
        Assert.NotNull(decoded);
        return decoded!;
    }

    private static byte[] WriteSnapshot(ServerSnapshot snapshot)
    {
        byte[] payload = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];
        Assert.True(ServerSnapshotPayloadSerializer.TryWriteSnapshot(snapshot, payload, out int bytesWritten));
        return payload.AsSpan(0, bytesWritten).ToArray();
    }

    private static ServerSnapshot CreateMinimalSnapshot() => CreateSnapshot(
        localPlayerId: null,
        acknowledgedInputSequence: null,
        players: [],
        match: DefaultMatch(),
        safeZone: DefaultSafeZone());

    private static ServerSnapshot CreateSnapshot(
        uint? localPlayerId,
        uint? acknowledgedInputSequence,
        IReadOnlyList<PlayerSnapshotState> players,
        MatchSnapshotState match,
        SafeZoneSnapshotState safeZone) => new(
        ServerTick: 123,
        localPlayerId,
        acknowledgedInputSequence,
        players,
        match,
        safeZone);

    private static PlayerSnapshotState CreatePlayer(
        uint playerId,
        string weaponId,
        bool alive,
        ulong? lastFiredTick,
        ulong? reloadCompleteTick) => new(
        playerId,
        ServerSnapshotPlayerKind.Human,
        new Vector3(1.0f + playerId, 2.0f, 3.0f),
        new Vector3(4.0f, 5.0f, 6.0f),
        YawRadians: 0.5f,
        PitchRadians: -0.25f,
        CurrentHealth: alive ? 80 : 0,
        MaxHealth: 100,
        alive,
        new WeaponSnapshotState(
            weaponId,
            AmmoInMagazine: 27,
            ReserveAmmo: 90,
            NextAllowedFireTick: 130,
            lastFiredTick,
            IsReloading: reloadCompleteTick.HasValue,
            reloadCompleteTick));

    private static MatchSnapshotState DefaultMatch() => new(
        ServerSnapshotMatchPhase.Playing,
        PhaseStartedTick: 60,
        LivingPlayerCount: 0,
        WinnerPlayerId: null);

    private static SafeZoneSnapshotState DefaultSafeZone() => new(
        Center: Vector3.Zero,
        CurrentRadius: 100.0f,
        TargetRadius: 50.0f,
        LastUpdatedTick: 90);

    private static int FindMatchPhaseOffset(byte[] payload)
    {
        int offset = sizeof(ulong);
        offset += payload[offset] == 1 ? sizeof(byte) + sizeof(uint) : sizeof(byte);
        offset += payload[offset] == 1 ? sizeof(byte) + sizeof(uint) : sizeof(byte);
        ushort playerCount = (ushort)(payload[offset] | (payload[offset + 1] << 8));
        offset += sizeof(ushort);

        for (int i = 0; i < playerCount; i++)
        {
            const int playerBeforeWeaponBytes =
                sizeof(uint) +
                sizeof(byte) +
                (sizeof(float) * 3) +
                (sizeof(float) * 3) +
                sizeof(float) +
                sizeof(float) +
                sizeof(int) +
                sizeof(int) +
                sizeof(byte);
            offset += playerBeforeWeaponBytes;
            int weaponIdByteLength = payload[offset];
            offset += sizeof(byte) + weaponIdByteLength;
            offset += sizeof(int) + sizeof(int) + sizeof(ulong);
            offset += payload[offset++] == 1 ? sizeof(ulong) : 0;
            offset += sizeof(byte);
            offset += payload[offset++] == 1 ? sizeof(ulong) : 0;
            offset += payload[offset++] == 1 ? sizeof(uint) : 0;
            offset += payload[offset++] == 1 ? sizeof(uint) : 0;
        }

        return offset;
    }

    private static void AssertSnapshotEqual(ServerSnapshot expected, ServerSnapshot actual)
    {
        Assert.Equal(expected.ServerTick, actual.ServerTick);
        Assert.Equal(expected.LocalPlayerId, actual.LocalPlayerId);
        Assert.Equal(expected.AcknowledgedInputSequence, actual.AcknowledgedInputSequence);
        Assert.Equal(expected.Players, actual.Players);
        Assert.Equal(expected.Match, actual.Match);
        Assert.Equal(expected.SafeZone, actual.SafeZone);
    }
}
