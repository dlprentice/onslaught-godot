using System;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class FVectorHashTests
{
    [Fact]
    public void HashOfAuthoredVectorDoesNotDependOnProcessSeed()
    {
        var vector = new global::OnslaughtRebuild.Parity.Maths.FVector(1.25f, 2.5f, 3.75f);
        Assert.Equal(unchecked((int)0xD3B00000), vector.GetHashCode());
    }

    [Fact]
    public void EqualComponentsAlwaysHaveEqualHashesForFloatEdgeCases()
    {
        float[] samples =
        {
            0.0f, -0.0f, 1.0f, -1.0f, float.PositiveInfinity, float.NegativeInfinity,
            float.NaN,
            BitConverter.Int32BitsToSingle(unchecked((int)0x7F800001)),
            BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234)),
            BitConverter.Int32BitsToSingle(unchecked((int)0xFF800001)),
            BitConverter.Int32BitsToSingle(unchecked((int)0xFFC01234)),
        };
        foreach (float component in samples)
        foreach (float otherComponent in samples)
        {
            var firstX = new global::OnslaughtRebuild.Parity.Maths.FVector(component, 2.0f, 3.0f);
            var secondX = new global::OnslaughtRebuild.Parity.Maths.FVector(otherComponent, 2.0f, 3.0f);
            var firstY = new global::OnslaughtRebuild.Parity.Maths.FVector(1.0f, component, 3.0f);
            var secondY = new global::OnslaughtRebuild.Parity.Maths.FVector(1.0f, otherComponent, 3.0f);
            var firstZ = new global::OnslaughtRebuild.Parity.Maths.FVector(1.0f, 2.0f, component);
            var secondZ = new global::OnslaughtRebuild.Parity.Maths.FVector(1.0f, 2.0f, otherComponent);
            if (firstX.Equals(secondX))
                Assert.Equal(firstX.GetHashCode(), secondX.GetHashCode());
            if (firstY.Equals(secondY))
                Assert.Equal(firstY.GetHashCode(), secondY.GetHashCode());
            if (firstZ.Equals(secondZ))
                Assert.Equal(firstZ.GetHashCode(), secondZ.GetHashCode());
        }
    }
}
