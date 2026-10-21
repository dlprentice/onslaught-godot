// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using Godot;
using OnslaughtRebuild.Parity;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;

namespace OnslaughtRebuild.Host;

/// <summary>
/// HOST_LIVE=flow: the whole game, boot to quit (<see cref="GameRun"/>), on its own thread through
/// <see cref="SteppedGameRun"/>. Godot's thread advances it one Present per fixed step and only ever reads the immutable
/// <see cref="FlowSnapshot"/> each Present hands back; keys go to the run's thread through its input queue. HOST_TAPE
/// replays a tape instead of the keyboard; HOST_SAVES ("NAME=FILE;...") puts career saves on the card first;
/// HOST_FULL_BOOT=1 plays the boot films (default: as -skipfmv); HOST_TRACE writes each snapshot's line; HOST_END_PRESENT
/// stops the run; HOST_DRAW=0 (with a tape) runs as fast as it can. HOST_FLOW_STRAIGHT=1 (with a tape, an end and a trace)
/// instead runs the same game straight through on this thread, writing the reference the stepped trace must equal.
/// This slice shows where the run is, not the game's pictures.
/// </summary>
public partial class Host
{
    private bool _flowMode;
    private bool _flowDraw;
    private bool _flowReplay;
    private SteppedGameRun? _flowRun;
    private StreamWriter? _flowTrace;
    private FlowSnapshot? _flowLast;
    private long? _flowEnd;
    private double _flowAccumulator;
    private string? _flowStop;
    private bool _flowFailure;
    private int _flowSettle;
    private Label? _flowStatus;

    private void ReadyFlow()
    {
        _flowMode = true;
        _flowDraw = System.Environment.GetEnvironmentVariable("HOST_DRAW") != "0";
        string? tape = System.Environment.GetEnvironmentVariable("HOST_TAPE");
        _flowReplay = tape is not null;
        if (!_flowDraw && !_flowReplay)
        {
            throw new ArgumentException("HOST_DRAW=0 requires HOST_TAPE; physical input requires the visible preview");
        }

        if (System.Environment.GetEnvironmentVariable("HOST_END_PRESENT") is { } endText)
        {
            _flowEnd = long.Parse(endText);
        }

        GameFiles files = GameFiles.Find() ?? throw new InvalidOperationException("the game installation was not found (set ONSLAUGHT_GAME_DIR)");
        bool fullBoot = System.Environment.GetEnvironmentVariable("HOST_FULL_BOOT") == "1";
        var run = new GameRun(files.Read, id => new RetailPcText(files.Read("BEA.exe").Bytes, 0)[id],
            new InstalledFilmHeaders(files.Read), !fullBoot, LevelSession.VirtualVoiceFor(files.Read));
        if (tape is not null)
        {
            run.Tape = RetailTape.Load(tape);
        }

        if (System.Environment.GetEnvironmentVariable("HOST_SAVES") is { } saves)
        {
            foreach (string entry in saves.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split('=', 2);
                run.FrontEnd.MemoryCard.Folder.Write(pair[0], File.ReadAllBytes(pair[1]));
            }
        }

        if (System.Environment.GetEnvironmentVariable("HOST_TRACE") is { } trace)
        {
            _flowTrace = new StreamWriter(File.Open(trace, FileMode.CreateNew));
        }

        if (System.Environment.GetEnvironmentVariable("HOST_FLOW_STRAIGHT") == "1")
        {
            RunFlowStraight(run);
            return;
        }

        _flowRun = new SteppedGameRun(run);
        _flowStatus = new Label { Name = "FlowStatus", Position = new Vector2(8, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_flowStatus);
        GD.Print($"HOST_FLOW_READY replay={_flowReplay} drawing={_flowDraw} full_boot={fullBoot} silent=true");
    }

    private void ProcessFlow(double delta)
    {
        if (_flowRun is null)
        {
            return;
        }

        if (_flowStop is null)
        {
            if (!_flowDraw)
            {
                // A replay without drawing: as many Presents as a frame allows; time is the run's own.
                for (int i = 0; i < 2000 && _flowStop is null; i++)
                {
                    StepFlow();
                }
            }
            else
            {
                _flowAccumulator += Math.Clamp(delta, 0, PlatformClock.ReplayDt * 4);
                int steps = 0;
                while (_flowAccumulator >= PlatformClock.ReplayDt && steps++ < 4 && _flowStop is null)
                {
                    _flowAccumulator -= PlatformClock.ReplayDt;
                    StepFlow();
                }
            }

            UpdateFlowStatus();
        }
        else if (++_flowSettle == 4)
        {
            FinishFlow();
            GetTree().Quit(_flowFailure ? 1 : 0);
        }
    }

    private void StepFlow()
    {
        FlowSnapshot? snapshot = _flowRun!.Step();
        if (snapshot is null)
        {
            _flowFailure = _flowRun.Failure is not null;
            _flowStop = _flowRun.Failure is { } failure
                ? $"The run stopped at an unsupported behavior or error after Present {_flowLast?.Present}.\n{failure.Message}"
                : $"The game quit (exit code {_flowRun.ExitCode}).";
            if (_flowRun.Failure is { } e)
            {
                GD.PrintErr($"HOST_FLOW_STOPPED present={_flowLast?.Present}: {e}");
            }

            return;
        }

        _flowLast = snapshot;
        _flowTrace?.WriteLine(snapshot.Line());
        if (_flowEnd is { } end && snapshot.Present >= end)
        {
            _flowStop = "Preview complete.";
        }
    }

    private void UpdateFlowStatus()
    {
        if (_flowStatus is null || _flowLast is not { } s)
        {
            return;
        }

        string where = s.Segment switch
        {
            FlowSegment.FrontEnd => $"front end, page {(FrontEndPage)BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(s.FrontEndLine![8..16]))}",
            FlowSegment.Level => $"level {BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(s.LevelFields!["LVL"]))}",
            _ => "boot",
        };
        _flowStatus.Text = $"Present {s.Present} · {where} · {(_flowReplay ? "replay" : "keyboard")} · silent preview"
            + (_flowStop is null ? "" : $"\n{_flowStop}");
    }

    private void InputFlow(InputEvent input)
    {
        if (_flowReplay || _flowRun is null || _flowStop is not null || input is not InputEventKey key || key.Echo)
        {
            return;
        }

        if (HostKeyMap.TryMap(key.PhysicalKeycode, key.Location, out HostKeyMap.PcKey pc))
        {
            bool down = key.Pressed;
            _flowRun.Post(run => run.Input.PostTapeKey(pc.VirtualKey, pc.ScanCode, down));
            GetViewport().SetInputAsHandled();
        }
    }

    private sealed class StopStraight : Exception;

    /// <summary>The reference: the run straight through on Godot's thread, each Present's line written as it ends.</summary>
    private void RunFlowStraight(GameRun run)
    {
        if (_flowTrace is null || _flowEnd is not { } end || !_flowReplay)
        {
            throw new ArgumentException("HOST_FLOW_STRAIGHT requires HOST_TAPE, HOST_END_PRESENT and HOST_TRACE");
        }

        run.OnPresented = () =>
        {
            _flowTrace.WriteLine(FlowSnapshot.Capture(run).Line());
            if (run.Clock.PresentCount >= end)
            {
                throw new StopStraight();
            }
        };
        string result;
        try
        {
            result = $"exit {run.Run()}";
        }
        catch (StopStraight)
        {
            result = "end";
        }

        _flowTrace.Dispose();
        _flowTrace = null;
        GD.Print($"HOST_FLOW_STRAIGHT_FINISHED present={run.Clock.PresentCount} result={result}");
        GetTree().Quit(0);
    }

    private void FinishFlow()
    {
        _flowTrace?.Dispose();
        _flowTrace = null;
        _flowRun?.Dispose();
        _flowRun = null;
        GD.Print($"HOST_FLOW_FINISHED present={_flowLast?.Present} failure={_flowFailure} stop={_flowStop?.Split('\n')[0]}");
    }
}
