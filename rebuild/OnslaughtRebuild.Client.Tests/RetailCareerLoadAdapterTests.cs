// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

public sealed class RetailCareerLoadAdapterTests
{
    [Fact]
    public void ReadExplicitSelections_ReadsOnlyTheNamedGoldFixture()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "gold_career_save.bin");

        string[] arguments = [$"--career-save={fixturePath}"];
        // Exercise the host's earlier argument gate as well as the file adapter.
        // The former gate rejected this supported argument before the adapter ran.
        FirstFlightLaunchOptions.Parse(arguments);
        IReadOnlyList<RetailCareerDescriptor> descriptors =
            RetailCareerLoadAdapter.ReadExplicitSelections(
                arguments);

        RetailCareerDescriptor descriptor = Assert.Single(descriptors);
        Assert.Null(descriptor.SlotNumber);
        Assert.Equal("gold_career_save", descriptor.Name);
        Assert.Equal(43, descriptor.Career.CompletedWorldCount);
        Assert.Equal(10_004, descriptor.Career.ContainerLength);
    }

    [Fact]
    public void GodotHost_PassesExplicitCareerSelectionsIntoTheFrontendSession()
    {
        string frontendSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string hostSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "FirstFlightGame.cs"));

        Assert.Contains(
            "public void Initialize(IReadOnlyList<RetailCareerDescriptor> careerDescriptors)",
            frontendSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_session = new RetailFrontendSession(careerDescriptors);",
            frontendSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "CareerSelected?.Invoke(selectedCareer);",
            frontendSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "RetailCareerLoadAdapter.ReadExplicitSelections(OS.GetCmdlineUserArgs())",
            hostSource,
            StringComparison.Ordinal);
        Assert.Contains("_frontend.Initialize(careerDescriptors);", hostSource, StringComparison.Ordinal);
        Assert.Contains("_frontend.CareerSelected += SelectCareer;", hostSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadExplicitSelections_DoesNotTreatBarePathsAsDiscoveryRequests()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "gold_career_save.bin");

        IReadOnlyList<RetailCareerDescriptor> descriptors =
            RetailCareerLoadAdapter.ReadExplicitSelections(["--skipfmv", fixturePath]);

        Assert.Empty(descriptors);
    }
}
