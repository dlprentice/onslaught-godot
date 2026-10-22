// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Core;

/// <summary>
/// The released weapon a Level 100 actor carries in a named mesh slot.
/// Both bindings come from the <c>CUnitUse</c> statements on the shipped
/// <c>Unit</c> records and are corroborated independently by the drone mesh,
/// whose three non-collidable parts are named <c>GUNA</c>, <c>GUNA</c> and
/// <c>GUNB</c>.
/// </summary>
public enum Level100ActorWeaponKind
{
    DroneVulcanCannon = 0,
    ForsetiDroneMissileLauncher = 1,
    ForsetiMissileTrainerLauncher = 2,
}

/// <summary>The released <c>Round</c> record an actor weapon launches.</summary>
public enum Level100ActorRoundKind
{
    Blaster = 0,
    ForsetiMissile = 1,
}

public sealed record Level100ActorWeaponSnapshot(
    Level100ActorId ActorId,
    Level100ActorWeaponKind Weapon,
    int ReloadBaseTicksRemaining,
    int BurstShotsRemaining,
    int BurstDelayBaseTicksRemaining);

public sealed record Level100ActorRoundSnapshot(
    int Id,
    Level100ActorId OwnerActorId,
    Level100ActorId TargetActorId,
    Level100ActorRoundKind Kind,
    SimVector3 PositionMillimeters,
    int YawMicroRadians,
    int PitchMicroRadians,
    int RemainingBaseTicks,
    int ElapsedBaseTicks,
    bool Locked);

/// <summary>
/// A released actor round reaching its designated target. Damage is incoming
/// milli-life; the Battle Engine still has to route it through shields.
/// </summary>
public sealed record Level100ActorRoundImpact(
    Level100ActorId TargetActorId,
    Level100ActorId OwnerActorId,
    Level100ActorRoundKind Kind,
    SimVector3 SourcePositionMillimeters,
    int IncomingDamageMilliLife);

/// <summary>
/// The released gameplay pseudo-random stream.
///
/// <para><c>Random__NextLCGAbs</c> @<c>0x004de8d0</c> is a Schrage-decomposed
/// Lehmer step whose two constants are shipped dwords: <c>a</c> =
/// <c>0x006321f0</c> = 48271 and <c>m</c> = <c>0x006321f4</c> =
/// <b>214783647</b>. That modulus is not the textbook MINSTD 2147483647 - the
/// shipped constant is one digit short - and because <c>m % a</c> (25968)
/// exceeds <c>m / a</c> (4449) the Schrage identity does not hold for it. The
/// reconstruction reproduces the shipped arithmetic exactly, including that
/// defect, because the shipped stream is the behaviour.</para>
///
/// <para>The stream is seeded to the constant 123456 (<c>0x1E240</c>) by
/// <c>CGame::InitRestartLoop</c> @<c>0x0046c430</c> at every level start, so
/// retail's own weapon scatter is reproducible from the first shot. This class
/// therefore introduces no non-determinism into
/// <c>OnslaughtRebuild.Core</c>: it is a pure integer function of a seed that
/// is part of the hashed simulation state.</para>
/// </summary>
public sealed class Level100ReleasedRandom
{
    private const int Multiplier = SimulationConstants.Level100ReleasedRandomMultiplier;
    private const int Modulus = SimulationConstants.Level100ReleasedRandomModulus;
    private const int Quotient = Modulus / Multiplier;
    private const int Remainder = Modulus % Multiplier;

    private int _seed;

    public Level100ReleasedRandom()
        : this(SimulationConstants.Level100ReleasedRandomInitialSeed)
    {
    }

    public Level100ReleasedRandom(int seed)
    {
        _seed = seed;
    }

    public int Seed => _seed;

    /// <summary>
    /// One released step. Transcribed instruction for instruction from the
    /// shipped body: the sign test is <c>&lt; 1</c>, not <c>&lt;= 0</c> on a
    /// different quantity, and the return is <c>abs(seed)</c> rather than the
    /// seed itself.
    /// </summary>
    public int Next()
    {
        // The shipped body is entirely 32-bit `imul`/`sub`/`add` with no
        // overflow handling, and because the shipped modulus breaks the
        // Schrage precondition (`m % a` > `m / a`) the stream really does
        // leave [1, m) and really does wrap. `unchecked` is therefore the
        // faithful transcription, not a convenience: a widened intermediate
        // would produce a different stream from the shipped executable.
        // C# `%` and `/` truncate toward zero exactly as x86 `idiv` does, so
        // negative seeds carry through identically.
        unchecked
        {
            int stepped =
                ((_seed % Quotient) * Multiplier) -
                ((_seed / Quotient) * Remainder);
            _seed = stepped < 1 ? Modulus + stepped : stepped;
            return _seed < 0 ? -_seed : _seed;
        }
    }

    /// <summary>
    /// The released scatter sample:
    /// <c>((float)(NextLCGAbs() % 65536) * (1/32768) - 1.0) * scale</c>, taken
    /// in exact integer arithmetic. Both <c>1/32768</c>
    /// (<c>0x005d8de4</c>) and <c>1.0</c> (<c>0x005d8568</c>) are shipped
    /// <c>.rdata</c> dwords.
    /// </summary>
    public int NextSignedUnitScaled(int scale)
    {
        // `uVar5 & 0x8000ffff` plus the sign fix-up is the MSVC idiom for a
        // SIGNED `% 65536`: the remainder keeps the dividend's sign. Next()
        // sign-normalizes every return except INT_MIN, whose remainder is
        // exactly zero, so the actual domain is still [0, 65535]. Internal
        // _seed values do become negative; confusing those with Next's return
        // had previously overstated the scatter range.
        int sample = Next() % SimulationConstants.Level100ReleasedRandomUnitModulus;
        long offset =
            (long)sample - SimulationConstants.Level100ReleasedRandomUnitDivisor;
        long product = offset * scale;
        long divisor = SimulationConstants.Level100ReleasedRandomUnitDivisor;
        return checked((int)(product >= 0
            ? (product + (divisor / 2)) / divisor
            : -((-product + (divisor / 2)) / divisor)));
    }
}

/// <summary>
/// The immutable released parameters of one actor weapon mode. Every field is
/// a dword out of <c>data/default physics.dat</c>, or the shipped default
/// written by <c>CWeaponModeStatement__Create</c> @<c>0x0042fa80</c> when the
/// record carries no such node.
/// </summary>
internal sealed record Level100ActorWeaponMode(
    Level100ActorWeaponKind Kind,
    Level100ActorRoundKind Round,
    int ReloadBaseTicks,
    int BurstSize,
    int BurstDelayBaseTicks,
    int MinimumRangeMillimeters,
    int MaximumRangeMillimeters,
    int InaccuracyMicroRadians,
    int YawToleranceMicroRadians,
    int PitchWindowMicroRadians);

/// <summary>
/// The immutable released parameters of one actor round record.
/// </summary>
internal sealed record Level100ActorRoundData(
    Level100ActorRoundKind Kind,
    int SpeedMillimetersPerBaseTick,
    int LifeSpanBaseTicks,
    int IncomingDamageMilliLife,
    bool Seeks,
    int TurnRateMicroRadians,
    int SeekDelayBaseTicks,
    int SeekAngleMicroRadians,
    int WiggleMicroRadians)
{
    /// <summary>
    /// The payload's life span in seconds, the float <c>CRound::Init</c> adds
    /// to the event time for its 4000. Both actor rounds live whole seconds.
    /// </summary>
    internal float LifeSpanSeconds =>
        LifeSpanBaseTicks / (float)Level100ActorMechanics.RetailBaseTicksPerSecond;
}

internal static class Level100ActorArmament
{
    private const int BaseTicksPerSecond = Level100ActorMechanics.RetailBaseTicksPerSecond;

    private static int Milliseconds(int milliseconds) =>
        milliseconds * BaseTicksPerSecond / 1_000;

    /// <summary>
    /// Released damage converted into Core milli-life. Aquila Prototype ships
    /// <c>mLife</c> 20.0 and Core defines a full hull as
    /// <see cref="SimulationConstants.MaximumHull"/>, so one released damage
    /// unit is <c>MaximumHull / 20</c>.
    /// </summary>
    internal static int IncomingDamageMilliLifeFromFloatBits(
        params int[] releasedDamageBits)
    {
        long total = 0;
        foreach (int bits in releasedDamageBits)
        {
            total += ScaleFloatBits(
                bits,
                SimulationConstants.MaximumHull /
                    SimulationConstants.Level100PlayerReleasedLife);
        }
        return checked((int)total);
    }

    private static int ScaleFloatBits(int bits, int scale)
    {
        uint raw = unchecked((uint)bits);
        int exponent = (int)((raw >> 23) & 0xFF);
        uint fraction = raw & 0x7FFFFF;
        if ((raw & 0x80000000) != 0 || exponent == 0xFF)
        {
            throw new InvalidDataException(
                "A Level 100 actor damage scalar is not finite and positive.");
        }
        if (exponent == 0 && fraction == 0)
        {
            return 0;
        }

        ulong significand = exponent == 0 ? fraction : (1U << 23) | fraction;
        int binaryExponent = exponent == 0 ? -149 : exponent - 150;
        ulong scaled = checked(significand * (ulong)scale);
        if (binaryExponent >= 0)
        {
            return checked((int)(scaled << binaryExponent));
        }

        ulong divisor = 1UL << -binaryExponent;
        return checked((int)((scaled + (divisor / 2)) / divisor));
    }

    internal static Level100ActorRoundData Round(Level100ActorRoundKind kind) =>
        kind switch
        {
            Level100ActorRoundKind.Blaster => new Level100ActorRoundData(
                Level100ActorRoundKind.Blaster,
                SpeedMillimetersPerBaseTick:
                    SimulationConstants.Level100BlasterSpeedMillimetersPerSecond /
                        BaseTicksPerSecond,
                LifeSpanBaseTicks:
                    Milliseconds(SimulationConstants.Level100BlasterLifeSpanMilliseconds),
                IncomingDamageMilliLife: IncomingDamageMilliLifeFromFloatBits(
                    SimulationConstants.Level100BlasterDamageFloatBits),
                Seeks: false,
                TurnRateMicroRadians: 0,
                SeekDelayBaseTicks: 0,
                SeekAngleMicroRadians: 0,
                WiggleMicroRadians: 0),
            Level100ActorRoundKind.ForsetiMissile => new Level100ActorRoundData(
                Level100ActorRoundKind.ForsetiMissile,
                SpeedMillimetersPerBaseTick:
                    SimulationConstants
                        .Level100ForsetiMissileSpeedMillimetersPerSecond /
                        BaseTicksPerSecond,
                LifeSpanBaseTicks: Milliseconds(
                    SimulationConstants.Level100ForsetiMissileLifeSpanMilliseconds),
                IncomingDamageMilliLife: IncomingDamageMilliLifeFromFloatBits(
                    SimulationConstants.Level100ForsetiMissileDamageFloatBits,
                    SimulationConstants
                        .Level100ForsetiMissileExplosionDamageFloatBits),
                Seeks: true,
                TurnRateMicroRadians:
                    SimulationConstants.Level100ForsetiMissileTurnRateMicroRadians,
                SeekDelayBaseTicks: Milliseconds(
                    SimulationConstants.Level100ForsetiMissileSeekDelayMilliseconds),
                SeekAngleMicroRadians:
                    SimulationConstants.Level100ForsetiMissileSeekAngleMicroRadians,
                WiggleMicroRadians:
                    SimulationConstants.Level100ForsetiMissileWiggleMicroRadians),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    internal static Level100ActorWeaponMode Mode(Level100ActorWeaponKind kind) =>
        kind switch
        {
            Level100ActorWeaponKind.DroneVulcanCannon => new Level100ActorWeaponMode(
                kind,
                Level100ActorRoundKind.Blaster,
                ReloadBaseTicks: Milliseconds(
                    SimulationConstants.Level100DroneVulcanReloadMilliseconds),
                BurstSize: SimulationConstants.Level100DroneVulcanBurstSize,
                BurstDelayBaseTicks: Milliseconds(
                    SimulationConstants.Level100DroneVulcanBurstDelayMilliseconds),
                MinimumRangeMillimeters:
                    SimulationConstants.Level100DroneVulcanMinimumRangeMillimeters,
                MaximumRangeMillimeters:
                    SimulationConstants.Level100DroneVulcanMaximumRangeMillimeters,
                InaccuracyMicroRadians:
                    SimulationConstants.Level100DroneVulcanInaccuracyMicroRadians,
                YawToleranceMicroRadians:
                    SimulationConstants.Level100DroneVulcanYawToleranceMicroRadians,
                PitchWindowMicroRadians:
                    SimulationConstants.Level100ActorWeaponPitchWindowMicroRadians),
            Level100ActorWeaponKind.ForsetiDroneMissileLauncher =>
                new Level100ActorWeaponMode(
                    kind,
                    Level100ActorRoundKind.ForsetiMissile,
                    ReloadBaseTicks: Milliseconds(
                        SimulationConstants
                            .Level100ForsetiDroneLauncherReloadMilliseconds),
                    BurstSize: 1,
                    BurstDelayBaseTicks: 0,
                    MinimumRangeMillimeters: SimulationConstants
                        .Level100ForsetiLauncherMinimumRangeMillimeters,
                    MaximumRangeMillimeters: SimulationConstants
                        .Level100ForsetiDroneLauncherMaximumRangeMillimeters,
                    InaccuracyMicroRadians: SimulationConstants
                        .Level100ForsetiLauncherInaccuracyMicroRadians,
                    YawToleranceMicroRadians: SimulationConstants
                        .Level100ForsetiLauncherYawToleranceMicroRadians,
                    PitchWindowMicroRadians: SimulationConstants
                        .Level100ActorWeaponPitchWindowMicroRadians),
            Level100ActorWeaponKind.ForsetiMissileTrainerLauncher =>
                new Level100ActorWeaponMode(
                    kind,
                    Level100ActorRoundKind.ForsetiMissile,
                    ReloadBaseTicks: Milliseconds(
                        SimulationConstants
                            .Level100ForsetiTrainerLauncherReloadMilliseconds),
                    BurstSize: 1,
                    BurstDelayBaseTicks: 0,
                    MinimumRangeMillimeters: SimulationConstants
                        .Level100ForsetiLauncherMinimumRangeMillimeters,
                    MaximumRangeMillimeters: SimulationConstants
                        .Level100ForsetiTrainerLauncherMaximumRangeMillimeters,
                    InaccuracyMicroRadians: SimulationConstants
                        .Level100ForsetiLauncherInaccuracyMicroRadians,
                    YawToleranceMicroRadians: SimulationConstants
                        .Level100ForsetiLauncherYawToleranceMicroRadians,
                    PitchWindowMicroRadians: SimulationConstants
                        .Level100ActorWeaponPitchWindowMicroRadians),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    /// <summary>
    /// The released <c>CUnitUse</c> weapon slots for the two Level 100 air
    /// units, in the order the shipped <c>Unit</c> record lists them
    /// (<c>GunA</c> then <c>GunB</c>).
    /// </summary>
    internal static IReadOnlyList<Level100ActorWeaponKind> Slots(
        string definitionName) =>
        definitionName switch
        {
            "Target Drone" =>
            [
                Level100ActorWeaponKind.DroneVulcanCannon,
                Level100ActorWeaponKind.ForsetiDroneMissileLauncher,
            ],
            "Air Trainer" =>
            [
                Level100ActorWeaponKind.ForsetiMissileTrainerLauncher,
            ],
            _ => Array.Empty<Level100ActorWeaponKind>(),
        };
}
