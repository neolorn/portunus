using System.Text;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class PushAnswerFormatTests
{
    [Fact]
    public void PRT_ENG_006_AC1_PushAnswerIsWrittenInItsForm()
    {
        using PinnedDocument accepted = PushAnswerFormat.WriteAccepted(4);
        using PinnedDocument refused = PushAnswerFormat.WriteRefused(new Failure { Code = "secrets.push.ready" });

        Assert.Equal("""{"version":1,"accepted":true,"items":4}""", Encoding.UTF8.GetString(accepted.Span));
        Assert.Equal("""{"version":1,"accepted":false,"failure":{"code":"secrets.push.ready","details":{}}}""", Encoding.UTF8.GetString(refused.Span));
    }

    [Fact]
    public void Read_EachForm_AnswersIt()
    {
        Assert.Equal(new PushAnswer.Accepted(4), Read("""{"version":1,"accepted":true,"items":4}""").Value);
        PushAnswer.Refused refused = Assert.IsType<PushAnswer.Refused>(Read("""{"accepted":false,"version":1,"failure":{"code":"secrets.push.wrongvault","details":{}},"later":1}""").Value);
        Assert.Equal("secrets.push.wrongvault", refused.Failure.Code);
    }

    [Fact]
    public void Read_AnotherProtocolVersion_AnswersTheVersionFailure()
    {
        Failure failure = Read("""{"version":2,"accepted":true,"items":4}""").Failure!;

        Assert.Equal(Codes.RequestVersion, failure.Code);
        Assert.Equal("2", failure.Details["version"]);
    }

    [Theory]
    [InlineData("""{"accepted":true,"items":4}""")]
    [InlineData("""{"version":1,"accepted":true}""")]
    [InlineData("""{"version":1,"accepted":true,"items":4,"failure":{"code":"secrets.push.ready","details":{}}}""")]
    [InlineData("""{"version":1,"accepted":false,"items":4}""")]
    [InlineData("""{"version":1,"accepted":"yes","items":4}""")]
    [InlineData("""{"version":1,"accepted":false}""")]
    [InlineData("[1]")]
    public void Read_DocumentOutsideBothForms_IsInvalid(string document)
    {
        Assert.Equal(Codes.RequestInvalid, Read(document).Failure!.Code);
    }

    private static Result<PushAnswer> Read(string document)
    {
        return PushAnswerFormat.Read(Encoding.UTF8.GetBytes(document));
    }
}