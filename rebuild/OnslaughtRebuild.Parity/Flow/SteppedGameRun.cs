// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// A <see cref="GameRun"/> a host advances one Present at a time. The run lives on its own thread; at the end of each
/// Present (<see cref="GameRun.OnPresented"/>) that thread captures an immutable <see cref="FlowSnapshot"/>, hands it
/// over and waits for the next <see cref="Step"/>. The host thread reads snapshots, and its input goes into a queue that
/// the run's thread drains at the end of the Present it was queued in (as a tape's events are delivered), so the host's
/// timing cannot change what the run computes. The one way the host may look at the run's objects themselves (to draw a
/// level) is <see cref="Inspect"/>, which only runs while the run's thread is parked at a Present and throws otherwise:
/// between a <see cref="Step"/>'s return and the next call, nothing on the run's thread moves.
/// </summary>
public sealed class SteppedGameRun : IDisposable
{
    private readonly GameRun _run;
    private readonly SemaphoreSlim _go = new(0, 1);
    private readonly SemaphoreSlim _presented = new(0, 1);
    private readonly Queue<Action<GameRun>> _input = new();
    private readonly object _inputLock = new();
    private readonly Thread _thread;
    private FlowSnapshot? _latest;
    private Exception? _failure;
    private volatile bool _stopping;
    private volatile bool _parked;
    private bool _finished;

    private sealed class Stopped : Exception;

    /// <param name="run">A run not yet started; its <see cref="GameRun.OnPresented"/> becomes this driver's.</param>
    public SteppedGameRun(GameRun run)
    {
        _run = run;
        run.OnPresented = Presented;
        _thread = new Thread(Main) { Name = "GameRun", IsBackground = true };
    }

    /// <summary>The run's exit code once it has finished (the front end's quit), else null.</summary>
    public int? ExitCode { get; private set; }

    /// <summary>The exception that ended the run (a NotSupportedException for an unported behavior), else null.</summary>
    public Exception? Failure => _failure;

    /// <summary>Whether the run has ended (finished or failed); no more Presents come.</summary>
    public bool Ended => _finished;

    /// <summary>
    /// Queues an input action (for example a window key message) for the run's thread, which applies it at the end of
    /// the Present now being prepared, after that Present's tape events.
    /// </summary>
    public void Post(Action<GameRun> input)
    {
        lock (_inputLock)
        {
            _input.Enqueue(input);
        }
    }

    /// <summary>
    /// Lets the run make its next Present and returns that Present's snapshot, or null once the run has ended (then see
    /// <see cref="ExitCode"/> and <see cref="Failure"/>).
    /// </summary>
    public FlowSnapshot? Step()
    {
        if (_finished)
        {
            return null;
        }

        if (!_thread.IsAlive && _latest is null)
        {
            _thread.Start();
        }
        else
        {
            _parked = false;
            _go.Release();
        }

        _presented.Wait();
        return _finished ? null : _latest;
    }

    private void Main()
    {
        try
        {
            ExitCode = _run.Run();
        }
        catch (Stopped)
        {
        }
        catch (Exception e)
        {
            _failure = e;
        }
        finally
        {
            _finished = true;
            _presented.Release();
        }
    }

    private void Presented()
    {
        lock (_inputLock)
        {
            while (_input.Count > 0)
            {
                _input.Dequeue()(_run);
            }
        }

        _latest = FlowSnapshot.Capture(_run);
        _parked = true;
        _presented.Release();
        _go.Wait();
        if (_stopping)
        {
            throw new Stopped();
        }
    }

    /// <summary>
    /// Runs <paramref name="read"/> on the host's thread with the run's objects, while the run's thread is parked at the
    /// Present the last <see cref="Step"/> returned (or has ended). The read must not change the run: it is for drawing.
    /// Throws when the run's thread may be moving (before the first step, or during one).
    /// </summary>
    public void Inspect(Action<GameRun> read)
    {
        bool ended = _finished && !_thread.IsAlive;
        if (!_parked && !ended)
        {
            throw new InvalidOperationException("The run's thread is not parked at a Present; its objects cannot be read now.");
        }

        read(_run);
    }

    /// <summary>Ends a run that is waiting for its next step (its thread unwinds) and waits for the thread.</summary>
    public void Dispose()
    {
        if (!_finished && _thread.IsAlive)
        {
            _stopping = true;
            _go.Release();
            _thread.Join();
        }
    }
}
