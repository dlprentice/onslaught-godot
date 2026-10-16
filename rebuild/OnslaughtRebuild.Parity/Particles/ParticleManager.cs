// SPDX-License-Identifier: GPL-3.0-or-later
// The particle manager. Adapted from bea-decomp src/ParticleManager.cpp (CleanupHandles 004caf60, Process 004cb210,
// DoInterpolation 004cb300, CreateEffect 004cb3d0, AllocateParticle 004cb5c0, ProcessParticle 004cb920, OnGround
// 004cba30, GetDistanceSq 004cba90, MoveParticles 004cbca0, FreeDeadParticles 004cbe30).
//
// The particles draw from the main thread's C runtime rand(), which the simulation shares (segment timings, animals,
// debris), so the particle system is part of the deterministic core; the host only draws what it produces.

using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>What the particles read from the game: the clock, the camera, the map and the meshes.</summary>
public interface IParticleWorld
{
    /// <summary>EVENT_MANAGER.GetTime().</summary>
    float EventTime { get; }

    /// <summary>ENGINE.GetCamera(): the camera being rendered, or none.</summary>
    (FVector Pos, FMatrix Orientation)? Camera { get; }

    /// <summary>GAME.GetFrameRenderFraction().</summary>
    float FrameRenderFraction { get; }

    /// <summary>MAP.Collide(pos): the ground's height there.</summary>
    float MapCollide(FVector pos);

    /// <summary>The mesh of that name's radius (CMesh::GetMesh(name, TRUE)->Radius()).</summary>
    float MeshRadius(string name);
}

/// <summary>CParticleManager (PARTICLE_MANAGER): the live particles, their frames of reference and the links to them.</summary>
public sealed class ParticleManager
{
    private const int ParticlesPerSystem = 512;
    private const int MaxSystems = 8;

    public ParticleManager(ParticleSet set, CrtRandStream rand, IParticleWorld world)
    {
        Set = set;
        Rand = rand;
        World = world;
    }

    public ParticleSet Set { get; }

    /// <summary>The main thread's rand().</summary>
    public CrtRandStream Rand { get; }

    public IParticleWorld World { get; }

    /// <summary>mParticles: the live list, newest first.</summary>
    public Particle? Particles;

    public int Frame;

    /// <summary>mFrameLength: this frame's, 0..1.</summary>
    public float FrameLength;

    public bool FirstFrame;

    /// <summary>mTime.</summary>
    public float Time;

    public int NumLiveParticles;

    /// <summary>The billboard axes RenderOther takes from the camera.</summary>
    public FVector Right, Up;

    // The free list (LIFO) and the systems allocated (blocks of 512).
    private readonly Stack<Particle> _free = new();
    private int _numSystems;
    private int _numAllocated;
    private int _allocations;
    private int _load;

    // sFoRs and sLinks, newest first.
    private FoR? _fors;
    private ParticleEffectLink? _links;

    internal float EventTime() => World.EventTime;

    internal void LinkEffectLink(ParticleEffectLink link)
    {
        link.Next = _links;
        _links = link;
    }

    internal void UnlinkEffectLink(ParticleEffectLink link)
    {
        if (_links == link)
        {
            _links = link.Next;
            return;
        }

        for (ParticleEffectLink? l = _links; l is not null; l = l.Next)
        {
            if (l.Next == link)
            {
                l.Next = link.Next;
                return;
            }
        }
    }

    /// <summary>CParticle::Destroy (004cae50): its data freed (when the descriptor allows) and off its descriptor's list.</summary>
    private void Destroy(Particle p)
    {
        if (p.Data is not null && (p.PD is null || p.PD.Unknown14(p)))
        {
            p.Data = null;
        }

        if (p.PD is { } pd)
        {
            if (p.PDPrev is not null && p.PDNext is not null)
            {
                p.PDPrev.PDNext = p.PDNext;
                p.PDNext.PDPrev = p.PDPrev;
                p.PDPrev = null;
                p.PDNext = null;
                if (pd.NewestAtUpdate == p)
                {
                    pd.NewestAtUpdate = null;
                }
            }
            else
            {
                pd.RemoveParticle(Set, p);
            }
        }
    }

    private void UnlinkParticle(Particle p)
    {
        if (p.Prev is null)
        {
            Particles = p.Next;
        }
        else
        {
            p.Prev.Next = p.Next;
        }

        if (p.Next is not null)
        {
            p.Next.Prev = p.Prev;
        }
    }

    private void FreeParticle(Particle p)
    {
        UnlinkParticle(p);
        Destroy(p);
        _free.Push(p);
    }

    private void ClearOwnerBacklinks()
    {
        for (FoR? f = _fors; f is not null; f = f.Next)
        {
            f.InUse = false;
            f.Particle = null;
        }
    }

    /// <summary>CleanupHandles (004caf60): each frame's stop state advances; frames no particle uses are dropped.</summary>
    private void CleanupHandles()
    {
        FoR? f;
        while ((f = _fors) is not null)
        {
            Advance(f);
            if (f.InUse)
            {
                break;
            }

            _fors = f.Next;
        }

        if (f is null)
        {
            return;
        }

        while (f.Next is { } n)
        {
            Advance(n);
            if (!n.InUse)
            {
                f.Next = n.Next;
            }
            else
            {
                f = n;
            }
        }

        static void Advance(FoR f)
        {
            if (f.State == 1)
            {
                f.State = 2;
            }
            else if (f.State == 2)
            {
                f.State = 3;
            }
        }
    }

    private void PruneDeadOwnerLinks()
    {
        for (ParticleEffectLink? l = _links; l is not null; l = l.Next)
        {
            if (l.Effect is { InUse: false })
            {
                l.Effect = null;
            }
        }
    }

    /// <summary>Process (004cb210): one render's step; TRUE while particles live.</summary>
    public bool Process(float time, bool first)
    {
        Frame++;
        _numAllocated = 0;
        time = time > 1.0f ? 1.0f : time < 0.0f ? 0.0f : time;
        Time += time;
        FirstFrame = first;
        FrameLength = time;
        ClearOwnerBacklinks();
        MoveParticles(Particles);
        for (ParticleDescriptor? pd = Set.FirstNode; pd is not null; pd = pd.NodeNext)
        {
            pd.NextToUpdate = pd.NodeNext;
            pd.NewestAtUpdate = pd.NewestParticle;
        }

        for (ParticleDescriptor? pd = Set.FirstNode; pd is not null;)
        {
            pd.Unknown22(this);
            ParticleDescriptor? next = pd.NextToUpdate;
            pd.NewestAtUpdate = null;
            pd.NextToUpdate = null;
            pd = next;
        }

        bool alive = FreeDeadParticles();
        PruneDeadOwnerLinks();
        CleanupHandles();
        return alive;
    }

    /// <summary>DoInterpolation (004cb300): each linked effect's drawn position, between its last two.</summary>
    public void DoInterpolation()
    {
        float fraction = World.FrameRenderFraction;
        for (ParticleEffectLink? l = _links; l is not null; l = l.Next)
        {
            if (l.Effect is { } f)
            {
                f.StartPos = f.OldPos.Z != 10000.0f ? f.OldPos + ((f.Pos - f.OldPos) * fraction) : f.OldPos;
            }
        }
    }

    /// <summary>CreateEffect (004cb3d0): a particle of the descriptor at pos; with a link, in a new frame of reference.</summary>
    public void CreateEffect(ParticleDescriptor? pd, ParticleEffectLink? link, FVector pos, bool a, bool important)
    {
        Particle? p = AllocateParticle(pd, important);
        if (p is not null)
        {
            p.Pos = pos;
            if (link is not null)
            {
                var f = new FoR { InUse = true, Particle = p, UnknownAC = a ? 0.0f : -1.0f };
                p.FoR = f;
                link.Effect = f;
                f.Next = _fors;
                _fors = f;
                if (important)
                {
                    f.Flags |= 1;
                }
            }
            else
            {
                p.PD!.Unknown15(this, p);
            }
        }
        else if (link is not null)
        {
            link.Effect = null;
        }
    }

    /// <summary>AddParticle: CreateEffect at the origin.</summary>
    public void AddParticle(ParticleDescriptor? pd, ParticleEffectLink? link, bool important = false) =>
        CreateEffect(pd, link, new FVector(0, 0, 0), false, important);

    /// <summary>
    /// AllocateParticle (004cb5c0): a fresh particle of the descriptor, or none. Up to eight blocks of 512; when they are
    /// full an important particle reclaims the newest unlinked sprite. Past 40% load (60%, 80% by kind) unimportant ones
    /// are thinned, past 70% (80%, 90%) refused.
    /// </summary>
    public Particle? AllocateParticle(ParticleDescriptor? pd, bool important)
    {
        if (pd is null)
        {
            return null;
        }

        bool rejected = false;
        if (_free.Count == 0)
        {
            if (_numSystems < MaxSystems)
            {
                for (int i = 0; i < ParticlesPerSystem; i++)
                {
                    _free.Push(new Particle());
                }

                _numSystems++;
            }
            else if (important)
            {
                Particle? q = Particles;
                if (q is null)
                {
                    rejected = true;
                }
                else
                {
                    for (; q is not null; q = q.Next)
                    {
                        if (q.PD is { Type: PdType.SimpleSprite } && q.FoR is null)
                        {
                            FreeParticle(q);
                            break;
                        }
                    }

                    if (q is null)
                    {
                        return null;
                    }
                }
            }
            else
            {
                rejected = true;
            }
        }

        if (!rejected)
        {
            _load = (NumLiveParticles + _numAllocated) * 1000 / (MaxSystems * ParticlesPerSystem);
            _allocations++;
            if (!important)
            {
                switch (pd.Type)
                {
                    case PdType.SimpleSprite or PdType.Mesh:
                        if (_load > 700)
                        {
                            rejected = true;
                        }
                        else if (_load > 400 && _allocations % 300 < _load - 400)
                        {
                            return null;
                        }

                        break;
                    case PdType.Trail or PdType.PMesh:
                        if (_load > 800)
                        {
                            rejected = true;
                        }
                        else if (_load > 600 && _allocations % 200 < _load - 600)
                        {
                            return null;
                        }

                        break;
                    case PdType.Emitter:
                        if (_load > 900 || (_load > 800 && _allocations % 100 < _load - 800))
                        {
                            rejected = true;
                        }

                        break;
                }
            }
        }

        if (rejected)
        {
            return null;
        }

        Particle p = _free.Pop();
        p.Pos = new FVector(0, 0, 0);
        p.Velocity = new FVector(0, 0, 0);
        p.Orientation = FMatrix.Identity;
        p.FoR = null;
        p.Unknown7E = 0;
        p.Unknown80 = 0;
        p.Life = 100.0f;
        p.Radius = 1.0f;
        p.Frame = 0;
        p.Unknown7C = 0;
        p.BirthTime = 0;
        p.PD = null;
        p.Data = null;
        p.Dead = false;
        p.PDNext = null;
        p.PDPrev = null;
        p.Next = Particles;
        Particles = p;
        if (p.Next is not null)
        {
            p.Next.Prev = p;
        }

        p.Prev = null;
        p.PD = pd;
        pd.AddParticle(this, p);
        p.BirthTime = Time;
        pd.Unknown9(this, p);
        _numAllocated++;
        return p;
    }

    /// <summary>ProcessParticle (004cb920): a new particle's first step, then its descriptor's; freed once dead.</summary>
    public void ProcessParticle(Particle p)
    {
        if (p.Life > -1.0f)
        {
            p.Life -= FrameLength;
        }

        p.Pos += p.Velocity * FrameLength;
        if (p.FoR is { } f)
        {
            f.InUse = true;
            f.Particle = p;
        }

        p.Dead |= FirstFrame && p.Life > -2.0f && p.Life < 0.0f;
        p.PD!.Unknown10(this, p);
        if (p.Dead)
        {
            FreeParticle(p);
        }
    }

    /// <summary>OnGround (004cba30): where a particle of that radius rests on the ground, once it has reached it.</summary>
    public bool OnGround(FVector pos, float radius, out FVector output)
    {
        float h = World.MapCollide(pos);
        if (pos.Z + radius > h)
        {
            output = pos;
            output.Z = h - radius;
            return true;
        }

        output = default;
        return false;
    }

    /// <summary>GetDistanceSq (004cba90): to the camera (single player), 0 with none.</summary>
    public float GetDistanceSq(Particle p)
    {
        if (World.Camera is not { } cam)
        {
            return 0.0f;
        }

        FVector pos = p.Pos;
        if (p.FoR is { } f)
        {
            pos += f.Pos;
        }

        return (cam.Pos - pos).MagnitudeSq();
    }

    /// <summary>MoveParticles (004cbca0): every particle's life and position step; on the first frame the dying die.</summary>
    private void MoveParticles(Particle? list)
    {
        float t = FrameLength;
        for (Particle? q = list; q is not null; q = q.Next)
        {
            if (q.FoR is { } f)
            {
                if (f.State == 2)
                {
                    q.PD!.Unknown21(this, q);
                }

                f.InUse = true;
                f.Particle = q;
            }

            if (q.Life > -1.0f)
            {
                q.Life -= t;
            }

            q.Pos += q.Velocity * t;
            if (FirstFrame)
            {
                q.Dead |= q.Life > -2.0f && q.Life < 0.0f;
            }
        }
    }

    /// <summary>FreeDeadParticles (004cbe30): the dead freed, the rest counted.</summary>
    private bool FreeDeadParticles()
    {
        NumLiveParticles = 0;
        bool alive = false;
        Particle? p = Particles;
        while (p is not null)
        {
            if (p.Dead)
            {
                Particle dead = p;
                p = p.Next;
                FreeParticle(dead);
            }
            else
            {
                p = p.Next;
                NumLiveParticles++;
                alive = true;
            }
        }

        return alive;
    }

    /// <summary>The live particles, newest first (for drawing and checks).</summary>
    public IEnumerable<Particle> LiveParticles()
    {
        for (Particle? p = Particles; p is not null; p = p.Next)
        {
            yield return p;
        }
    }
}
