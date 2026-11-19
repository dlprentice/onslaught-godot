// SPDX-License-Identifier: GPL-3.0-only
namespace OnslaughtRebuild.Host;

public partial class Host
{
    private bool _exitFaultTaken;
    private int _exitFaultCalls;

    /// <summary>
    /// Test-only exit fault (HOST_EXIT_FAULT=flow-process, audio-start, release-throw or audio-teardown): reports
    /// once, at that point, that the test asks it to fail, so error exits that cannot otherwise be reached offscreen
    /// can be driven: whole-game processing (on its 120th check, after the front end has drawn), the live audio
    /// startup, the shutdown's mode release, and the live audio teardown during that release. Unset, it never fires.
    /// </summary>
    private bool TakeExitFault(string point)
    {
        if (_exitFaultTaken || System.Environment.GetEnvironmentVariable("HOST_EXIT_FAULT") != point)
        {
            return false;
        }

        if (point == "flow-process" && ++_exitFaultCalls < 120)
        {
            return false;
        }

        _exitFaultTaken = true;
        return true;
    }

    private static InvalidOperationException ExitFaultError(string point) =>
        new($"HOST_EXIT_FAULT={point}: a test-only fault at this exit");
}
