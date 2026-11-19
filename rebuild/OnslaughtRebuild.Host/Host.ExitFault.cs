// SPDX-License-Identifier: GPL-3.0-only
namespace OnslaughtRebuild.Host;

public partial class Host
{
    private bool _exitFaultTaken;
    private int _exitFaultCalls;

    /// <summary>
    /// Test-only exit fault (HOST_EXIT_FAULT=flow-process or audio-start): reports once, at that point, that the test
    /// asks it to fail, so the exits behind those two error boundaries can be driven offscreen. flow-process fires on its
    /// 120th check (after the front end has drawn); audio-start fires on its first. Unset, it never fires.
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
