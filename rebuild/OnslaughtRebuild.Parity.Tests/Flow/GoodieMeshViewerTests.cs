// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class GoodieMeshViewerTests
{
    private sealed class Load(GoodieMeshResource resource) : IGoodieMeshLoad
    {
        public bool Ready { get; set; }
        public GoodieMeshResource Resource => resource;
        public int Reads { get; private set; }
        public int Releases { get; private set; }
        public GoodieMeshResource Read() { Reads++; return resource; }
        public void Release() => Releases++;
    }

    private static (FrontEnd Fe, GoodiesPage Page, Load Load) Rig()
    {
        FrontEnd fe = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        fe.Career.Blank();
        fe.Career.Record.SetGoodieState(8, CareerGoodieState.New);
        var page = new GoodiesPage(fe);
        var load = new Load(GoodieMeshResource.Read("authored.bin", GoodieMeshResourceTests.Resource(), 8,
            GoodieMeshResourceTests.NoExternalMesh));
        page.StartMeshLoad = number => { Assert.Equal(8, number); return load; };
        page.Init();
        page.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        return (fe, page, load);
    }

    [Fact]
    public void StartsOnlyAfterSelectionMarksSeenAndWaitsForTheExternalCompletion()
    {
        var (fe, page, load) = Rig();
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.True(page.DisplayGoody);
        Assert.Equal(CareerGoodieState.Old, fe.Career.Record.GoodieState(8));
        page.Process(FrontEndPageState.Active);
        Assert.Null(page.MeshResource);
        Assert.Equal(0f, page.ImageZoom);
        Assert.Equal(0, load.Reads);
        load.Ready = true;
        page.Process(FrontEndPageState.Active);
        Assert.Same(load.Resource, page.MeshResource);
        Assert.Equal(.3f, page.ImageZoom);
        Assert.Equal(1, load.Reads);
        page.Process(FrontEndPageState.Active);
        Assert.Equal(1, load.Reads);
    }

    [Fact]
    public void BackEasesTheViewerClosedBeforeReleasingItsMesh()
    {
        var (_, page, load) = Rig();
        load.Ready = true;
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        for (int i = 0; i < 20; i++) page.Process(FrontEndPageState.Active);
        Assert.Equal(1f, page.ImageZoom);
        Assert.Throws<NotSupportedException>(() => page.ButtonPressed(Buttons.CameraStrafeLeft, 1f));
        page.ButtonPressed(FrontEndButtons.MenuBack, 1f);
        Assert.False(page.DisplayGoody);
        page.Process(FrontEndPageState.Active);
        Assert.Equal(.6f, page.ImageZoom);
        Assert.Equal(0, load.Releases);
        for (int i = 0; i < 10; i++) page.Process(FrontEndPageState.Active);
        Assert.Equal(0f, page.ImageZoom);
        Assert.Null(page.MeshResource);
        Assert.Equal(1, load.Releases);
        page.Shutdown();
        Assert.Equal(1, load.Releases);
    }

    [Fact]
    public void MissingLoaderAndWrongGoodieRemainExplicitLimits()
    {
        var (fe, page, _) = Rig();
        page.StartMeshLoad = null;
        Assert.Throws<NotSupportedException>(() => page.ButtonPressed(FrontEndButtons.MenuSelect, 1f));
        Assert.Equal(CareerGoodieState.New, fe.Career.Record.GoodieState(8));
        fe.Career.Record.SetGoodieState(9, CareerGoodieState.New);
        page.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        page.StartMeshLoad = _ => throw new InvalidOperationException("must not start an unsupported goodie");
        Assert.Throws<NotSupportedException>(() => page.ButtonPressed(FrontEndButtons.MenuSelect, 1f));
        Assert.Equal(CareerGoodieState.New, fe.Career.Record.GoodieState(9));
    }

    [Fact]
    public void ClosingAnUnfinishedLoadIsExplicitlyUnsupported()
    {
        var (_, page, load) = Rig();
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        page.ButtonPressed(FrontEndButtons.MenuBack, 1f);
        Assert.Throws<NotSupportedException>(() => page.Process(FrontEndPageState.Active));
        Assert.Equal(0, load.Releases);
    }

    [Fact]
    public void TheLoaderCannotSubstituteAnotherGoodie()
    {
        var (_, page, _) = Rig();
        var wrong = new Load(GoodieMeshResource.Read("wrong.bin", GoodieMeshResourceTests.Resource(number: 9), 9,
            GoodieMeshResourceTests.NoExternalMesh)) { Ready = true };
        page.StartMeshLoad = _ => wrong;
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Throws<InvalidDataException>(() => page.Process(FrontEndPageState.Active));
        Assert.Null(page.MeshResource);
    }

    [Fact]
    public void SupplyingACanvasDoesNotChangeTheDiagnosticViewerState()
    {
        var (fe, page, load) = Rig();
        var (_, other, otherLoad) = Rig();
        fe.Draw = new FrontEndDraw(new FrontEndTextures(_ => throw new FileNotFoundException()));
        load.Ready = otherLoad.Ready = true;
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        other.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        page.Process(FrontEndPageState.Active);
        other.Process(FrontEndPageState.Active);
        page.Render(1f, FrontEndPage.None);
        other.Render(1f, FrontEndPage.None);
        Assert.Equal((other.Column, other.Row, other.DisplayGoody, other.ImageZoom, other.SelectFade, other.Yaw),
            (page.Column, page.Row, page.DisplayGoody, page.ImageZoom, page.SelectFade, page.Yaw));
        Assert.NotNull(page.MeshResource);
        Assert.Equal(CareerGoodieState.Old, fe.Career.Record.GoodieState(8));
    }

    [Fact]
    public void GridRenderUpdatesTheSelectionOnMouseMovement()
    {
        var (fe, page, _) = Rig();
        fe.Platform.Mouse.X = 193;
        fe.Platform.Mouse.Y = 342;
        fe.Platform.Mouse.Moved = true;
        page.Render(1f, FrontEndPage.None);
        Assert.Equal((1, 2), (page.Column, page.Row));
    }

    [Fact]
    public void GridClickSelectsAndStartsTheSameViewerPathAsTheController()
    {
        var (fe, page, load) = Rig();
        fe.Platform.Mouse.X = 148;
        fe.Platform.Mouse.Y = 297;
        fe.Platform.Mouse.Buttons[0].Released = true;
        page.Render(1f, FrontEndPage.None);
        Assert.True(page.DisplayGoody);
        Assert.False(fe.Platform.Mouse.Buttons[0].Released);
        Assert.Equal(CareerGoodieState.Old, fe.Career.Record.GoodieState(8));
        Assert.Equal(0, load.Reads);
    }
}
