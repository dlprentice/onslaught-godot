// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CLTShell attract-restart consumer of the CFEPIntro idle
/// <c>-3</c>. Specimen <c>74154bfa…</c>, body
/// <c>CLTShell::RunFrontEndAndGameLoop</c> <c>0x004F0330</c>–<c>0x004F0535</c>.
///
/// <para>The mutation these cases kill is treating idle as a CFEPIntro fade
/// or replaying the cold-start splash hold on attract re-entry.</para>
/// </summary>
public sealed class RetailAttractLoopTests
{
    [Fact]
    public void MinusThreeRestartsAttractAndMinusOneExitsTheProcess()
    {
        // 0x004F03ED cmp ebp,-1 / je; 0x004F03F2 cmp ebp,-3 / jne RunLevel.
        Assert.Equal(-3, RetailAttractLoop.AttractRestartResult);
        Assert.Equal(-3, RetailClickToStartPrompt.IdleResult);
        Assert.Equal(-1, RetailAttractLoop.ProcessExitResult);
        Assert.Equal(-2, RetailAttractLoop.StayInFrontend);
        Assert.Equal(
            RetailAttractLoop.ShellAction.AttractRestart,
            RetailAttractLoop.AfterFrontEndRun(-3));
        Assert.Equal(
            RetailAttractLoop.ShellAction.ProcessExit,
            RetailAttractLoop.AfterFrontEndRun(-1));
        Assert.Equal(
            RetailAttractLoop.ShellAction.RunLevel,
            RetailAttractLoop.AfterFrontEndRun(100));
        Assert.Equal(
            RetailAttractLoop.ShellAction.RunLevel,
            RetailAttractLoop.AfterFrontEndRun(RetailAttractLoop.StayInFrontend));
    }

    [Fact]
    public void AttractReentryIsFeeFromAttractThenFepIntro()
    {
        // 0x004F038F sete al after cmp ebp,-3 → esi = FEE_FROM_ATTRACT = 1.
        // FrontEnd.cpp:188-202 SetPage(FEP_INTRO, 0). FEE_TITLE_SCREEN is FEP_MAIN.
        Assert.Equal(0, RetailAttractLoop.StartEntry);
        Assert.Equal(1, RetailAttractLoop.FromAttractEntry);
        Assert.Equal(2, RetailAttractLoop.TitleScreenEntry);
        Assert.True(RetailAttractLoop.ReentersIntro(RetailAttractLoop.FromAttractEntry));
        Assert.True(RetailAttractLoop.ReentersIntro(RetailAttractLoop.StartEntry));
        Assert.False(RetailAttractLoop.ReentersIntro(RetailAttractLoop.TitleScreenEntry));
    }

    [Fact]
    public void AttractCuesAreLtlogoThenOpeningfmvWithNoSplashBeat()
    {
        Assert.Equal(
            new[]
            {
                RetailStartupCue.LostToysLogo,
                RetailStartupCue.OpeningMontage,
            },
            RetailAttractLoop.AttractCues);
        Assert.DoesNotContain(RetailStartupCue.Splash, RetailAttractLoop.AttractCues);
    }

    [Fact]
    public void AttractScheduleIsLogoThenMontageThenFinishedWithNoSplash()
    {
        var logo = new RetailStartupClip(229, 25, 1, 480, 300);
        var montage = new RetailStartupClip(2054, 25, 1, 480, 300);
        RetailStartupSchedule attract = RetailStartupSchedule.ForAttractRestart(
            new Dictionary<RetailStartupCue, RetailStartupClip>
            {
                [RetailStartupCue.LostToysLogo] = logo,
                [RetailStartupCue.OpeningMontage] = montage,
            });

        Assert.Equal(RetailStartupCue.LostToysLogo, attract.Sample(0.0).Cue);
        Assert.Equal(RetailStartupCue.OpeningMontage, attract.Sample(logo.DurationSeconds).Cue);
        Assert.Equal(
            RetailStartupFrameKind.Finished,
            attract.Sample(logo.DurationSeconds + montage.DurationSeconds).Kind);
        Assert.DoesNotContain(RetailStartupCue.Splash, attract.MissingCues);
        Assert.Equal(
            logo.DurationSeconds + montage.DurationSeconds,
            attract.TotalSeconds,
            6);
        Assert.NotEqual(
            new RetailStartupSchedule(
                new Dictionary<RetailStartupCue, RetailStartupClip>
                {
                    [RetailStartupCue.LostToysLogo] = logo,
                    [RetailStartupCue.OpeningMontage] = montage,
                },
                splashPresent: true).TotalSeconds,
            attract.TotalSeconds);
    }

    [Fact]
    public void IdlePageElapsedIsTheOnlyAttractTrigger()
    {
        Assert.False(RetailAttractLoop.ShouldRestartAttract(30.0d));
        Assert.True(RetailAttractLoop.ShouldRestartAttract(30.0d + 1e-6));
        Assert.False(RetailAttractLoop.ShouldRestartAttract(0d));
    }

    [Fact]
    public void FirstFlightGameWiresAttractReentryWithoutAFadeOrTheHotspot()
    {
        string game = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "FirstFlightGame.cs"));
        string sequence = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailStartupSequence.cs"));
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        Assert.Contains("RetailAttractLoop.ShouldRestartAttract", game);
        Assert.Contains("InitializeForAttract", game);
        Assert.Contains("InitializeForAttract", sequence);
        Assert.DoesNotContain("AttractFade", game);
        Assert.DoesNotContain("RetailAttractLoop", flow);
    }
}
