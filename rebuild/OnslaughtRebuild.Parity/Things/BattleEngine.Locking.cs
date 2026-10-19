// SPDX-License-Identifier: GPL-3.0-only
// Adapted from matched bea-decomp BattleEngine.cpp CanBeLocked0040e7d0 and
// BattleEngineWalkerPart.cpp GetIsDoingSpecialWalkerMove004135d0; pristine PC SHA-256
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
namespace OnslaughtRebuild.Parity.Things;

public sealed partial class BattleEngine
{
    /// <summary>Slot104: stealth, a jet loop/positive roll count, or a positive walker dash count breaks lock.</summary>
    public bool CanBeLocked()
    {
        if (Stealth > 0) return false;
        // DoingBarrelCount > 0 is deliberately not IsDoingSpecialAirMove's != 0 predicate.
        if (State == BattleEngineState.Jet && (JetPart!.DoingLoop || JetPart.DoingBarrelCount > 0)) return false;
        if (State == BattleEngineState.Walker && WalkerPart!.DoingDashCount > 0) return false;
        return true;
    }
}
