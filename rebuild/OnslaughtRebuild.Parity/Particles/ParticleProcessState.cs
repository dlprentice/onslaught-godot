// SPDX-License-Identifier: GPL-3.0-only
// Process-lifetime particle state from onslaught-decomp ParticleManager.cpp, ParticleDescriptor.cpp and PDSimpleSprite.cpp.
using OnslaughtRebuild.Parity.Crt;

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>The retail globals that outlive a particle set, owned by one simulated game process, not the CLR process.</summary>
public sealed class ParticleProcessState
{
    public int Frame;
    public int Allocations;
    public int SpriteCount;
    public float[]? Noise;

    /// <summary>GAME.mFraction, still read by DoInterpolation while the front end runs after a level.</summary>
    public float RenderFraction;

    /// <summary>RenderOther's deterministic side effects, after mesh drawing. No draw is made when BeginScene failed.</summary>
    public void RenderOther(ParticleManager manager)
    {
        for (ParticleDescriptor? pd = manager.Set.FirstNode; pd is not null; pd = pd.NodeNext)
        {
            if (pd.Type == PdType.SimpleSprite)
            {
                InitNoiseTable(manager.Rand);
            }
            else if (pd.Type == PdType.PMesh)
            {
                for (Particle? p = pd.OldestParticle; p is not null; p = p.PDNext)
                {
                    if (!(p.Life < 0.0f) || p.Life == -2.0f) InitNoiseTable(manager.Rand);
                }
            }
        }
    }

    /// <summary>InitNoiseTable (004c7db0): diamond-square midpoints with random offsets, then normalised; once.</summary>
    public void InitNoiseTable(CrtRandStream rand)
    {
        if (Noise is not null)
        {
            return;
        }

        var noise = new float[32 * 32];
        ref float N(int x, int y) => ref noise[((y & 31) << 5) + (x & 31)];
        float Offset(int scale) => (((rand.Rand() / 3) - 0x1000) * scale >> 12) / 4096.0f;
        for (int step = 32; step > 1; step >>= 1)
        {
            for (int y = 0; y < 32; y += step)
            {
                for (int x = 0; x < 32; x += step)
                {
                    int half = step / 2;
                    int scale = half * 80;
                    N(x, y + half) = ((N(x, y + step) + N(x, y)) * 0.5f) + Offset(scale);
                    N(x + half, y + half) = ((N(x + step, y + step) + N(x + step, y) + N(x, y + step) + N(x, y)) * 0.25f)
                        + Offset(scale);
                    N(x + half, y) = ((N(x + step, y) + N(x, y)) * 0.5f) + Offset(scale);
                }
            }
        }

        float max = 0;
        foreach (float v in noise)
        {
            max = Math.Max(max, Math.Abs(v));
        }

        if (max > 0)
        {
            float s = 1.0f / max;
            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] *= s;
            }
        }

        Noise = noise;
    }
}
