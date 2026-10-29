// SPDX-License-Identifier: GPL-3.0-only
// The battle engine's decays as the pristine specimen (SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750)
// compiles them: each product is formed in an x87 register (24-bit significand at PC24, the register's wide exponent) and then
// stored to a float. CBattleEngine::UpdateRotation (00407a50): the pitch limits' products 00407e1d..00407e23 and
// 00407e8b..00407e91, the rotation velocities 00407f92..00407fc9, the roll 00407fcf..00407ffa, the shakes 0040809d..004080cd.
// CBattleEngine::Move (004081c0): the vibration 0040836e..0040837a. CBattleEngineWalkerPart::Move (00413760): the friction
// 00413967..0041398b, the speed 004139a1..004139d5 and its cap 004139ed..00413a24. CBattleEngine::DeclareOnGround: the jet's
// slowing 0040c94e..0040c977. CBattleEngineJetPart::Move (00410c50): the slowing without energy 004110af..004110d9, its speed
// 004110e2..00411108 and push 0041110e..00411145. CBattleEngineJetPart::HandleGroundEffect (00411630): 004117b7..004117c3.
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A product whose result is subnormal rounds twice in retail: to 24 bits in the register, keeping its wide exponent, and again
/// when the store fits it to float's subnormal grid. C#'s float multiply rounds once, so the two can differ by one unit in the
/// last place; and a square of a subnormal underflows to zero as a float but not in the register. Every input below is authored
/// (found by searching from our own starting points, nothing copied from a recording or a capture), and each case is one where
/// the single rounding gives a different value, so reverting any one site to plain float arithmetic fails the check written for
/// it. The expected words are the exact products rounded as the register and then the store round them.
/// </summary>
public sealed class SubnormalDecayTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    private static HeightField FlatMap()
    {
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x102c), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x1034), 100.0f);
        return HeightField.Load(new ChunkReader(MemBuffer.FromFile("flat-map", raw)));
    }

    /// <summary>A battle engine high above a flat map (or with no map), not on the ground, an object or water.</summary>
    private static BattleEngine Engine(BattleEngineState state, HeightField? map = null)
    {
        var pool = new SPtrSetPool();
        pool.Init(128);
        var events = new EventManager();
        events.Init();
        for (int n = 0; n < 40; n++)
        {
            events.AdvanceTime();
        }

        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), map!, new PhysicsRegistry());
        var engine = new BattleEngine(context)
        {
            Configuration = new BattleEngineData(),
            State = state,
            Pos = new FVector(4, 5, -50),
            LastTimeOnGround = -100,
            LastTimeOnObject = -100,
            LastTimeInWater = -100,
        };
        engine.WalkerPart = new BattleEngineWalkerPart(engine);
        if (state == BattleEngineState.Jet)
        {
            engine.JetPart = new BattleEngineJetPart(engine);
        }

        return engine;
    }

    [Fact]
    public void UpdateRotationStoresEachDecayedVelocityRollAndShakeFromTheRegister()
    {
        // An airborne walker with no pitch to limit: each velocity is decayed by 0.8 after it has turned the engine, the roll
        // (here 0x00221000 + the roll velocity, an exact sum) by 0.8, and the shakes by 0.8 while one of them exceeds 0.001.
        BattleEngine be = Engine(BattleEngineState.Walker);
        be.Yawvel = F(0x005002ca);
        be.Pitchvel = F(0x80503240);
        be.Rollvel = F(0x00502471);
        be.CurrentOrientation.Roll = F(0x00221000);
        be.YawShake = F(0x0050019e);
        be.PitchShake = 0.5f;
        be.RollShake = F(0x80514009);
        be.UpdateRotation();
        Assert.Equal(0x0040023cu, Bits(be.Yawvel));
        Assert.Equal(0x80402834u, Bits(be.Pitchvel));
        Assert.Equal(0x00401d28u, Bits(be.Rollvel));
        Assert.Equal(0x005b5d28u, Bits(be.CurrentOrientation.Roll));
        Assert.Equal(0x0040014cu, Bits(be.YawShake));
        Assert.Equal(0x3ecccccdu, Bits(be.PitchShake));
        Assert.Equal(0x80410008u, Bits(be.RollShake));

        // One float rounding of the same products: each differs (so each check above can see its own site reverted).
        Assert.Equal(0x0040023bu, Bits(F(0x005002ca) * 0.8f));
        Assert.Equal(0x80402833u, Bits(F(0x80503240) * 0.8f));
        Assert.Equal(0x00401d27u, Bits(F(0x00502471) * 0.8f));
        Assert.Equal(0x005b5d27u, Bits((F(0x00221000) + F(0x00502471)) * 0.8f));
        Assert.Equal(0x0040014bu, Bits(F(0x0050019e) * 0.8f));
        Assert.Equal(0x80410007u, Bits(F(0x80514009) * 0.8f));

        be.YawShake = 0.5f;
        be.PitchShake = F(0x8050343e);
        be.RollShake = 0;
        be.UpdateRotation();
        Assert.Equal(0x804029ccu, Bits(be.PitchShake));
        Assert.Equal(0x804029cbu, Bits(F(0x8050343e) * 0.8f));
    }

    [Fact]
    public void TheJetsAutoLevelledRollStoresItsProductFromTheRegister()
    {
        BattleEngine be = Engine(BattleEngineState.Jet);
        Assert.True(be.JetPart!.AutoLevel());
        be.CurrentOrientation.Roll = F(0x0047575d);
        be.UpdateRotation();
        Assert.Equal(0x00453376u, Bits(be.CurrentOrientation.Roll));
        Assert.Equal(0x00453377u, Bits(F(0x0047575d) * 0.97f));
    }

    [Theory]
    // Pitch 1.2 and -1.2: the limit's factor 1 - (|pitch| - 1.17) * 1.5 is 0x3f747adf; the limited velocity is stored, then
    // decayed by 0.8 and stored again. Rounding either product once instead gives a different result.
    [InlineData(0x3f99999au, 0x00661390u, 0x004dfc84u)]
    [InlineData(0xbf99999au, 0x8053e5c4u, 0x80401904u)]
    public void AirbornePitchLimitsStoreTheirProductBeforeTheDecay(uint pitch, uint pitchvel, uint expected)
    {
        BattleEngine be = Engine(BattleEngineState.Walker);
        be.CurrentOrientation.Pitch = F(pitch);
        be.Pitchvel = F(pitchvel);
        be.UpdateRotation();
        Assert.Equal(expected, Bits(be.Pitchvel));
    }

    [Theory]
    // A walker on flat ground (ground pitch -0): pitch 0.55 gives the factor 1 - (0.55 - 0.5) * 6 = 0x3f333332 for a rising
    // pitch velocity, pitch -0.85 the factor 1 - |(-0.85 + 0.8) * 6| = 0x3f333332 for a falling one.
    [InlineData(0x3f0ccccdu, 0x0072c93cu, 0x004047bcu)]
    [InlineData(0xbf59999au, 0x80743b9au, 0x80411724u)]
    public void WalkerGroundPitchLimitsStoreTheirProductBeforeTheDecay(uint pitch, uint pitchvel, uint expected)
    {
        BattleEngine be = Engine(BattleEngineState.Walker, FlatMap());
        be.Pos = new FVector(4, 5, 0);
        be.LastTimeOnGround = be.Context.Events.Time;
        Assert.True(be.IsOnGround());
        be.CurrentOrientation.Pitch = F(pitch);
        be.Pitchvel = F(pitchvel);
        be.UpdateRotation();
        Assert.Equal(expected, Bits(be.Pitchvel));
    }

    [Fact]
    public void TheVibrationDecayStoresItsProductFromTheRegister()
    {
        BattleEngine be = Engine(BattleEngineState.Walker);
        be.Vibration = F(0x00499006);
        be.DecayVibration();
        Assert.Equal(0x00337e6au, Bits(be.Vibration));
        Assert.Equal(0x00337e6bu, Bits(F(0x00499006) * 0.7f));
    }

    [Fact]
    public void WalkingFrictionStoresEachComponentFromTheRegister()
    {
        // An airborne dash (the walker's friction applies on the ground or while dashing); the count is at the cap's limit, so
        // only the friction runs.
        BattleEngine be = Engine(BattleEngineState.Walker);
        be.Configuration!.WalkFriction = 0.7f;
        be.Configuration.MaxWalkVelocity = 0.25f;
        be.WalkerPart!.DoingDashCount = 15;
        be.Velocity = new FVector(F(0x0021e021), F(0x8057f021), F(0x00340021));
        be.WalkerPart.Move();
        Assert.Equal(0x0017b67eu, Bits(be.Velocity.X));
        Assert.Equal(0x803d8e7eu, Bits(be.Velocity.Y));
        Assert.Equal(0x0024667eu, Bits(be.Velocity.Z));
        Assert.Equal(0x0017b67du, Bits(F(0x0021e021) * 0.7f));
        Assert.Equal(0x803d8e7du, Bits(F(0x8057f021) * 0.7f));
        Assert.Equal(0x0024667du, Bits(F(0x00340021) * 0.7f));
    }

    [Fact]
    public void TheWalkingSpeedCapKeepsTheSquaresInTheRegister()
    {
        // An authored subnormal maximum speed (0x0062c9f2) just below the friction-slowed speed: the register keeps the squares
        // of the subnormal components (a float would hold zero), so the stored speed 0x006b6112 exceeds the maximum and the cap
        // scales both components by maximum / speed (0x3f6b851f, kept in the register) and stores each product.
        BattleEngine be = Engine(BattleEngineState.Walker);
        be.Configuration!.WalkFriction = 0.7f;
        be.Configuration.MaxWalkVelocity = F(0x0062c9f2);
        be.WalkerPart!.DoingDashCount = 4;
        be.Velocity = new FVector(F(0x00716351), F(0x8067511d), 0);
        be.WalkerPart.Move();
        Assert.Equal(0x00490598u, Bits(be.Velocity.X));
        Assert.Equal(0x8042893au, Bits(be.Velocity.Y));
        Assert.Equal(0u, Bits(be.Velocity.Z));
        Assert.Equal(3, be.WalkerPart.DoingDashCount);

        // Uncapped (float squares underflow to zero), the components would keep their friction values 0x004f5f1f and
        // 0x80485261; capped with one rounding per product they would be 0x00490597 and 0x8042893b.
        Assert.Equal(0.0f, F(0x004f5f1f) * F(0x004f5f1f));
        Assert.Equal(0x00490597u, Bits(F(0x3f6b851f) * F(0x004f5f1f)));
        Assert.Equal(0x8042893bu, Bits(F(0x3f6b851f) * F(0x80485261)));
    }

    [Fact]
    public void AJetOnTheGroundStoresItsSlowedVelocityFromTheRegister()
    {
        // The ground tests' jet (BattleEngineGroundTests): far below the hard-landing cap, a jet's velocity is slowed by 0.9.
        BattleEngine be = Engine(BattleEngineState.Jet, FlatMap());
        be.Configuration = new BattleEngineData { Life = 20, Energy = 12, ShieldEfficiency = 50 };
        be.Pos = new FVector(4, 5, 0);
        be.Life = 20;
        be.Energy = 7;
        be.Shields = 10;
        be.Vulnerable = true;
        be.Player = new Player(1, be.Context);
        be.Velocity = new FVector(F(0x0022e01b), F(0x80443800), F(0x00313003));
        be.DeclareOnGround();
        Assert.Equal(0x001f634cu, Bits(be.Velocity.X));
        Assert.Equal(0x803d659au, Bits(be.Velocity.Y));
        Assert.Equal(0x002c44d0u, Bits(be.Velocity.Z));
        Assert.Equal(0x001f634bu, Bits(F(0x0022e01b) * 0.9f));
        Assert.Equal(0x803d6599u, Bits(F(0x80443800) * 0.9f));
        Assert.Equal(0x002c44cfu, Bits(F(0x00313003) * 0.9f));
    }

    [Fact]
    public void AJetWithoutEnergyStoresItsSlowedVelocityAndPushesByTheRegistersSpeed()
    {
        // No energy: the velocity is slowed by 0.95 and stored; its speed ((Z*Z + X*X) + Y*Y, square-rooted, about 7.65e-39) stays
        // in the register, which a float's squares (zero) would lose; the forward column (here 0, 1, 0) times that speed is
        // stored, times 0.05 stored again, and added. Y ends at 0x00344785 if the slowing rounds once (0x00301d4f), at
        // 0x00301d50 without the push (float squares), and at 0x00344787 if the push rounds once.
        BattleEngine be = Engine(BattleEngineState.Jet, FlatMap());
        be.Energy = 0;
        be.TransformStartTime = be.Context.Events.Time;
        be.Orientation = FMatrix.Identity;
        be.Velocity = new FVector(F(0x00328252), F(0x0032a597), F(0x8032b6a8));
        be.JetPart!.Move();
        Assert.Equal(0x002ffbceu, Bits(be.Velocity.X));
        Assert.Equal(0x00344786u, Bits(be.Velocity.Y));
        Assert.Equal(0x80302d86u, Bits(be.Velocity.Z));
        Assert.Equal(0x00301d4fu, Bits(F(0x0032a597) * 0.95f));
    }

    [Fact]
    public void TheGroundEffectStoresItsDampedDescentFromTheRegister()
    {
        BattleEngine be = Engine(BattleEngineState.Jet, FlatMap());
        be.Energy = 1;
        be.Pos = new FVector(4, 5, -1);
        be.Orientation = FMatrix.Identity;
        be.Velocity = new FVector(0, 0, F(0x00499000));
        be.JetPart!.HandleGroundEffect();
        Assert.Equal(0x004234ccu, Bits(be.Velocity.Z));
        Assert.Equal(0x004234cdu, Bits(F(0x00499000) * 0.9f));
    }
}
