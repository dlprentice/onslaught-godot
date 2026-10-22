// SPDX-License-Identifier: GPL-3.0-only
// Matched Plane.cpp Init004d19d0 and Move004d1cd0. Launch effects precede CAirUnit::Move.
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class Plane
{
    internal void InitLaunchThrusters()
    {
        // Retail tests the resolved descriptor pointer, not the presence of an unresolved effect name.
        if (Context.Particles is not { } particles || Context.FindParticle(UnitData!.EffectNames[8]) is null) return;
        int count = RenderThing!.CountEmitters("Engine");
        for (int n = 0; n < count; n++) Thrusters.Add(new ParticleEffectLink(particles.Manager));
    }

    internal void UpdateLaunchThrusters()
    {
        if (LaunchTime > Context.Events.Time)
        {
            if (Context.Particles is { } particles)
            {
                int n = 1;
                foreach (ParticleEffectLink link in Thrusters)
                {
                    if (link.Effect is null)
                        particles.Manager.CreateEffect(Context.FindParticle(UnitData!.EffectNames[8]), link,
                            new FVector(0, 0, 0), false, false);
                    GetEmitter(23, n, out FVector pos, out _);
                    link.SetPos(pos);
                    // Native deliberately uses the aircraft orientation, not the emitter orientation.
                    link.SetOrientation(Orientation);
                    n++;
                }
            }
            ThrustersActive = true;
        }
        else if (ThrustersActive)
        {
            foreach (ParticleEffectLink link in Thrusters) link.Kill();
            ThrustersActive = false;
        }
    }
}
