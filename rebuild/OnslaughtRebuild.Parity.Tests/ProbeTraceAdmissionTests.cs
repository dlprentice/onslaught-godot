// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ProbeTraceAdmissionTests
{
    public static IEnumerable<object[]> TruncatedRows =>
        from name in ProbeCatalog.Names
        from ending in new[] { "", "\n" }
        select new object[] { $"P 1 {name} {ending}" };

    [Theory]
    [MemberData(nameof(TruncatedRows))]
    public void AnEmptyValueTokenCannotStandForNull(string text) =>
        Assert.Throws<FormatException>(() => ProbeTrace.Parse(text));

    [Fact]
    public void ExplicitNullAndProgrammaticEmptyPayloadsStillRoundTrip()
    {
        foreach (string name in ProbeCatalog.Names)
        {
            string text = $"P 1 {name} null\n";
            ProbeTrace parsed = ProbeTrace.Parse(text);
            Assert.True(parsed.TryGet(1, name, out byte[] bytes));
            Assert.Empty(bytes);
            Assert.False(parsed.TryGet(2, name, out _));
            var parsedWriter = new StringWriter();
            parsed.Write(parsedWriter);
            Assert.Equal(text, parsedWriter.ToString());

            var recorded = new ProbeTrace();
            recorded.Add(1, name, []);
            var recordedWriter = new StringWriter();
            recorded.Write(recordedWriter);
            Assert.Equal(text, recordedWriter.ToString());
        }
    }

    [Fact]
    public void ACompleteZeroPayloadRemainsDistinctFromNull()
    {
        foreach (string name in ProbeCatalog.Names)
        {
            string text = $"P 1 {name} null\nP 2 {name} {new string('0', ProbeCatalog.LengthOf(name) * 2)}\n";
            ProbeTrace trace = ProbeTrace.Parse(text);
            Assert.True(trace.TryGet(1, name, out byte[] absent));
            Assert.Empty(absent);
            Assert.True(trace.TryGet(2, name, out byte[] present));
            Assert.Equal(new byte[ProbeCatalog.LengthOf(name)], present);
            var writer = new StringWriter();
            trace.Write(writer);
            Assert.Equal(text, writer.ToString());
        }
    }
}
