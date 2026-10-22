// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The options page's menu render (CPauseMenu::Render, CMenuOptionsList::Render and the items'): drawing it leaves every
/// Present of the options route as it was, and the cursor's hover and click change the menu as retail's render does
/// (MenuOptionsList.cpp 004a4810: a row under the moved cursor becomes the selection; a click there presses select).
/// </summary>
public sealed class OptionsMenuRenderTests(ITestOutputHelper output)
{
    private const string Tape = "portrefs/fe-r4-options-port/feopt.tape";

    private sealed class StopRun : Exception;

    private static GameRun NewRun() =>
        new(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(Tape)!),
        };

    private static void RunTo(GameRun run, long end, Action? presented = null)
    {
        run.OnPresented = () =>
        {
            presented?.Invoke();
            if (run.Clock.PresentCount >= end) throw new StopRun();
        };
        try
        {
            run.Run();
        }
        catch (StopRun)
        {
        }
    }

    private static PauseMenu Menu(GameRun run) =>
        Assert.IsType<OptionsPage>(run.FrontEnd[FrontEndPage.Options]).Menu ?? throw new InvalidOperationException("no options menu");

    [RetailGoldenFact(Tape)]
    public void DrawingTheOptionsMenuLeavesEveryPresentAsItIs()
    {
        var plain = new List<string>();
        GameRun a = NewRun();
        RunTo(a, 1300, () => plain.Add(FlowSnapshot.Capture(a).Line()));

        var drawn = new List<string>();
        var frames = new List<CanvasFrame>();
        GameRun b = NewRun();
        b.FrontEnd.Draw = new FrontEndDraw(new FrontEndTextures(GameData.Read)) { Canvas = new RecordingCanvas(frames.Add) };
        RunTo(b, 1300, () => drawn.Add(FlowSnapshot.Capture(b).Line()));

        Assert.Equal(plain, drawn);
        // The sound list (P700..1140): its scale bars' blank segments, drawn with the depth test off.
        CanvasFrame sound = frames.Single(f => f.Present == 820);
        int segments = sound.Quads.Count(q => q.Texture.Name == @"FrontEnd\v2\FE_Blank.tga" && !q.ZEnable);
        output.WriteLine($"P820: {sound.Quads.Count} quads, {segments} blank sprites");
        Assert.Equal(20, segments);
    }

    [RetailGoldenFact(Tape)]
    public void TheCursorSelectsARowAndAClickOpensIt()
    {
        GameRun run = NewRun();
        var seen = new List<(long Present, int Page, int Selected)>();
        // The first list (y 300, four rows from 245): Video Options' row is 285..305, Sound Options' 265..285. Messages
        // posted after a Present are handled in the next frame's process, before its render.
        RunTo(run, 592, () =>
        {
            long p = run.Clock.PresentCount;
            if (p < 590) return;
            PauseMenu menu = Menu(run);
            seen.Add((p, menu.Page, menu.Lists[0].Selected));
            if (p == 590) run.Input.PostMouseMessage(MouseMessage.Move, 320, 295, 0);
            if (p == 591)
            {
                run.Input.PostMouseMessage(MouseMessage.Move, 320, 275, 0);
                run.Input.PostMouseMessage(MouseMessage.LeftDown, 320, 275, 1);
                run.Input.PostMouseMessage(MouseMessage.LeftUp, 320, 275, 0);
            }
        });
        output.WriteLine(string.Join("; ", seen));
        Assert.Equal([(590L, 0, 0), (591L, 0, 2), (592L, 3, 1)], seen);
    }
}
