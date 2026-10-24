// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Host;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ReplaySoundProfileTests
{
    [Fact]
    public void UnadmittedProfileFailsBeforeReadingGameData()
    {
        string path = Path.Combine(Path.GetTempPath(), $"onslaught-profile-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"admitted\":true,\"seed_profile\":{\"node_count\":256,\"seeds\":[]}}");
            int reads = 0;
            Assert.Throws<InvalidDataException>(() => ReplaySoundProfile.Load(path, name =>
            {
                reads++;
                return (name, []);
            }));
            Assert.Equal(0, reads);
        }
        finally { File.Delete(path); }
    }

    [RetailGoldenFact("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void ChangedBankCannotUseAdmittedPool()
    {
        string path = RetailGolden.File("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")!;
        int reads = 0;
        Assert.Throws<InvalidDataException>(() => ReplaySoundProfile.Load(path, name =>
        {
            reads++;
            Assert.Equal(@"data\sounds\sounds_english_pc.xap", name);
            return (name, [1, 2, 3]);
        }));
        Assert.Equal(1, reads);
    }

    [RetailGoldenFact("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void ChangedOptionsCannotUseAdmittedPool()
    {
        string path = RetailGolden.File("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")!;
        int optionsReads = 0;
        var error = Assert.Throws<InvalidDataException>(() => ReplaySoundProfile.Load(path, name =>
        {
            if (name == "defaultoptions.bea")
            {
                optionsReads++;
                return (name, [1, 2, 3]);
            }
            return GameData.Read(name);
        }));
        Assert.Equal(1, optionsReads);
        Assert.Contains("startup options", error.Message);
    }
}
