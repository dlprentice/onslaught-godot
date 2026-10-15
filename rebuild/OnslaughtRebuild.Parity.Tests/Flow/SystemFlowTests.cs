// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class SystemFlowTests
{
    private sealed class Parts(Queue<int> frontEnd, Queue<QuitType> levels, params string[] skipped) : ISystemParts
    {
        public List<string> Films { get; } = [];
        public bool PlayVendorFilm { get; set; }
        public int RunFrontEnd(FrontEndEntry entry) => frontEnd.Dequeue();
        public QuitType RunLevel(int level) => levels.Dequeue();

        public bool PlayAttractFilm(string name)
        {
            Films.Add(name);
            return skipped.Contains(name);
        }
    }

    [Fact]
    public void TheFrontEndAndItsLevelsRunInTurnUntilTheFrontEndQuits()
    {
        Parts parts = new(new Queue<int>([100, 110, SystemFlow.QuitToSystem]),
            new Queue<QuitType>([QuitType.QuitToFrontEnd, QuitType.QuitToFrontEnd]));
        SystemFlow flow = new(parts);
        Assert.Equal(SystemFlow.QuitToSystem, flow.Run());
        Assert.Equal(["front end Start", "level 100", "front end Start", "level 110", "front end Start"], flow.Trace);
    }

    [Fact]
    public void QuittingALevelToTheTitleScreenEntersAtTheMainMenu()
    {
        Parts parts = new(new Queue<int>([100, SystemFlow.QuitToSystem]),
            new Queue<QuitType>([QuitType.UserQuitToTitleScreen]));
        SystemFlow flow = new(parts);
        flow.Run();
        Assert.Equal("front end TitleScreen", flow.Trace[^1]);
    }

    [Fact]
    public void TheTitleTimeoutPlaysTheFilmsButALevelTimeoutOnlyReentersFromThem()
    {
        Parts parts = new(new Queue<int>([SystemFlow.Attract, 100, SystemFlow.QuitToSystem]),
            new Queue<QuitType>([QuitType.QuitTimeout]));
        SystemFlow flow = new(parts);
        flow.Run();
        // CSystem::Run: a level's timeout sets the result to -3, which picks the entry at the loop's top; only the front
        // end returning -3 plays the films.
        Assert.Equal(["front end Start", "attract", "front end FromAttract", "level 100", "front end FromAttract"], flow.Trace);
        Assert.Equal(["ltlogo", "openingfmv"], parts.Films);
    }

    [Fact]
    public void TheAttractFilmsStopAtTheFirstOneSkipped()
    {
        Parts skipLogo = new(new Queue<int>([SystemFlow.Attract, SystemFlow.QuitToSystem]), new Queue<QuitType>(), "ltlogo");
        new SystemFlow(skipLogo).Run();
        Assert.Equal(["ltlogo"], skipLogo.Films);

        Parts vendor = new(new Queue<int>([SystemFlow.Attract, SystemFlow.QuitToSystem]), new Queue<QuitType>(),
            "TWIMTBP_GefFX_640x480_Audio") { PlayVendorFilm = true };
        new SystemFlow(vendor).Run();
        Assert.Equal(["ltlogo", "TWIMTBP_GefFX_640x480_Audio"], vendor.Films);
    }

    [Fact]
    public void TheFirstFrontEndIsAlwaysAStart()
    {
        Parts parts = new(new Queue<int>([SystemFlow.QuitToSystem]), new Queue<QuitType>());
        SystemFlow flow = new(parts);
        flow.Run();
        Assert.Equal(["front end Start"], flow.Trace);
    }
}
