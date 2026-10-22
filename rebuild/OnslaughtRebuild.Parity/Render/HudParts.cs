// SPDX-License-Identifier: GPL-3.0-only
// CHud's part states (onslaught-decomp src/Hud.h, mPartState and mFlash; CHud::Reset 004815c0, CHud::Render 00487bc0,
// IsComponentVisible 004881e0) and the script's HighlightHudPart 00535e60 / UnHighlightHudPart 00535e80.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Render;

/// <summary>
/// The HUD's parts, each off, on or flashing (mPartState: HCS_OFF 0, HCS_ON 1, HCS_FLASHING 2), all on at the level's start
/// (CHud::Reset), and the flash a flashing part follows. The mission script highlights a part (flashing) and unhighlights it
/// (on); CHud::Render sets the flash from the clock and the battle line's map from the message box each render.
/// </summary>
public sealed class HudParts
{
    public const int HealthBar = 0;
    public const int EnergyBar = 1;
    public const int Compass = 2;
    public const int BattleLineMap = 3;
    public const int Radar = 4;
    public const int CurrentWeapon = 5;

    public const int Off = 0;
    public const int On = 1;
    public const int Flashing = 2;

    /// <summary>mPartState, by part.</summary>
    public int[] State { get; } = [On, On, On, On, On, On];

    /// <summary>mFlash: on for the second and fourth quarter of each second of the clock.</summary>
    public bool Flash { get; private set; }

    /// <summary>
    /// CHud::Render's start: <c>mFlash = int((t - floorf(t)) * 4) &amp; 1</c> (the conversion rounds, as /QIfist's fistp does),
    /// then the battle line's map, unless flashing, is on exactly while no message is current.
    /// </summary>
    public void Update(float time, bool messageCurrent)
    {
        Flash = (X87.Fist((time - MathF.Floor(time)) * 4.0f) & 1) != 0;
        if (State[BattleLineMap] != Flashing)
        {
            State[BattleLineMap] = messageCurrent ? Off : On;
        }
    }

    /// <summary>IsComponentVisible (004881e0): off never, on always, flashing with the flash.</summary>
    public bool IsComponentVisible(int part) => State[part] switch
    {
        On => true,
        Flashing => Flash,
        _ => false,
    };
}
