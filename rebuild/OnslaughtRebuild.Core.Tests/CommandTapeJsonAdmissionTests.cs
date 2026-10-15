// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>Wire admission only; these synthetic tapes do not establish gameplay parity.</summary>
public sealed class CommandTapeJsonAdmissionTests
{
    [Theory]
    [InlineData("schemaVersion", "\"onslaught-rebuild-command-tape.v5\"")]
    [InlineData("name", "\"different\"")]
    [InlineData("seed", "2")]
    [InlineData("durationTicks", "3")]
    [InlineData("expectedFinalStateHash", "null")]
    [InlineData("expectedTraceHash", "null")]
    [InlineData("spans", "[]")]
    public void DuplicateRootMembersCannotReplaceInputOrVerification(string name, string value)
    {
        string json = Document(extra: $", \"{name}\": {value}");
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(json));
        Assert.Contains($"Duplicate command tape JSON member '{name}'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("startTick", "1")]
    [InlineData("durationTicks", "1")]
    [InlineData("moveX", "1")]
    [InlineData("moveZ", "-1")]
    [InlineData("fire", "true")]
    [InlineData("chargeWeapon", "true")]
    [InlineData("zoomIn", "true")]
    public void DuplicateSpanMembersCannotChangeTheConsumedCommand(string name, string value)
    {
        string spans = $$"""
            [{"startTick":0,"durationTicks":1,"moveX":0,"moveZ":0,
              "fire":false,"chargeWeapon":false,"zoomIn":false,"{{name}}":{{value}}}]
            """;
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(Document(spans)));
        Assert.Contains($"Duplicate command tape JSON member '{name}'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapedRootNameCannotEvadeDuplicateDetection()
    {
        const string extra = """, "\u0073eed": 2""";
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(Document(extra: extra)));
        Assert.Contains("member 'seed'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapedSpanNameCannotEvadeDuplicateDetection()
    {
        const string spans = """
            [{"startTick":0,"durationTicks":1,"moveX":0,"moveZ":0,"fire":false,"f\u0069re":true}]
            """;
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(Document(spans)));
        Assert.Contains("member 'fire'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("chargeWeapon")]
    [InlineData("zoomIn")]
    [InlineData("zoomOut")]
    public void DuplicateSpansCannotHideV5FieldsDuringV4Migration(string member)
    {
        string spans = $$"""
            [{"startTick":0,"durationTicks":1,"moveX":0,"moveZ":0,"{{member}}":true}]
            """;
        string json = Document(spans, ", \"spans\": []", CommandTape.PreviousUpgradableSchemaVersion);
        Assert.Throws<InvalidDataException>(() => CommandTapeCodec.Deserialize(json));
    }

    [Theory]
    [InlineData("chargeWeapon")]
    [InlineData("zoomIn")]
    [InlineData("zoomOut")]
    public void UniqueV5FieldsRemainRefusedInV4EvenWhenFalse(string member)
    {
        string spans = $$"""
            [{"startTick":0,"durationTicks":1,"moveX":0,"moveZ":0,"{{member}}":false}]
            """;
        string json = Document(spans, schema: CommandTape.PreviousUpgradableSchemaVersion);
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(json));
        Assert.Contains("v5-only", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateSchemaCannotHideAnUnsupportedVersion()
    {
        string json = Document(extra: $", \"schemaVersion\": \"{CommandTape.CurrentSchemaVersion}\"",
            schema: "onslaught-rebuild-command-tape.v3");
        Assert.Throws<InvalidDataException>(() => CommandTapeCodec.Deserialize(json));
    }

    [Fact]
    public void CaseVariantRemainsAnUnknownMemberRatherThanAnAlias()
    {
        Assert.Throws<JsonException>(() => CommandTapeCodec.Deserialize(Document(extra: ", \"Seed\": 2")));
    }

    [Theory]
    [InlineData(CommandTape.CurrentSchemaVersion)]
    [InlineData(CommandTape.PreviousUpgradableSchemaVersion)]
    public void NamesAreUniquePerObjectAndValidTapesKeepTheirCanonicalIdentity(string schema)
    {
        const string spans = """
            [{"startTick":0,"durationTicks":1,"moveX":1,"moveZ":0},
             {"startTick":1,"durationTicks":1,"moveX":0,"moveZ":-1,"fire":true}]
            """;
        CommandTape actual = CommandTapeCodec.Deserialize(Document(spans, schema: schema));
        var expected = new CommandTape(CommandTape.CurrentSchemaVersion, "json-admission", 1, 2,
            new string('a', 64), new string('b', 64),
            [new CommandSpan(0, 1, 1, 0), new CommandSpan(1, 1, 0, -1, Fire: true)]);

        Assert.Equal(CommandTapeCodec.Serialize(expected), CommandTapeCodec.Serialize(actual));
        Assert.Equal(CommandTape.IdentityOf(expected), CommandTape.IdentityOf(actual));
    }

    [Fact]
    public void EscapedUniqueNameKeepsTheSameMeaning()
    {
        string json = Document().Replace("\"seed\"", "\"\\u0073eed\"", StringComparison.Ordinal);
        CommandTape actual = CommandTapeCodec.Deserialize(json);
        Assert.Equal(1u, actual.Seed);
        Assert.Equal(CommandTape.IdentityOf(CommandTapeCodec.Deserialize(Document())), CommandTape.IdentityOf(actual));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    public void InvalidDocumentShapesRemainRefused(string json)
    {
        Exception? error = Record.Exception(() => CommandTapeCodec.Deserialize(json));
        Assert.True(error is InvalidDataException or JsonException,
            $"Expected an admission error, got {error?.GetType().Name ?? "success"}.");
    }

    private static string Document(string spans = "[]", string extra = "",
        string schema = CommandTape.CurrentSchemaVersion) => $$"""
        {
          "schemaVersion": "{{schema}}",
          "name": "json-admission",
          "seed": 1,
          "durationTicks": 2,
          "expectedFinalStateHash": "{{new string('a', 64)}}",
          "expectedTraceHash": "{{new string('b', 64)}}",
          "spans": {{spans}}{{extra}}
        }
        """;
}
