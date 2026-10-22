// SPDX-License-Identifier: GPL-3.0-only
// A particle, a frame of reference and a thing's link to an effect. Adapted from bea-decomp src/ParticleManager.h
// (CParticle, CFoR, CParticleEffectLink) and ParticleManager.cpp.

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>CParticle (0xd8 bytes): one of the manager's particles.</summary>
public sealed class Particle
{
    /// <summary>The descriptor's list (mPDPrev, mPDNext).</summary>
    public Particle? PDPrev, PDNext;

    public FMatrix Orientation = FMatrix.Identity;
    public FVector Pos;
    public FVector Velocity;
    public FoR? FoR;
    public ParticleDescriptor? PD;

    /// <summary>mLife: 100 when allocated; -1 lives for ever; -2 waits.</summary>
    public float Life;
    public bool Dead;

    /// <summary>The manager's list (mNext, mPrev).</summary>
    public Particle? Next, Prev;

    public float Radius;
    public float Frame;
    public ushort Unknown7C;
    public short Unknown7E;
    public int Unknown80;
    public float BirthTime;

    /// <summary>mData: a trail's points or a frame-of-reference particle's link.</summary>
    public object? Data;

    public float U0, V0, U1, V1;

    public bool IsDying => Life > -2.0f && Life < 0.0f;
}

/// <summary>A trail's point (STrailPoint, 0x28 bytes).</summary>
public sealed class TrailPoint
{
    public FVector Pos;
    public FVector Unknown10;
    public float Width;
    public int Unknown24;
}

/// <summary>
/// CFoR (0xb8 bytes): what an effect's link points to; the effect's particles move with it. The manager keeps them on a
/// list until no particle refers to one.
/// </summary>
public sealed class FoR
{
    public FVector Pos;
    public FMatrix Orientation = FMatrix.Identity;

    /// <summary>Z 10000 until first placed.</summary>
    public FVector OldPos = new(0, 0, 10000.0f);
    public FMatrix OldOrientation;

    /// <summary>mStartPos: where it started; DoInterpolation sets it to the drawn position.</summary>
    public FVector StartPos;
    public FVector Unknown90;
    public bool HasOrientation;
    public bool InUse;
    public Particle? Particle;

    /// <summary>-1 for none.</summary>
    public float UnknownAC = -1.0f;
    public FoR? Next;

    /// <summary>1 stopping, 2 stopped, 3 gone.</summary>
    public ushort State;
    public ushort Flags;

    /// <summary>SetPos: the first move also sets where it started.</summary>
    public void SetPos(FVector pos, float time)
    {
        if (OldPos.Z == 10000.0f)
        {
            OldPos = StartPos = pos;
        }
        else
        {
            OldPos = Pos;
        }

        SetCurrentPos(pos, time);
    }

    public void SetCurrentPos(FVector pos, float time)
    {
        Pos = pos;
        if (UnknownAC != -1.0f)
        {
            UnknownAC = time;
        }
    }

    public void Stop(bool now) => State = now ? (ushort)1 : (ushort)2;
}

/// <summary>
/// CParticleEffectLink (CValidatedFoR): a thing's link to an effect, on the manager's list of links while it exists.
/// <see cref="Release"/> is its destructor.
/// </summary>
public sealed class ParticleEffectLink
{
    private readonly ParticleManager _manager;

    public ParticleEffectLink(ParticleManager manager)
    {
        _manager = manager;
        _manager.LinkEffectLink(this);
    }

    public ParticleEffectLink? Next;
    public FoR? Effect;

    public bool Full => Effect is not null;

    /// <summary>The destructor: off the manager's list.</summary>
    public void Release() => _manager.UnlinkEffectLink(this);

    /// <summary>Stop (004cb0b0).</summary>
    public void Stop(bool now)
    {
        Effect?.Stop(now);
        Effect = null;
    }

    public void Kill() => Stop(false);

    public void Reset() => Effect = null;

    public void SetPos(FVector pos) => Effect?.SetPos(pos, _manager.EventTime());

    public void SetOrientation(FMatrix ori)
    {
        if (Effect is { } f)
        {
            f.Orientation = ori;
            f.HasOrientation = true;
        }
    }
}
