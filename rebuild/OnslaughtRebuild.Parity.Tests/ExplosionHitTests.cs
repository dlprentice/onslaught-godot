// SPDX-License-Identifier: GPL-3.0-only
// Authored inputs executed through unchanged retail CExplosion::Hit0044bf10.
// Pristine SHA-25674154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// Target radius and Damage sink are isolated service boundaries; no gameplay/trajectory claim.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ExplosionHitTests
{
    private sealed class Target(ThingContext context, float radius) : Thing(context, Oid.Thing)
    {
        public readonly List<(float Amount, Thing? Owner, bool Shields, int Part)> Hits = [];
        public override float GetRadius() => radius;
        public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
            => Hits.Add((amount, byThis, damageShields, meshPart));
    }
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    // Six summation-order counterexamples, then retained-exponent overflow/underflow controls.
    [Theory]
    [InlineData(0xc071d2bfu, 0xc039747cu, 0x3fe73aa7u, 0x40a00000u, 0x3ecccccdu, 0x40000000u, 10, 0x3c4949ecu)]
    [InlineData(0x3fa8f644u, 0xbfa833bfu, 0x402135c1u, 0x40a00000u, 0x3ecccccdu, 0x40000000u, 17, 0x3d5a826bu)]
    [InlineData(0x401cdb15u, 0x3f3e04feu, 0x40641281u, 0x40a00000u, 0x3ecccccdu, 0x40000000u, 17, 0x3cc3020cu)]
    [InlineData(0xc060f1e3u, 0x405176ceu, 0x3eb4a645u, 0x40a00000u, 0x3ecccccdu, 0x40000000u, 2, 0x3def614du)]
    [InlineData(0xbdb85bebu, 0xc040b23au, 0x3fa654c7u, 0x40a00000u, 0x3ecccccdu, 0x40000000u, 17, 0x3d4c438fu)]
    [InlineData(0x4076be2du, 0xbeed9c00u, 0x3fba9ddeu, 0x40a00000u, 0x3ecccccdu, 0x40000000u, 17, 0x3cf15a85u)]
    [InlineData(0x61821ab1u, 0x00000000u, 0x00000000u, 0x6322a15du, 0x00000000u, 0x3f800000u, 1, 0x3f666667u)]
    [InlineData(0x0da24260u, 0x00000000u, 0x00000000u, 0x0f4ad2f8u, 0x00000000u, 0x3f800000u, 1, 0x3f666666u)]
    public void OriginalFalloffKeepsTheInlinedDistanceOrderAndRange(
        uint x, uint y, uint z, uint radius, uint targetRadius, uint damage, int frames, uint expected)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var target = new Target(context, F(targetRadius));
        var explosion = new Explosion(context)
        {
            Pos = new FVector(F(x), F(y), F(z)), Radius = F(radius), CurrentRadius = F(radius),
            Behaviour = new ExplosionData("authored"u8.ToArray()) { Damage = F(damage), Time = frames }
        };
        explosion.Hit(target, null);
        var hit = Assert.Single(target.Hits);
        Assert.Equal(expected, BitConverter.SingleToUInt32Bits(hit.Amount));
        Assert.Same(explosion, hit.Owner); Assert.True(hit.Shields); Assert.Equal(-1, hit.Part);
    }
}
