// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class OriginalLossReferenceTests
{
    [Fact]
    public void RepeatedIdenticalRowsRemainAValidReference()
    {
        string[] lines = ["P 7 EVT 01020304", "P 7 EVT 01020304"];
        OriginalLossRouteTests.Original reference = OriginalLossRouteTests.Original.LoadRows(lines);
        Assert.Equal("01020304", reference.Get(7, "EVT"));
        Assert.Null(reference.Get(7, "LIFE"));
        Assert.Equal(7, reference.Last);
    }

    [Theory]
    [InlineData("01020304", "01020305")]
    [InlineData("01020305", "01020304")]
    public void ConflictingRowsForTheSamePresentAndFieldAreRejected(string first, string second)
    {
        string[] lines = [$"P 7 EVT {first}", $"P 7 EVT {second}"];
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => OriginalLossRouteTests.Original.LoadRows(lines));
        Assert.Contains("P7 EVT", error.Message);
    }
}
