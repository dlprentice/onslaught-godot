// SPDX-License-Identifier: GPL-3.0-only
// The lead time of CBattleEngine::UpdateAutoAim (0040b120; |bp - op| / |bv| * 20, the two magnitudes at 0040b258..0040b293) and of
// CWeapon::GetPredictedTargetPos (0050a0e0; |pos - tpos| at 0050a1ac..0050a1ca and |vel| at 0050a1d4..0050a1f0), read from the
// pristine specimen SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Retail adds the three squares of each of these magnitudes as (Z*Z + Y*Y) + X*X (or (Y*Y + Z*Z) + X*X: a sum commutes), X last;
/// maths.h's Magnitude adds them X, Y, Z. The two groupings round differently at 24 bits. Every input below is authored (nothing is
/// copied from a recording or a capture) and every case is one where the groupings give different float32 lead times: the
/// "distance" case when only the first magnitude's grouping matters, the "velocity" case when only the second's does, so that
/// correcting one magnitude alone cannot satisfy both.
/// </summary>
public sealed class LeadTimeSumOrderTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static FVector V(uint x, uint y, uint z) => new(F(x), F(y), F(z));

    private sealed class TestUnit(ThingContext context) : Unit(context)
    {
        public override float GetBoundingRadius() => 1;
    }

    /// <summary>
    /// One authored encounter. <c>Offset</c> is the vector from the target to the shooter (the target sits at the origin);
    /// <c>Forward</c> is the shooter's forward (Y) column, a unit vector, and the launch vector (0, Speed, 0) reads only that column.
    /// </summary>
    private sealed record Case(FVector Offset, FVector Forward, float Speed, FVector WeaponTargetVelocity, FVector AimTargetVelocity,
        uint RetailLead, uint LegacyLead);

    private static readonly Dictionary<string, Case> Cases = new()
    {
        // The target is 36 units away, the launch speed is 20 and the forward vector is (-13, -16, 4) / 21. Reversing the first
        // magnitude's grouping moves the lead time by one float32 ULP; reversing the second's does not.
        ["distance"] = new(V(0x41a40000, 0xc1e73333, 0xc0a9999a), V(0xbf1e79e8, 0xbf430c31, 0x3e430c31), 20.0f,
            V(0x3fc00000, 0xc0200000, 0x3f000000), V(0x3f000000, 0xbf400000, 0xbe19999a), 0x420f4e82, 0x420f4e81),
        // The target is 37 units away, the launch speed is 100 and the forward vector is (-6, 2, 9) / 11. Reversing the second
        // magnitude's grouping moves the lead time by two ULPs; reversing the first's does not.
        ["velocity"] = new(V(0x417e6666, 0xc2066666, 0xbfc00000), V(0xbf0ba2e9, 0x3e3a2e8c, 0x3f51745d), 100.0f,
            V(0x3fc00000, 0xc0200000, 0x3f000000), V(0x40166666, 0xc09e6666, 0xbe4ccccd), 0x40ee186e, 0x40ee1870),
    };

    /// <summary>sqrtf((Z*Z + Y*Y) + X*X): retail's grouping.</summary>
    private static float MagnitudeZYX(FVector v)
    {
        float zy = (v.Z * v.Z) + (v.Y * v.Y);
        return MathF.Sqrt(zy + (v.X * v.X));
    }

    /// <summary>sqrtf((X*X + Y*Y) + Z*Z): maths.h's grouping.</summary>
    private static float MagnitudeXYZ(FVector v)
    {
        float xy = (v.X * v.X) + (v.Y * v.Y);
        return MathF.Sqrt(xy + (v.Z * v.Z));
    }

    private static float Lead(float distance, float speed)
    {
        float quotient = distance / speed;
        return quotient * 20.0f;
    }

    private static FMatrix ForwardOnly(FVector forward) =>
        new(new FVector(0, forward.X, 0), new FVector(0, forward.Y, 0), new FVector(0, forward.Z, 0));

    private static ThingContext NewContext()
    {
        var pool = new SPtrSetPool();
        pool.Init(64);
        var events = new EventManager();
        events.Init();
        return new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
    }

    private static Weapon NewPredictiveWeapon(ThingContext context, float speed)
    {
        var round = new RoundData("authored"u8.ToArray()) { Speed = speed };
        var mode = new WeaponModeData("authored"u8.ToArray()) { Round = round, Predictive = 1 };
        return new Weapon(context, new WeaponData("authored"u8.ToArray()), 0) { Mode = mode };
    }

    [Theory]
    [InlineData("distance")]
    [InlineData("velocity")]
    public void PredictedTargetPositionAddsBothMagnitudesSquaresXLast(string name)
    {
        Case c = Cases[name];
        ThingContext context = NewContext();
        var shooter = new TestUnit(context) { Pos = c.Offset, Orientation = ForwardOnly(c.Forward) };
        var target = new TestUnit(context) { Pos = new FVector(0, 0, 0), Velocity = c.WeaponTargetVelocity };
        shooter.SetThingType(ThingType.Unit);
        target.SetThingType(ThingType.Unit);
        Weapon weapon = NewPredictiveWeapon(context, c.Speed);
        weapon.Init(new InitEquipment { AttachedTo = shooter, Number = -1 });

        FVector vel = shooter.Orientation.Transform(new FVector(0, c.Speed, 0));
        float retail = Lead(MagnitudeZYX(c.Offset), MagnitudeZYX(vel));
        // The one magnitude the case is about, in maths.h's grouping; the other keeps retail's.
        float legacy = name == "distance"
            ? Lead(MagnitudeXYZ(c.Offset), MagnitudeZYX(vel))
            : Lead(MagnitudeZYX(c.Offset), MagnitudeXYZ(vel));
        Assert.Equal(c.RetailLead, Bits(retail));
        Assert.NotEqual(Bits(retail), Bits(legacy)); // the case tells the groupings apart

        FVector got = weapon.GetPredictedTargetPos(target);
        FVector expected = (c.WeaponTargetVelocity * retail) + new FVector(0, 0, 0);
        FVector wrong = (c.WeaponTargetVelocity * legacy) + new FVector(0, 0, 0);
        Assert.Equal((Bits(expected.X), Bits(expected.Y), Bits(expected.Z)), (Bits(got.X), Bits(got.Y), Bits(got.Z)));
        Assert.NotEqual((Bits(wrong.X), Bits(wrong.Y), Bits(wrong.Z)), (Bits(got.X), Bits(got.Y), Bits(got.Z)));
    }

    [Theory]
    [InlineData("distance")]
    [InlineData("velocity")]
    public void AutoAimLeadTimeAddsBothMagnitudesSquaresXLast(string name)
    {
        Case c = Cases[name];
        ThingContext context = NewContext();
        var engine = new BattleEngine(context) { Configuration = new BattleEngineData(), State = BattleEngineState.Walker };
        engine.WalkerPart = new BattleEngineWalkerPart(engine) { PrimaryWeapon = NewPredictiveWeapon(context, c.Speed) };
        engine.Pos = c.Offset;
        engine.Orientation = ForwardOnly(c.Forward);
        var target = new TestUnit(context) { Pos = new FVector(0, 0, 0), Velocity = c.AimTargetVelocity };
        target.SetThingType(ThingType.Unit);
        engine.AutoAimTarget.SetReader(target);

        FVector bv = engine.Orientation.Transform(new FVector(0, c.Speed, 0));
        float retail = Lead(MagnitudeZYX(c.Offset), MagnitudeZYX(bv)); // 0040b258..0040b293: both magnitudes
        float legacy = Lead(MagnitudeXYZ(c.Offset), MagnitudeXYZ(bv)); // maths.h's grouping for both, as the rebuild had it
        Assert.Equal(c.RetailLead, Bits(retail));
        Assert.Equal(c.LegacyLead, Bits(legacy));

        // What the engine does with the lead: the target's position led by its velocity, then the angles to it from the
        // shooter's (zero) heading and pitch. Both leads are checked to give different offsets for this case.
        (float yaw, float pitch) Offsets(float lead)
        {
            FVector diff = (new FVector(0, 0, 0) + (c.AimTargetVelocity * lead)) - c.Offset;
            return (-WeaponMaths.AngleDifference(0.0f, WeaponMaths.AzimuthUnrounded(diff)),
                -WeaponMaths.AngleDifference(0.0f, WeaponMaths.ElevationUnrounded(diff)));
        }

        (float yaw, float pitch) = Offsets(retail);
        (float legacyYaw, float legacyPitch) = Offsets(legacy);
        Assert.NotEqual((Bits(legacyYaw), Bits(legacyPitch)), (Bits(yaw), Bits(pitch)));
        Assert.True(MathF.Abs(yaw) + MathF.Abs(pitch) > 0.2f); // a lead over 0.2 rad becomes the tracking offsets

        engine.UpdateAutoAim();

        Assert.True(engine.TrackingActive);
        Assert.Equal((Bits(yaw), Bits(pitch)), (Bits(engine.TrackingYawOffset), Bits(engine.TrackingPitchOffset)));
    }
}
