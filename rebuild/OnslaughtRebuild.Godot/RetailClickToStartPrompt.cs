// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The CFEPIntro click-to-start clock — <c>this+0x18</c> — recovered from the
/// pristine specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para><b>Bodies.</b> <c>CFEPIntro::Process</c> <c>0x0051B6B0</c>–<c>0x0051B83C</c>
/// owns the timer. <c>CFEPIntro::Render</c> <c>0x0051B840</c>–<c>0x0051BE66</c>
/// spends it on the splash pulse and the "Click to start" prompt. Neither body
/// is in the pinned GPL drop (<c>FEPIntro.cpp</c> is absent).</para>
///
/// <para><b>Process, in order.</b></para>
/// <list type="number">
/// <item>If the timer is 0 and <c>GetTime() - [this+4]</c> is strictly greater
/// than <c>1.0f</c> at <c>0x005D8568</c>, seed <c>[this+18]</c> with the
/// immediate <c>0x3727C5AC</c> (<c>0x0051B776</c>).</item>
/// <item>If the timer is then nonzero, add twice the frame Δ
/// (<c>fld [dt]; fadd st,st; fadd [esi+18]</c> at <c>0x0051B78D</c>).</item>
/// </list>
///
/// <para><b>Render.</b> The splash argument is <c>min(timer, 1.0f)</c>:
/// <c>fld [this+0x18]</c> at <c>0x0051B866</c>, then <c>fcom [1.0f]</c> at
/// <c>0x005D8568</c> / <c>test ah,0x41</c> / <c>jnz keep</c>; only when the
/// timer is strictly greater than 1 does Render <c>fstp</c> and reload
/// <c>1.0f</c>. <c>0x005D856C</c> is the adjacent <c>0.0f</c> used by Process,
/// not this compare. The prompt is drawn only when the timer is strictly
/// greater than <c>4.0f</c> at <c>0x005D85BC</c> and <c>fmod(timer, 4.0)</c>
/// is strictly less than <c>2.0f</c> at <c>0x005D8BA0</c>. The modulus is the
/// qword <c>4.0</c> at <c>0x005DB4A0</c>, consumed by the CRT <c>fmod</c>
/// thunk at <c>0x0055E3EA</c>.</para>
///
/// <para><b>Idle result, predicate only.</b> After page-elapsed time is
/// strictly greater than <c>30.0f</c> at <c>0x005DB1E4</c>, Process writes
/// <c>-3</c> to the frontend quit/result global <c>0x008A956C</c>
/// (<c>fsub [this+4]; fcomp [30.0f]; test ah,0x41 / jnz skip</c> then
/// <c>mov [0x008A956C], -3</c> at <c>0x0051B731</c>). The consumer is
/// <see cref="RetailAttractLoop"/> — not a CFEPIntro fade.</para>
///
/// <para>No Godot types. The capture rig suppresses this page's timing the
/// same way retail's <c>-skipfmv</c> does, so these numbers have to stand on
/// their own.</para>
/// </summary>
public static class RetailClickToStartPrompt
{
    /// <summary>
    /// <c>0x005D8568</c> = <c>1.0f</c>. Process will not seed <c>this+0x18</c>
    /// until page-elapsed time is strictly greater than this.
    /// </summary>
    public const double SeedDelaySeconds = 1.0;

    /// <summary>The 32-bit immediate Process writes at <c>0x0051B776</c>.</summary>
    public const uint SeedBits = 0x3727C5ACu;

    /// <summary><see cref="SeedBits"/> decoded as IEEE-754 single, widened.</summary>
    public static double SeedValue { get; } = BitConverter.UInt32BitsToSingle(SeedBits);

    /// <summary>
    /// Process adds <c>2 * dt</c> once the timer is nonzero
    /// (<c>fadd st,st</c> at <c>0x0051B793</c>).
    /// </summary>
    public const double Rate = 2.0;

    /// <summary><c>0x005D85BC</c> = <c>4.0f</c>. Prompt hidden while timer ≤ this.</summary>
    public const double PromptGateSeconds = 4.0;

    /// <summary>
    /// Qword at <c>0x005DB4A0</c> = <c>4.0</c>. The CRT <c>fmod</c> period.
    /// </summary>
    public const double BlinkPeriodSeconds = 4.0;

    /// <summary>
    /// <c>0x005D8BA0</c> = <c>2.0f</c>. Prompt on while
    /// <c>fmod(timer, period)</c> is strictly less than this.
    /// </summary>
    public const double BlinkOnSeconds = 2.0;

    /// <summary>
    /// <c>0x005DB1E4</c> = <c>30.0f</c>. Process writes
    /// <see cref="IdleResult"/> once <c>GetTime()-[this+4]</c> is strictly
    /// greater than this. Independent of the 2*dt pulse timer.
    /// </summary>
    public const double IdleTimeoutSeconds = 30.0;

    /// <summary>
    /// Immediate written to <c>0x008A956C</c> at <c>0x0051B731</c>.
    /// </summary>
    public const int IdleResult = -3;

    /// <summary>
    /// One Process tick of <c>this+0x18</c>.
    /// <paramref name="pageSeconds"/> is wall time since the page became
    /// active, matching <c>GetTime() - [this+4]</c>.
    /// </summary>
    public static double Advance(double timer, double pageSeconds, double dt)
    {
        if (timer == 0d && pageSeconds > SeedDelaySeconds)
        {
            timer = SeedValue;
        }

        if (timer != 0d)
        {
            timer += Rate * dt;
        }

        return timer;
    }

    /// <summary>Whether Render would submit the localization-0x77 prompt.</summary>
    public static bool IsPromptVisible(double timer)
    {
        if (timer <= PromptGateSeconds)
        {
            return false;
        }

        // CRT fmod is toward-zero remainder. C# double % is the same for these
        // signs. The compare is strictly less than 2.0 (test ah,1 / jz).
        double remainder = timer % BlinkPeriodSeconds;
        if (remainder < 0d)
        {
            remainder += BlinkPeriodSeconds;
        }

        return remainder < BlinkOnSeconds;
    }

    /// <summary>
    /// Whether Process would write <see cref="IdleResult"/> this tick.
    /// Compare is page elapsed, not <see cref="Advance"/>.
    /// </summary>
    public static bool ShouldWriteIdleResult(double pageSeconds) =>
        pageSeconds > IdleTimeoutSeconds;

    /// <summary>
    /// Splash-pulse argument: the live timer while it is ≤ 1.0, otherwise 1.0.
    /// <c>0x0051B869</c> <c>fcom [1.0f]</c> / <c>test ah,0x41</c> / <c>jnz keep</c>.
    /// </summary>
    public static float SplashArgument(double timer) => timer <= 1d ? (float)timer : 1f;

    /// <summary>
    /// <c>((cos(t*π)+1)*0.375)+0.46875</c> with <see cref="SplashArgument"/>.
    /// The 0.375 is <c>0.5 * 0.75</c> from the two packed multiplies at
    /// <c>0x0051B894</c> / <c>0x0051B89A</c>; 0.46875 is <c>0x005E49F0</c>.
    /// </summary>
    public static float SplashScale(double timer)
    {
        float t = SplashArgument(timer);
        return ((MathF.Cos(t * MathF.PI) + 1f) * 0.375f) + 0.46875f;
    }
}
