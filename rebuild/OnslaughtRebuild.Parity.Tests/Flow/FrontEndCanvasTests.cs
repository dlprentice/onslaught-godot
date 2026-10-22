// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Game;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// Drawing never reaches the simulation: the front-end loss route through its front end (to Present 1400: the title, the
/// main menu, a new career named and saved, level select, briefing, configuration) makes the same snapshot every Present
/// with no drawing, with the front end drawing to a null canvas and with it drawing to a recording one; and two
/// recordings draw the same frames.
/// </summary>
public sealed class FrontEndCanvasTests(ITestOutputHelper output)
{
    private const string Tape = "portrefs/tapes/fe-level100-lose-quit-7300f.tape";
    private const long End = 1400;

    private sealed class StopRun : Exception;

    private static (List<string> Lines, List<CanvasFrame> Frames) Run(Func<IFrontEndCanvas?> canvas, FrontEndTextures textures)
    {
        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(Tape)!),
        };
        var frames = new List<CanvasFrame>();
        if (canvas() is { } c)
        {
            run.FrontEnd.Draw = new FrontEndDraw(textures) { Canvas = c is RecordingCanvas ? new RecordingCanvas(frames.Add) : c };
        }

        var lines = new List<string>();
        run.OnPresented = () =>
        {
            lines.Add(FlowSnapshot.Capture(run).Line());
            if (run.Clock.PresentCount >= End)
            {
                throw new StopRun();
            }
        };
        try
        {
            run.Run();
        }
        catch (StopRun)
        {
        }

        return (lines, frames);
    }

    private static string Hash(List<CanvasFrame> frames)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (CanvasFrame frame in frames)
        {
            sha.AppendData(BitConverter.GetBytes(frame.Present));
            sha.AppendData(BitConverter.GetBytes(frame.ClearColour ?? uint.MaxValue));
            foreach (CanvasQuad q in frame.Quads)
            {
                sha.AppendData(System.Text.Encoding.UTF8.GetBytes(q.ToString()));
            }
        }

        return Convert.ToHexString(sha.GetHashAndReset());
    }

    [RetailGoldenFact(Tape)]
    public void DrawingLeavesEveryPresentAsItIsAndRecordsTheSameFramesTwice()
    {
        var textures = new FrontEndTextures(GameData.Read);
        (List<string> none, _) = Run(() => null, textures);
        (List<string> nulled, _) = Run(() => NullCanvas.Instance, textures);
        (List<string> recorded, List<CanvasFrame> frames) = Run(() => new RecordingCanvas(_ => { }), textures);
        (_, List<CanvasFrame> again) = Run(() => new RecordingCanvas(_ => { }), textures);

        int drawn = frames.Count(f => f.Quads.Count > 0);
        output.WriteLine($"{none.Count} Presents; {frames.Count} front-end frames, {drawn} with quads, {frames.Sum(f => f.Quads.Count)} quads");
        Assert.Equal(End, none.Count);
        Assert.Equal(none, nulled);
        Assert.Equal(none, recorded);
        Assert.Equal(Hash(frames), Hash(again));
        Assert.True(drawn > 500, $"only {drawn} frames drew anything");
        Assert.Equal(frames.Select(f => f.Present), frames.Select(f => f.Present).Distinct());
    }
}
