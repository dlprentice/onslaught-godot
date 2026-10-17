// SPDX-License-Identifier: GPL-3.0-or-later
// The descriptors' per-particle steps (their virtual slots 9..22). Adapted from bea-decomp src/ParticleDescriptor.cpp,
// ParticleDescriptor.h (CPDFunction::Evaluate 004c0c70, CFloatRef::GetValue 004c10c0) and ParticleSet.cpp (the inline
// slots). Slot names follow retail's numbering where the purpose is not certain.
//
// The arithmetic is single precision in source order; the x87's wider intermediates are not modelled here, so a value
// can differ from retail's in its last bits. Every rand() draw is made in retail's order (VC6 evaluates call arguments
// right to left, so FVector(rand, rand, rand) draws Z first).

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Particles;

public abstract partial class ParticleDescriptor
{
    /// <summary>Unknown9: a new particle's set-up.</summary>
    public abstract void Unknown9(ParticleManager m, Particle p);

    /// <summary>Unknown10: one step; TRUE when the particle dies.</summary>
    public virtual bool Unknown10(ParticleManager m, Particle p) => p.Dead = true;

    /// <summary>Unknown12: a mover's step at x.</summary>
    public virtual void Unknown12(ParticleManager m, Particle p, float x)
    {
    }

    /// <summary>Unknown13: a modifier's step.</summary>
    public virtual void Unknown13(ParticleManager m, Particle p)
    {
    }

    /// <summary>Unknown14: whether the particle's data is freed with it.</summary>
    public virtual bool Unknown14(Particle p) => true;

    /// <summary>Unknown15: after an unlinked particle is created.</summary>
    public virtual void Unknown15(ParticleManager m, Particle p)
    {
    }

    /// <summary>Unknown16: the descriptor to use instead (a selector's choice).</summary>
    public virtual ParticleDescriptor? Unknown16(ParticleManager m) => null;

    /// <summary>Unknown17: a shape's particle count.</summary>
    public virtual int Unknown17() => 0;

    /// <summary>Unknown18: a shape places the i'th of n.</summary>
    public virtual void Unknown18(ParticleManager m, Particle p, int i, int n)
    {
    }

    /// <summary>Unknown21 (004c0140): its frame stopped; the particle ends.</summary>
    public virtual void Unknown21(ParticleManager m, Particle p) => p.Life = 0;

    /// <summary>Unknown22 (004c0490): this frame's particles, newest first from the newest when the frame began.</summary>
    public virtual void Unknown22(ParticleManager m)
    {
        if (OldestParticle is null)
        {
            return;
        }

        for (Particle? p = NewestAtUpdate; p is not null; p = p.PDPrev)
        {
            p.Dead |= Unknown10(m, p);
        }
    }

    /// <summary>AddParticle (004c0510): newest last; the first puts the descriptor on the set's live list.</summary>
    public void AddParticle(ParticleManager m, Particle p)
    {
        if (OldestParticle is null)
        {
            m.Set.LinkNode(this);
            NewestParticle = OldestParticle = p;
            p.PDPrev = p.PDNext = null;
        }
        else
        {
            NewestParticle!.PDNext = p;
            p.PDPrev = NewestParticle;
            NewestParticle = p;
        }
    }

    /// <summary>RemoveParticle (004c0560): the last off takes the descriptor off the live list.</summary>
    public void RemoveParticle(ParticleSet set, Particle p)
    {
        if (NewestAtUpdate == p)
        {
            NewestAtUpdate = null;
        }

        if (p.PDPrev is null)
        {
            OldestParticle = p.PDNext;
        }
        else
        {
            p.PDPrev.PDNext = p.PDNext;
        }

        if (p.PDNext is null)
        {
            NewestParticle = p.PDPrev;
        }
        else
        {
            p.PDNext.PDPrev = p.PDPrev;
        }

        p.PDPrev = p.PDNext = null;
        if (OldestParticle is null)
        {
            set.UnlinkNode(this);
        }
    }

    /// <summary>TransmitFoR (004c0150): share the frame, or move out of it into world space.</summary>
    public static void TransmitIntoFoR(Particle p, FoR? f, bool transmit)
    {
        if (transmit)
        {
            p.FoR = f;
            return;
        }

        if (f is null)
        {
            return;
        }

        if (f.HasOrientation)
        {
            p.Pos = f.Orientation.Transform(p.Pos);
            p.Orientation = p.Orientation.Multiply(f.Orientation);
            p.Velocity = f.Orientation.Transform(p.Velocity);
        }

        p.Pos += f.Pos;
    }

    /// <summary>A selector chain resolved to the descriptor it picks.</summary>
    protected static ParticleDescriptor? Resolve(ParticleManager m, ParticleDescriptor? pd)
    {
        while (pd is { Type: PdType.Selector })
        {
            pd = pd.Unknown16(m);
        }

        return pd;
    }

    /// <summary>The manager's AllocateParticle with the frame's importance (`mFoR && (flags | 1)`: any frame).</summary>
    protected static Particle? AllocateFrom(ParticleManager m, ParticleDescriptor? pd, Particle parent) =>
        m.AllocateParticle(pd, parent.FoR is not null);

    /// <summary>(rand() &amp; 0xff) - 128, scaled to +-scale.</summary>
    protected static float RandomScaled(ParticleManager m, float scale)
    {
        int r = (m.Rand.Rand() & 0xff) - 128;
        return r * scale * (1.0f / 128.0f);
    }

    /// <summary>FVector(RandomScaled, RandomScaled, RandomScaled): the arguments drawn right to left.</summary>
    protected static FVector RandomVector(ParticleManager m, float scale)
    {
        float z = RandomScaled(m, scale);
        float y = RandomScaled(m, scale);
        float x = RandomScaled(m, scale);
        return new FVector(x, y, z);
    }

    /// <summary>A new particle's first steps, as each spawner gives them: processed, then its Unknown15.</summary>
    protected static void Launch(ParticleManager m, Particle np)
    {
        ParticleDescriptor pd = np.PD!;
        m.ProcessParticle(np);
        pd.Unknown15(m, np);
    }
}

public static class FloatRefs
{
    /// <summary>CFloatRef::GetValue (004c10c0): the value, or the function's value at x times it.</summary>
    public static float GetValue(this FloatRef r, ParticleManager m, float x, Particle? p)
    {
        if (r.Ref is not PdFunction f)
        {
            if (r.Ref is not null)
            {
                throw new NotSupportedException($"a float reference to a {r.Ref.Type} descriptor (retail reads it as a function)");
            }

            return r.Value;
        }

        if (f.ValueType == 0)
        {
            return f.Evaluate(m, x, p) * r.Value;
        }

        float t = m.Time;
        float scale = f.GameturnScale.GetValue(m, t, p);
        if (f.OffsetGameturn != 0 && p is not null)
        {
            t -= p.BirthTime;
        }

        return f.Evaluate(m, (float)((double)t % scale) / scale, p) * r.Value;
    }

    /// <summary>GetValueByAge: at (life - left) / life.</summary>
    public static float GetValueByAge(this FloatRef r, ParticleManager m, float left, float life, Particle? p) =>
        r.GetValue(m, (life - left) / life, p);
}

public sealed partial class PdFunction
{
    /// <summary>Evaluate (004c0c70): a(x) * f(b(x) * x + c(x)) + d(x), clipped to -1..1 or wrapped.</summary>
    public float Evaluate(ParticleManager m, float x, Particle? p)
    {
        float input = x;
        x = (ParamB.GetValue(m, x, p) * x) + ParamC.GetValue(m, x, p);
        switch (ParamFunction)
        {
            case 1: x *= x; break;
            case 2: x = (float)Math.Exp(x); break;
            case 3: x = (float)Math.Sin(x); break;
            case 4: x = (float)Math.Cos(x); break;
            case 5:
                if (x != 0.0f)
                {
                    x = 1.0f / x;
                }

                break;
            case 6: x = (float)Math.Log(x); break;
            case 10: x = ((m.Rand.Rand() & 0xff) - 128) * (1.0f / 128.0f); break;
        }

        float r = (ParamA.GetValue(m, input, p) * x) + ParamD.GetValue(m, input, p);
        switch (Clip)
        {
            case 0:
                r = r > 1.0f ? 1.0f : r < -1.0f ? -1.0f : r;
                break;
            case 1:
                if (r > -1.0f)
                {
                    r = (float)((double)(r + 1.0f) % 2.0);
                    r -= 1.0f;
                }
                else
                {
                    r = -r;
                    r = (float)((double)(r + 1.0f) % 2.0);
                    r = -(r - 1.0f);
                }

                break;
        }

        return r;
    }

    /// <summary>GetValue: at x, or at the game time wrapped by the scale for a time function.</summary>
    public float GetValue(ParticleManager m, float x, Particle? p)
    {
        if (ValueType == 0)
        {
            return Evaluate(m, x, p);
        }

        float t = m.Time;
        float scale = GameturnScale.GetValue(m, x, p);
        if (OffsetGameturn != 0 && p is not null)
        {
            t -= p.BirthTime;
        }

        return Evaluate(m, (float)((double)t % scale) / scale, p);
    }

    public override void Unknown9(ParticleManager m, Particle p)
    {
    }
}

public sealed partial class PdSimpleSprite
{
    /// <summary>Unknown9 (004c0ab0): a life of Life_Pct..1 of Life at random, the radius at birth, the start frame.</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        float spread = (1.0f - LifePct) * 0.5f;
        int random = (m.Rand.Rand() & 0xff) - 128;
        float centre = (1.0f - LifePct) * 0.5f;
        p.Life = (centre + (random * spread * (1.0f / 128.0f)) + LifePct) * Life;
        p.Radius = Radius.GetValue(m, 0.0f, p);
        p.Frame = 0;
        if (RandomStartFrame != 0)
        {
            float frames = GetNumFrames() * 0.5f;
            p.Frame = (RandomScaled(m, 1.0f) + 1.0f) * frames;
        }

        p.U0 = U0;
        p.U1 = U1;
        p.V0 = V0;
        p.V1 = V1;
        p.Unknown7E = (short)((m.State.SpriteCount & 0xf) + 0xc0);
        m.State.SpriteCount++;
    }

    /// <summary>Unknown15 (004c0a50): a tiny unlinked sprite far from the camera dies at once.</summary>
    public override void Unknown15(ParticleManager m, Particle p)
    {
        if (p.FoR is null)
        {
            float d = m.GetDistanceSq(p);
            if (Radius.Value * Radius.Value / d * 102400.0f < 0.5f && TwoD == 0)
            {
                Unknown21(m, p);
            }
        }
    }

    /// <summary>Unknown10 (004c14f0): grow toward the final radius, fall, slow, change and animate.</summary>
    public override bool Unknown10(ParticleManager m, Particle p)
    {
        float r = Radius.GetValueByAge(m, p.Life, Life, p);
        p.Radius = Life == 0 ? r : r + ((r * FinalRadius) - r) * (Life - p.Life) / Life;
        if (Gravity != 0)
        {
            p.Velocity.Z += m.FrameLength * 0.003f;
        }

        if (VelocityDamp != 1.0f && m.FirstFrame)
        {
            p.Velocity *= VelocityDamp;
        }

        Modifier?.Unknown13(m, p);
        if (AnimType != 0)
        {
            p.Frame += m.FrameLength * AnimSpeed;
            if (!(p.Frame < GetNumFrames()))
            {
                p.Frame = 0;
            }
        }

        return false;
    }

    /// <summary>Unknown22 (004c15f0): this frame's sprites, newest first: radius, gravity, damping, modifier, animation.</summary>
    public override void Unknown22(ParticleManager m)
    {
        Particle? p = NewestAtUpdate;
        if (p is null)
        {
            return;
        }

        float t = m.FrameLength;
        float gravity = Gravity != 0 ? m.FrameLength * 0.003f : 0.0f;
        float damp = VelocityDamp != 1.0f && m.FirstFrame ? VelocityDamp : 1.0f;
        float scale = Life != 0 ? 1.0f : 0.0f;
        float inv = 1.0f / Life;
        for (; p is not null; p = p.PDPrev)
        {
            float r = Radius.GetValueByAge(m, p.Life, Life, p);
            p.Radius = ((((r * FinalRadius) - r) * (Life - p.Life) * inv) + r) * scale;
            p.Velocity.Z += gravity;
            p.Velocity *= damp;
        }

        if (Modifier is not null)
        {
            for (p = NewestAtUpdate; p is not null; p = p.PDPrev)
            {
                Modifier.Unknown13(m, p);
            }
        }

        if (AnimType != 0)
        {
            float step = t * AnimSpeed;
            float frames = GetNumFrames();
            for (Particle? q = NewestAtUpdate; q is not null; q = q.PDPrev)
            {
                q.Frame += step;
                if (!(q.Frame < frames))
                {
                    q.Frame = 0;
                }
            }
        }
    }
}

public sealed partial class PdEmitter
{
    /// <summary>Unknown9 (004c1a60).</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        p.Life = Life;
        p.Frame = 0;
        p.Radius = 1.0f;
        p.Unknown7E = (short)((m.Rand.Rand() % 20) - 20);
    }

    /// <summary>
    /// Unknown10 (004c1a90): on the first frame, this frame's share of particles (fewer the further the emitter is from
    /// the camera), each placed by the shape, moved into the emitter's orientation and given its velocities.
    /// </summary>
    public override bool Unknown10(ParticleManager m, Particle p)
    {
        Mover?.Unknown12(m, p, (Life - p.Life) / Life);
        if (ParticleDescriptor is not null && m.FirstFrame)
        {
            float rate = EmitPerTurn.GetValueByAge(m, p.Life, Life, p);
            p.Unknown7E--;
            if (p.Unknown7E < 0)
            {
                p.Unknown7E += 20;
                float d = ((float)Math.Sqrt(m.GetDistanceSq(p)) - 30.0f) * (1.0f / 120.0f);
                d = d < 0.0f ? 0.0f : d > 1.0f ? 1.0f : d;
                p.Radius = 1.0f - (d * 0.8f);
            }

            p.Frame += rate * p.Radius;
            int n = (int)Math.Floor(p.Frame);
            p.Frame -= n;
            int count = PassNumParticles != 0 ? n : -1;
            if (Shape is not null && PassNumParticles == 0)
            {
                n = Shape.Unknown17();
            }

            for (int i = 0; i < n; i++)
            {
                ParticleDescriptor? pd = Resolve(m, ParticleDescriptor);
                Particle? np = AllocateFrom(m, pd, p);
                if (np is null)
                {
                    continue;
                }

                if (Shape is not null)
                {
                    Shape.Unknown18(m, np, i, count);
                    float outward = OutwardVelocity.GetValueByAge(m, p.Life, Life, p);
                    if (outward != 0.0f)
                    {
                        np.Velocity += p.Orientation.Transform(np.Pos) * outward;
                    }
                }

                np.Pos = p.Orientation.Transform(np.Pos);
                np.Pos += p.Pos;
                FVector velocity;
                velocity.X = InitialVelocityX.GetValueByAge(m, p.Life, Life, p);
                velocity.Y = InitialVelocityY.GetValueByAge(m, p.Life, Life, p);
                velocity.Z = InitialVelocityZ.GetValueByAge(m, p.Life, Life, p);
                float r = VelocityRandomness.GetValueByAge(m, p.Life, Life, p);
                np.Velocity += RandomVector(m, r) + velocity;
                if (TransmitLife != 0)
                {
                    np.Life = np.Life * p.Life / Life;
                    if (np.Life <= 0.0f)
                    {
                        np.Life = 0.01f;
                    }
                }

                float tv = TransmitVelocity.GetValueByAge(m, p.Life, Life, p);
                if (tv != 0.0f)
                {
                    np.Velocity += p.Velocity * tv;
                }

                TransmitIntoFoR(np, p.FoR, TransmitFoR != 0);
                Launch(m, np);
            }
        }

        return false;
    }
}

public sealed partial class PdModifier
{
    public override void Unknown9(ParticleManager m, Particle p)
    {
    }

    /// <summary>Unknown13 (004c2100): a sideways waft.</summary>
    public override void Unknown13(ParticleManager m, Particle p) =>
        p.Velocity.Y += (((float)Math.Cos(p.Pos.Y * 10.0f) * 0.01f) + 0.003f) * m.FrameLength;
}

public sealed partial class PdSelector
{
    public override void Unknown9(ParticleManager m, Particle p)
    {
    }

    /// <summary>Unknown16 (004c22a0): one of the four at random by their probabilities.</summary>
    public override ParticleDescriptor? Unknown16(ParticleManager m)
    {
        int total = 0;
        for (int i = 0; i < 4; i++)
        {
            if (Probability[i] > 0)
            {
                total += Probability[i];
            }
        }

        int r = m.Rand.Rand() % total;
        int sum = 0;
        for (int i = 0; i < 4; i++)
        {
            if (Probability[i] > 0)
            {
                sum += Probability[i];
                if (r <= sum)
                {
                    return Descriptors[i];
                }
            }
        }

        return null;
    }
}

public sealed partial class PdColourRange
{
    public override void Unknown9(ParticleManager m, Particle p)
    {
    }
}

public sealed partial class PdTimeline
{
    /// <summary>Unknown9 (004c2620).</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        p.Unknown7E = 0;
        p.Unknown80 = 0;
        p.Life = 9999999.0f;
    }

    /// <summary>
    /// Unknown10 (004c2640): fire the entries due at this step; TRUE once past the last. (The GeForce FX branch, which
    /// leaves out two explosions' emitters, needs EXTRA_GEFORCE_FX_FEATURES, which this configuration never sets.)
    /// </summary>
    public override bool Unknown10(ParticleManager m, Particle p)
    {
        if (p.Life <= 0.0f)
        {
            return true;
        }

        while (Entries[p.Unknown80].Time == p.Unknown7E && p.Unknown80 < NumEntries)
        {
            ParticleDescriptor? pd = Resolve(m, Entries[p.Unknown80].Descriptor);
            if (pd is not null)
            {
                Particle? np = AllocateFrom(m, pd, p);
                if (np is not null)
                {
                    np.Pos = p.Pos;
                    TransmitIntoFoR(np, p.FoR, Entries[p.Unknown80].TransmitFoR != 0);
                    Launch(m, np);
                }
            }

            p.Unknown80++;
        }

        if (p.Unknown80 >= NumEntries)
        {
            return true;
        }

        p.Unknown7E++;
        return false;
    }
}

public sealed partial class PdShape
{
    private const float PiM2 = 6.28318530717958647692f;

    public override void Unknown9(ParticleManager m, Particle p)
    {
    }

    public override int Unknown17() => NumParticles;

    /// <summary>Unknown18 (004c2d30): the i'th of n (-1: the shape's own count) on a ring, in a sphere or in a box.</summary>
    public override void Unknown18(ParticleManager m, Particle p, int i, int n)
    {
        if (n == -1)
        {
            n = NumParticles;
        }

        float r = Radius.GetValue(m, 1.0f, p);
        switch (ShapeType)
        {
            case 0:
            {
                float a = i * PiM2 / n;
                float s = (float)Math.Sin(a);
                float c = (float)Math.Cos(a);
                p.Pos = RingAxis switch
                {
                    0 => new FVector(0.0f, s * r, -(c * r)),
                    1 => new FVector(s * r, 0.0f, -(c * r)),
                    2 => new FVector(s * r, -(c * r), 0.0f),
                    _ => p.Pos,
                };
                break;
            }

            case 1:
            {
                int z = (m.Rand.Rand() & 0xff) - 128;
                int y = (m.Rand.Rand() & 0xff) - 128;
                int x = (m.Rand.Rand() & 0xff) - 128;
                p.Pos = new FVector(x * (1.0f / 128.0f), y * (1.0f / 128.0f), z * (1.0f / 128.0f));
                p.Pos.Normalise();
                if (Hollow != 0)
                {
                    p.Pos *= r;
                }
                else
                {
                    p.Pos *= ((m.Rand.Rand() & 0xff) - 128) * r * (1.0f / 128.0f);
                }

                if (Hemisphere != 0)
                {
                    float hz = p.Pos.Z;
                    if (!(hz > 0.0f))
                    {
                        hz = -hz;
                    }

                    p.Pos.Z = -hz;
                }

                break;
            }

            case 2:
            {
                int z = (m.Rand.Rand() & 0xff) - 128;
                int y = (m.Rand.Rand() & 0xff) - 128;
                int x = (m.Rand.Rand() & 0xff) - 128;
                p.Pos = new FVector(x * RandomSX * r * (1.0f / 128.0f), y * RandomSY * r * (1.0f / 128.0f),
                    z * RandomSZ * r * (1.0f / 128.0f));
                break;
            }
        }
    }
}

public sealed partial class PdTrail
{
    /// <summary>Unknown9 (004c35b0).</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        p.Life = Life;
        p.Unknown80 = 0;
    }

    /// <summary>AllocatePoints (004c35d0): a fixed number, or one per segment length of the way its frame has come.</summary>
    private void AllocatePoints(Particle p)
    {
        if (TrailType == 0)
        {
            p.Unknown80 = NumPoints;
        }
        else
        {
            if (p.FoR is not { } f)
            {
                return;
            }

            if (UseSegmentLength != 0)
            {
                FVector d = f.Pos - f.Unknown90;
                p.Unknown80 = Numerics.X87.Fist((float)Math.Sqrt((d.X * d.X) + ((d.Y * d.Y) + (d.Z * d.Z))) / SegmentLength) + 3;
            }
            else
            {
                p.Unknown80 = NumPoints;
            }
        }

        var points = new TrailPoint[p.Unknown80];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new TrailPoint { Unknown24 = 0, Width = 0.2f };
        }

        p.Data = points;
    }

    /// <summary>
    /// Unknown10 (004c36b0): a streaming trail (type 0) adds a point each first frame while it is wide enough; a stretched
    /// trail (type 1) lays its points from the particle to its frame's origin, wiggled, and dies with its frame.
    /// </summary>
    public override bool Unknown10(ParticleManager m, Particle p)
    {
        bool fresh = false;
        if (p.Data is null)
        {
            AllocatePoints(p);
            fresh = true;
        }

        if (Gravity != 0)
        {
            p.Velocity.Z += m.FrameLength * 0.003f;
        }

        if (DisperseRate != 0.0f && p.Data is TrailPoint[] dispersing)
        {
            for (int i = 0; i < p.Unknown80; i++)
            {
                if (dispersing[i].Unknown24 != 0)
                {
                    dispersing[i].Pos += dispersing[i].Unknown10 * m.FrameLength;
                }
            }
        }

        switch (TrailType)
        {
            case 0:
                if (m.FirstFrame)
                {
                    float width = p.Radius;
                    if (width > 0.001f && (p.Life < 0.0f || p.Unknown80 < p.Life))
                    {
                        Unknown19(m, p, p.Pos, width);
                    }
                    else
                    {
                        Unknown20(p);
                    }
                }

                break;

            case 1:
                if (p.Radius > 0.001f && (p.Life < -1.0f || FadePoint < p.Life) && p.FoR is { } f)
                {
                    FVector at = p.Pos;
                    if (f.HasOrientation)
                    {
                        at = f.Orientation.Transform(at);
                    }

                    at += f.Pos;
                    FVector step = (f.Unknown90 - at) * (1.0f / (p.Unknown80 - 2));
                    var points = (TrailPoint[])p.Data!;
                    FVector side = default, up = default;
                    if (ManualWiggleEnabled != 0)
                    {
                        side = FVector.Cross(step, new FVector(0.0f, 0.0f, -1.0f));
                        float mag = side.Magnitude();
                        side = mag > 0.0001f ? new FVector(side.X / mag, side.Y / mag, side.Z / mag) : new FVector(1.0f, 0.0f, 0.0f);
                        up = FVector.Cross(step, side);
                        mag = up.Magnitude();
                        up = mag > 0.0001f ? new FVector(up.X / mag, up.Y / mag, up.Z / mag) : new FVector(0.0f, 1.0f, 0.0f);
                    }

                    p.Unknown7E = 0;
                    for (int i = 0; i < p.Unknown80; i++)
                    {
                        FVector wiggle;
                        if (ManualWiggleEnabled != 0)
                        {
                            float t = i / (float)(p.Unknown80 - 1);
                            float length = WiggleLength.GetValue(m, t, p);
                            float yaw = YawLength.GetValue(m, t, p);
                            FVector across = up * ((float)Math.Sin(yaw) * length);
                            wiggle = (side * ((float)Math.Cos(yaw) * length)) + across;
                        }
                        else if (i == 1 || i == p.Unknown80 - 1)
                        {
                            wiggle = new FVector(0, 0, 0);
                        }
                        else
                        {
                            wiggle = RandomVector(m, WiggleFactor);
                        }

                        points[i].Unknown24 = 1;
                        points[i].Pos = wiggle + at;
                        points[i].Width = p.Radius;
                        points[i].Unknown10 = wiggle * DisperseRate;
                        if (fresh && ImpactSpawnee is not null && i > 1 && i < p.Unknown80 - 1)
                        {
                            ParticleDescriptor? pd = Resolve(m, ImpactSpawnee);
                            if (pd is not null)
                            {
                                Particle? np = AllocateFrom(m, pd, p);
                                if (np is not null)
                                {
                                    np.Pos = wiggle + at;
                                    Launch(m, np);
                                }
                            }
                        }

                        if (i != 0)
                        {
                            at += step;
                        }
                    }

                    p.Unknown7E = (short)(p.Unknown80 - 1);
                }

                break;
        }

        return TrailType == 1 && p.FoR is null;
    }

    /// <summary>Unknown19 (004c4100): a point at pos (in the frame's space) with that width, wiggled and dispersing.</summary>
    public void Unknown19(ParticleManager m, Particle p, FVector pos, float width)
    {
        var points = (TrailPoint[])p.Data!;
        int last = points[p.Unknown7E].Unknown24;
        p.Unknown7E++;
        if (p.Unknown7E >= p.Unknown80)
        {
            p.Unknown7E = 0;
        }

        FVector wiggle = last != 0 ? RandomVector(m, WiggleFactor) : new FVector(0, 0, 0);
        FVector at = pos;
        if (p.FoR is { } f)
        {
            if (f.HasOrientation)
            {
                at = f.Orientation.Transform(at);
            }

            at.X += f.Pos.X;
            at.Y += f.Pos.Y;
            at.Z += f.Pos.Z;
        }

        TrailPoint point = points[p.Unknown7E];
        point.Unknown24 = 1;
        point.Pos = wiggle + at;
        point.Width = width;
        point.Unknown10 = wiggle * DisperseRate;
        p.Unknown7C = (ushort)p.Unknown7E;
    }

    /// <summary>Unknown20 (004c43a0): a gap.</summary>
    public void Unknown20(Particle p)
    {
        p.Unknown7E++;
        if (p.Unknown7E >= p.Unknown80)
        {
            p.Unknown7E = 0;
        }

        ((TrailPoint[])p.Data!)[p.Unknown7E].Unknown24 = 0;
    }

    /// <summary>Unknown21 (004c43e0): a stopped trail lives on until its points have gone.</summary>
    public override void Unknown21(ParticleManager m, Particle p) =>
        p.Life = TrailType == 0 ? p.Unknown80 + 2 : FadePoint;
}

public sealed partial class PdMover
{
    public override void Unknown9(ParticleManager m, Particle p)
    {
    }

    /// <summary>Unknown12 (004c4560): gravity, and the orientation from the angles (each plus its function, times pi).</summary>
    public override void Unknown12(ParticleManager m, Particle p, float x)
    {
        const float Pi = 3.14159265358979f;
        float g = GravityPC.GetValue(m, x, p);
        p.Velocity.Z += m.FrameLength * g * 0.003f;
        float yaw = Yaw, pitch = Pitch, roll = Roll;
        if (YawFunction is PdFunction yf)
        {
            yaw += yf.GetValue(m, x, null) * Pi;
        }

        if (PitchFunction is PdFunction pf)
        {
            pitch += pf.GetValue(m, x, null) * Pi;
        }

        if (RollFunction is PdFunction rf)
        {
            roll += rf.GetValue(m, x, null) * Pi;
        }

        p.Orientation = FMatrix.MakeRotationOutOfLine(yaw, pitch, roll);
    }
}

public sealed partial class PdMesh
{
    /// <summary>Unknown9 (004c4cf0): the life, a random spin (drawn Z first), the mesh's radius.</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        p.Life = Life;
        FVector spin = RandomVector(m, AngularMomentum);
        p.U0 = spin.X;
        p.U1 = spin.Y;
        p.V0 = spin.Z;
        p.Unknown7E = 0;
        if (MeshName is not null)
        {
            p.Radius = m.World.MeshRadius(MeshName);
        }
    }

    /// <summary>Unknown10 (004c4dc0): tumble and fall until the mesh reaches the ground; then settle and spawn the impact.</summary>
    public override bool Unknown10(ParticleManager m, Particle p)
    {
        if ((p.Unknown7C & 1) == 0)
        {
            if (m.FirstFrame)
            {
                p.Orientation = p.Orientation.Multiply(FMatrix.MakeRotationOutOfLine(p.U0, p.U1, p.V0));
            }

            if (Gravity != 0)
            {
                p.Velocity.Z += m.FrameLength * 0.003f;
            }
        }

        if (Bounce != 0 && (p.Unknown7C & 1) == 0)
        {
            FVector pos = p.Pos;
            if (p.FoR is { } f)
            {
                if (f.HasOrientation)
                {
                    pos = f.Orientation.Transform(pos);
                }

                pos += f.Pos;
            }

            if (m.OnGround(pos, p.Radius / 10.0f, out FVector ground))
            {
                if (p.FoR is { } g)
                {
                    ground -= g.Pos;
                    if (g.HasOrientation)
                    {
                        ground = g.Orientation.Transpose().Transform(ground);
                    }
                }

                p.Pos = ground;
                p.Velocity = new FVector(0, 0, 0);
                p.Unknown7C |= 1;
                ParticleDescriptor? pd = Resolve(m, ImpactSpawnee);
                if (pd is not null)
                {
                    Particle? np = AllocateFrom(m, pd, p);
                    if (np is not null)
                    {
                        np.Pos = p.Pos;
                        TransmitIntoFoR(np, p.FoR, TransmitFoR != 0);
                        Launch(m, np);
                    }
                }
            }
        }

        return false;
    }
}

public sealed partial class PdFoR
{
    /// <summary>Unknown9 (004c53f0).</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        p.Life = -2.0f;
        p.Unknown7E = 0;
    }

    /// <summary>
    /// Unknown10 (004c5410): the frame's own effect, made on the first step, follows the particle; once that effect has
    /// ended the particle dies, spawning its death effect where it was.
    /// </summary>
    public override bool Unknown10(ParticleManager m, Particle p)
    {
        if (p.FoR is { } frame && p.Data is ParticleEffectLink flagged && flagged.Effect is { } effect)
        {
            effect.Flags = frame.Flags;
        }

        Mover?.Unknown12(m, p, 1.0f);
        if (p.Unknown7E == 0)
        {
            p.Unknown7E = 1;
            var made = new ParticleEffectLink(m);
            p.Data = made;
            ParticleDescriptor? pd = Resolve(m, Initial);
            if (pd is not null)
            {
                m.CreateEffect(pd, made, new FVector(0, 0, 0), false, p.FoR is not null);
            }
        }

        var link = (ParticleEffectLink)p.Data!;
        if (p.FoR is { } f)
        {
            link.SetPos(f.Orientation.Transform(p.Pos) + f.Pos);
            link.SetOrientation(f.Orientation.Multiply(p.Orientation));
        }
        else
        {
            link.SetPos(p.Pos);
            link.SetOrientation(p.Orientation);
        }

        if (p.FoR is { } owner && link.Full)
        {
            link.Effect!.UnknownAC = owner.UnknownAC;
        }

        if (!link.Full)
        {
            if (Death is not null)
            {
                ParticleDescriptor? pd = Resolve(m, Death);
                if (pd is not null)
                {
                    Particle np = AllocateFrom(m, pd, p)
                        ?? throw new NotSupportedException("CPDFoR::Unknown10: the death effect's particle was refused (retail writes through null)");
                    np.Pos = p.Pos;
                    np.Orientation = p.Orientation;
                    np.FoR = p.FoR;
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>Unknown14 (004c5700): the particle's link stops its effect and goes; nothing else to free.</summary>
    public override bool Unknown14(Particle p)
    {
        if (p.Data is ParticleEffectLink link)
        {
            link.Stop(true);
            link.Release();
        }

        return false;
    }
}

public sealed partial class PdPMesh
{
    /// <summary>Unknown9 (004c5b40).</summary>
    public override void Unknown9(ParticleManager m, Particle p)
    {
        p.Life = Life;
        p.Radius = 1.0f;
    }

    public override bool Unknown10(ParticleManager m, Particle p) => false;
}
