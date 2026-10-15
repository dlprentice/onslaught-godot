// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class BriefingAndConfigTests
{
    private sealed class Videos(params string[] present) : IFrontEndVideo
    {
        public List<(string Path, int Width, int Height)> Opened { get; } = [];
        public bool Exists(string path) => present.Contains(path);
        public void Open(string path, int width, int height) => Opened.Add((path, width, height));
    }

    // A synthetic WorldHeaders.dat: (version, level, configurations, tara, billy, jason).
    private static byte[] Headers(params (int Version, int Level, string[] Configs, bool Tara, bool Billy, bool Jason)[] levels)
    {
        using MemoryStream stream = new();
        using BinaryWriter w = new(stream);
        w.Write(1);
        w.Write(levels.Length);
        foreach (var l in levels)
        {
            w.Write(l.Version);
            w.Write(0);
            w.Write(l.Level);
            w.Write(l.Configs.Length);
            foreach (string c in l.Configs)
            {
                w.Write((byte)c.Length);
                w.Write(Encoding.ASCII.GetBytes(c));
            }
            if (l.Version >= 2)
            {
                w.Write(l.Tara ? 1 : 0);
                w.Write(l.Billy ? 1 : 0);
                w.Write(l.Jason ? 1 : 0);
            }
            if (l.Version >= 3) w.Write(7);
        }
        return stream.ToArray();
    }

    private static readonly byte[] TestHeaders = Headers(
        (3, 100, ["alpha", "beta"], false, false, false),
        (1, 110, ["gamma"], false, false, false),
        (2, 231, ["delta"], false, true, false));

    private sealed class Rig
    {
        public FrontEnd FrontEnd { get; }
        public BriefingPage Briefing { get; }
        public BeConfigPage Config { get; }
        public Videos Videos { get; }

        public Rig(int level, byte[]? headers, params string[] videos)
        {
            FrontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
            FrontEnd.Career.Blank();
            FrontEnd.ReadDataFile = path => path == WorldHeaders.FileName ? headers : null;
            FrontEnd.CockpitOf = name => name == "unknown" ? null : name + "_cockpit";
            Videos = new Videos(videos);
            FrontEnd.Video = Videos;
            Briefing = new BriefingPage(FrontEnd);
            Config = new BeConfigPage(FrontEnd);
            FrontEnd[FrontEndPage.Briefing] = Briefing;
            FrontEnd[FrontEndPage.BeConfig] = Config;
            FrontEnd.SelectedLevel = level;
        }
    }

    [Fact]
    public void TheHeadersReadEveryVersionsFields()
    {
        WorldHeaders headers = WorldHeaders.Read(MemBuffer.FromFile(WorldHeaders.FileName, TestHeaders));
        Assert.Equal([100, 110, 231], headers.Levels.Select(l => l.Level));
        Assert.Equal(["alpha", "beta"], headers.ForLevel(100)!.Configurations);
        Assert.Equal(7, headers.ForLevel(100)!.Unknown10);
        // Before version 2 only Tara is offered.
        Assert.Equal((true, false, false, 1), (headers.ForLevel(110)!.Tara, headers.ForLevel(110)!.Billy, headers.ForLevel(110)!.Jason, headers.ForLevel(110)!.NumWingmen));
        Assert.Equal(1, headers.ForLevel(231)!.NumWingmen);
        Assert.Null(headers.ForLevel(200));
    }

    [GameDataFact]
    public void TheGamesHeadersCoverEveryCampaignWorldAndEndExactly()
    {
        (string name, byte[] bytes) = GameData.Read(WorldHeaders.FileName);
        MemBuffer file = MemBuffer.FromFile(name, bytes);
        WorldHeaders headers = WorldHeaders.Read(file);
        Assert.Equal((uint)file.Length, file.Position);
        for (int node = 0; node < CareerTables.LevelStructure.GetLength(0); node++)
        {
            WorldHeader? header = headers.ForLevel(CareerTables.LevelStructure[node, 0]);
            Assert.NotNull(header);
            Assert.NotEmpty(header.Configurations);
        }
    }

    [Fact]
    public void WithoutTheHeadersFileTheFrontEndCannotStart()
    {
        Rig r = new(100, null);
        Assert.False(r.FrontEnd.Begin(FrontEndPage.Briefing));
    }

    [Fact]
    public void TheBriefingOpensTheBestVideoThereIs()
    {
        Rig r = new(212, TestHeaders, @"data\video\Briefings\PC_211_exact.vid");
        r.FrontEnd.Begin(FrontEndPage.Briefing);
        Assert.Equal((@"data\video\Briefings\PC_211_exact.vid", 201, 149), r.Videos.Opened.Single());

        r = new(100, TestHeaders, @"data\video\Briefings\PC_100.vid");
        r.FrontEnd.Begin(FrontEndPage.Briefing);
        Assert.Equal((@"data\video\Briefings\PC_100.vid", 256, 256), r.Videos.Opened.Single());
        Assert.Equal((0.78515625f, 0.58203125f), r.Briefing.VideoScale);

        r = new(100, TestHeaders);
        r.FrontEnd.Begin(FrontEndPage.Briefing);
        Assert.Equal((@"data\video\Briefings\PC_100_25.vid", 256, 256), r.Videos.Opened.Single());
    }

    [Fact]
    public void TheBriefingGoesOnToTheConfigurationAndBackToTheLevelSelect()
    {
        Rig r = new(100, TestHeaders);
        r.FrontEnd.Begin(FrontEndPage.Briefing);
        r.Briefing.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal((FrontEndPage.BeConfig, 25f), (r.FrontEnd.TransitionTo, r.FrontEnd.TransitionTime));

        r = new(100, TestHeaders);
        r.FrontEnd.Begin(FrontEndPage.Briefing);
        r.Briefing.ButtonPressed(FrontEndButtons.MenuBack, 1f);
        Assert.Equal((FrontEndPage.LevelSelect, 30f), (r.FrontEnd.TransitionTo, r.FrontEnd.TransitionTime));
        Assert.Equal((@"data\video\FEBack128.vid", 128, 128), r.Videos.Opened[^1]);
        Assert.False(r.Briefing.VideoOpen);
    }

    [Fact]
    public void TheConfigurationWrapsAndSelectStartsTheLevel()
    {
        Rig r = new(100, TestHeaders);
        r.FrontEnd.Begin(FrontEndPage.BeConfig);
        Assert.Equal(2, r.Config.NumConfigs);
        r.Config.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        Assert.Equal((1, 1f), (r.Config.Config, r.Config.DownFlash));
        r.Config.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        Assert.Equal(0, r.Config.Config);
        r.Config.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        r.Config.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal(100, r.FrontEnd.Quit);
        Assert.Equal((1, "beta_cockpit"), (r.FrontEnd.Settings.Player1Configuration, r.FrontEnd.Settings.Player1CockpitMesh));
        Assert.False(r.FrontEnd.Step());
    }

    [Fact]
    public void FromWorld231WithWingmenSelectGoesToTheWingmenPage()
    {
        Rig r = new(231, TestHeaders);
        r.FrontEnd.Begin(FrontEndPage.BeConfig);
        r.Config.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal((FrontEndPage.Wingmen, 25f), (r.FrontEnd.TransitionTo, r.FrontEnd.TransitionTime));
        Assert.Equal(FrontEnd.QuitRunning, r.FrontEnd.Quit);
    }

    [Fact]
    public void TheUpArrowIsClickable()
    {
        Rig r = new(100, TestHeaders);
        r.FrontEnd.Begin(FrontEndPage.BeConfig);
        // The click tests the cursor's position, which only a move sets.
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.Move, 162, 141, 0);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 162, 141, 0x1);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, 162, 141, 0);
        r.FrontEnd.Step();
        Assert.Equal((1, 1f), (r.Config.Config, r.Config.UpFlash));
    }
}
