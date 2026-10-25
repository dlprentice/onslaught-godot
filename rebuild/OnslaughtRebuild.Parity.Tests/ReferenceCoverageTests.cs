// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ReferenceCoverageTests
{
    private static Dictionary<(long, string), string> Complete() => new()
    {
        [(3, "EVT")] = "00000000", [(4, "EVT")] = "00000000", [(5, "EVT")] = "00000000",
        [(3, "POS")] = "null", [(4, "POS")] = new string('0', 24), [(5, "POS")] = new string('0', 24),
    };

    [Fact]
    public void EndpointAloneCannotStandForACompleteRoute()
    {
        var probes = Complete();
        probes.Remove((4, "EVT")); probes.Remove((4, "POS"));
        Assert.Contains("Present 4", Assert.Throws<InvalidDataException>(() =>
            ReferenceCoverage.Require(probes, 3, 5, 1, "EVT", "POS")).Message);
        Assert.Throws<InvalidDataException>(() => ReferenceCoverage.Require(new Dictionary<(long, string), string>(), 3, 5, 1, "EVT"));
    }

    [Fact]
    public void EveryRequiredFieldMustExistAtEveryScheduledPresent()
    {
        var probes = Complete(); probes.Remove((4, "POS"));
        Assert.Contains("POS at Present 4", Assert.Throws<InvalidDataException>(() =>
            ReferenceCoverage.Require(probes, 3, 5, 1, "EVT", "POS")).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("000000")]
    [InlineData("zzzzzzzz")]
    [InlineData("null")]
    public void MalformedRequiredValuesAreRejected(string value)
    {
        var probes = Complete(); probes[(4, "EVT")] = value;
        Assert.Throws<InvalidDataException>(() => ReferenceCoverage.Require(probes, 3, 5, 1, "EVT", "POS"));
    }

    [Fact]
    public void CompleteReferencesAndExplicitMissingPlayersAreAdmitted() =>
        ReferenceCoverage.Require(Complete(), 3, 5, 1, "EVT", "POS");

    [RetailGoldenFact("vds/level100.vds-a.probes")]
    public void RemovingAnInteriorProbeFromTheRetainedReferenceFailsAdmission()
    {
        var probes = TrainerRouteTests.ReadPortProbes("vds/level100.vds-a.probes");
        string[] names = ["EVT", "FT", "RNG", "SC", "LVL", "POS", "LIFE"];
        ReferenceCoverage.Require(probes, 50, 10000, 50, names);
        Assert.True(probes.Remove((5000, "POS"))); // modify this small in-memory copy only
        Assert.Contains("POS at Present 5000", Assert.Throws<InvalidDataException>(() =>
            ReferenceCoverage.Require(probes, 50, 10000, 50, names)).Message);
    }
}
