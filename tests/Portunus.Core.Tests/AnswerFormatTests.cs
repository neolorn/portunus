using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class AnswerFormatTests
{
    private static readonly DateTimeOffset Since = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset LoadedAt = new(2026, 11, 5, 9, 0, 12, TimeSpan.Zero);

    [Fact]
    public void PRT_ENG_006_AC1_AnswerWhileWaitingIsWrittenInItsForm()
    {
        using PinnedDocument document = AnswerFormat.WriteWaiting(WaitReason.RateLimited, Since);

        Assert.Equal("""{"version":1,"state":"waiting","reason":"ratelimited","since":"2026-11-05T09:00:00Z"}""", Text(document));
    }

    [Fact]
    public void PRT_ENG_006_AC1_StatusWhileReadyIsWrittenInItsForm()
    {
        using PinnedDocument document = AnswerFormat.WriteReady(Since, SetSource.Vault, LoadedAt, 4);

        Assert.Equal("""{"version":1,"state":"ready","since":"2026-11-05T09:00:00Z","source":"vault","loadedAt":"2026-11-05T09:00:12Z","items":4}""", Text(document));
    }

    [Fact]
    public void PRT_ENG_006_AC1_SecretIsWrittenInItsForm()
    {
        Dictionary<int, ReadOnlyMemory<byte>> versions = new() { [2] = "Host=db"u8.ToArray(), [1] = new byte[] { 0xFB, 0xFF, 0x00, 0x01 } };

        using PinnedDocument document = AnswerFormat.WriteSecret(2, versions);

        Assert.Equal("""{"version":1,"state":"ready","secret":{"current":2,"versions":{"1":"-_8AAQ","2":"SG9zdD1kYg"}}}""", Text(document));
    }

    [Fact]
    public void PRT_ENG_006_AC1_FailureIsWrittenInItsForm()
    {
        using PinnedDocument denied = AnswerFormat.WriteFailure(AgentState.Ready, new Failure { Code = "secrets.secret.denied" });
        using PinnedDocument version = AnswerFormat.WriteFailure(AgentState.Waiting, WireJson.OtherVersion(1));

        Assert.Equal("""{"version":1,"state":"ready","failure":{"code":"secrets.secret.denied","details":{}}}""", Text(denied));
        Assert.Equal("""{"version":1,"state":"waiting","failure":{"code":"secrets.request.version","details":{"version":"1"}}}""", Text(version));
    }

    [Fact]
    public void WriteFailure_SeveralDetails_WritesThemInOrdinalOrder()
    {
        Failure failure = new()
        {
            Code = "secrets.push.layoutinvalid",
            Details = new Dictionary<string, string> { ["item"] = "Janus", ["field"] = "keys.current" }.ToFrozenDictionary(StringComparer.Ordinal),
        };

        using PinnedDocument document = AnswerFormat.WriteFailure(AgentState.Ready, failure);

        Assert.Equal("""{"version":1,"state":"ready","failure":{"code":"secrets.push.layoutinvalid","details":{"field":"keys.current","item":"Janus"}}}""", Text(document));
    }

    [Fact]
    public void WriteWaiting_InstantAtAnotherOffsetWithAFraction_IsWrittenAtOffsetZeroToTheSecond()
    {
        using PinnedDocument document = AnswerFormat.WriteWaiting(WaitReason.Fetching, new DateTimeOffset(2026, 11, 5, 11, 0, 0, 750, TimeSpan.FromHours(2)));

        Assert.Equal("""{"version":1,"state":"waiting","reason":"fetching","since":"2026-11-05T09:00:00Z"}""", Text(document));
    }

    [Theory]
    [InlineData(WaitReason.Fetching, "fetching")]
    [InlineData(WaitReason.Unreachable, "unreachable")]
    [InlineData(WaitReason.RateLimited, "ratelimited")]
    [InlineData(WaitReason.Refused, "refused")]
    [InlineData(WaitReason.LayoutInvalid, "layoutinvalid")]
    [InlineData(WaitReason.AwaitingPush, "awaitingpush")]
    public void WriteWaiting_EachReason_IsWrittenByItsSpellingAndReadBack(WaitReason reason, string spelling)
    {
        using PinnedDocument document = AnswerFormat.WriteWaiting(reason, Since);

        Assert.Contains($"\"reason\":\"{spelling}\"", Text(document), StringComparison.Ordinal);
        Assert.Equal(new WireAnswer.Waiting(reason, Since), AnswerFormat.Read(document.Span).Value);
    }

    [Fact]
    public void Read_StatusWhileReady_AnswersIt()
    {
        Result<WireAnswer> answer = Read("""{"version":1,"state":"ready","since":"2026-11-05T09:00:00Z","source":"push","loadedAt":"2026-11-05T09:00:12Z","items":4}""");

        Assert.Equal(new WireAnswer.Ready(Since, SetSource.Push, LoadedAt, 4), answer.Value);
    }

    [Fact]
    public void Read_Secret_AnswersItsVersionsAndValues()
    {
        Result<WireAnswer> answer = Read("""{"version":1,"state":"ready","secret":{"versions":{"2":"SG9zdD1kYg","1":"-_8AAQ"},"current":2,"later":true}}""");

        WireAnswer.Value secret = Assert.IsType<WireAnswer.Value>(answer.Value);
        using (secret.Values)
        {
            Assert.Equal(2, secret.Current);
            Assert.Equal("Host=db"u8.ToArray(), secret.Versions[2].ToArray());
            Assert.Equal(new byte[] { 0xFB, 0xFF, 0x00, 0x01 }, secret.Versions[1].ToArray());
            Assert.Equal("Value { Current = 2, Versions = [1, 2] }", secret.ToString());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void WriteSecret_ValueOfEachLength_IsWrittenUnpaddedAndReadBack(int length)
    {
        byte[] value = new byte[length];
        Array.Fill(value, (byte)0xFF);

        using PinnedDocument document = AnswerFormat.WriteSecret(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = value });
        WireAnswer.Value secret = Assert.IsType<WireAnswer.Value>(AnswerFormat.Read(document.Span).Value);
        using (secret.Values)
        {
            Assert.DoesNotContain("=", Text(document), StringComparison.Ordinal);
            Assert.Equal(value, secret.Versions[1].ToArray());
        }
    }

    [Fact]
    public void Read_Failure_AnswersItsStateAndFailure()
    {
        Result<WireAnswer> answer = Read("""{"version":1,"state":"ready","failure":{"code":"secrets.secret.unknown","details":{}}}""");

        WireAnswer.Refused refused = Assert.IsType<WireAnswer.Refused>(answer.Value);
        Assert.Equal(AgentState.Ready, refused.State);
        Assert.Equal("secrets.secret.unknown", refused.Failure.Code);
        Assert.Empty(refused.Failure.Details);
    }

    [Fact]
    public void Read_AnotherProtocolVersion_AnswersTheVersionFailureNamingTheAgents()
    {
        Failure failure = Read("""{"version":3,"state":"ready","whatever":[]}""").Failure!;

        Assert.Equal(Codes.RequestVersion, failure.Code);
        Assert.Equal("3", failure.Details["version"]);
    }

    [Theory]
    [InlineData("""{"state":"waiting","reason":"fetching","since":"2026-11-05T09:00:00Z"}""")]
    [InlineData("""{"version":1,"state":"asleep","reason":"fetching","since":"2026-11-05T09:00:00Z"}""")]
    [InlineData("""{"version":1,"state":"waiting","reason":"tired","since":"2026-11-05T09:00:00Z"}""")]
    [InlineData("""{"version":1,"state":"waiting","reason":"fetching"}""")]
    [InlineData("""{"version":1,"state":"waiting","reason":"fetching","since":"2026-11-05T09:00:00+02:00"}""")]
    [InlineData("""{"version":1,"state":"waiting","reason":"fetching","since":"2026-11-05T09:00:00.5Z"}""")]
    [InlineData("""{"version":1,"state":"waiting","reason":"fetching","since":"2026-11-05T09:00:00Z","items":4}""")]
    [InlineData("""{"version":1,"state":"ready","since":"2026-11-05T09:00:00Z","source":"vault","loadedAt":"2026-11-05T09:00:12Z"}""")]
    [InlineData("""{"version":1,"state":"ready","since":"2026-11-05T09:00:00Z","source":"vault","loadedAt":"2026-11-05T09:00:12Z","items":-1}""")]
    [InlineData("""{"version":1,"state":"ready","since":"2026-11-05T09:00:00Z","source":"disk","loadedAt":"2026-11-05T09:00:12Z","items":4}""")]
    [InlineData("""{"version":1,"state":"ready","secret":{"current":3,"versions":{"1":"MQ","2":"Mg"}}}""")]
    [InlineData("""{"version":1,"state":"ready","secret":{"current":1,"versions":{}}}""")]
    [InlineData("""{"version":1,"state":"ready","secret":{"current":1,"versions":{"01":"MQ"}}}""")]
    [InlineData("""{"version":1,"state":"ready","secret":{"current":1,"versions":{"1":"MQ=="}}}""")]
    [InlineData("""{"version":1,"state":"ready","secret":{"current":1,"versions":{"1":"M\u0051"}}}""")]
    [InlineData("""{"version":1,"state":"ready","secret":{"current":1,"versions":{"1":"MR"}}}""")]
    [InlineData("""{"version":1,"state":"waiting","secret":{"current":1,"versions":{"1":"MQ"}}}""")]
    [InlineData("""{"version":1,"state":"ready","failure":{"code":"secrets.secret.unknown"}}""")]
    [InlineData("""{"version":1,"state":"ready","failure":{"code":"","details":{}}}""")]
    [InlineData("""{"version":1,"state":"ready","failure":{"code":"secrets.secret.unknown","details":{"item":1}}}""")]
    [InlineData("""{"version":1,"state":"ready","failure":{"code":"secrets.secret.unknown","details":{}},"items":4}""")]
    [InlineData("""{"version":1,"state":"ready","state":"ready","failure":{"code":"secrets.secret.unknown","details":{}}}""")]
    [InlineData("not json")]
    public void Read_DocumentOutsideEveryForm_IsInvalid(string document)
    {
        Assert.Equal(Codes.RequestInvalid, Read(document).Failure!.Code);
    }

    [Fact]
    public void Read_DocumentOverTwoMib_IsInvalid()
    {
        string document = """{"version":1,"state":"waiting","reason":"fetching","since":"2026-11-05T09:00:00Z"}""";

        Assert.Equal(Codes.RequestInvalid, Read(document + new string(' ', Limits.AnswerBytes)).Failure!.Code);
    }

    private static Result<WireAnswer> Read(string document)
    {
        return AnswerFormat.Read(Encoding.UTF8.GetBytes(document));
    }

    private static string Text(PinnedDocument document)
    {
        return Encoding.UTF8.GetString(document.Span);
    }
}