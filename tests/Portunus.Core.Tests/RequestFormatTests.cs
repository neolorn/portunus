using System;
using System.Linq;
using System.Text;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class RequestFormatTests
{
    [Fact]
    public void PRT_ENG_006_AC1_ReadRequestIsWrittenInItsForm()
    {
        using PinnedDocument document = RequestFormat.WriteRead(SecretAddress.Parse("Themis/connection"));

        Assert.Equal("""{"version":1,"kind":"read","address":"Themis/connection"}""", Text(document));
    }

    [Fact]
    public void PRT_ENG_006_AC1_StatusRequestIsWrittenInItsForm()
    {
        using PinnedDocument document = RequestFormat.WriteStatus();

        Assert.Equal("""{"version":1,"kind":"status"}""", Text(document));
    }

    [Fact]
    public void PRT_ENG_006_AC1_PushRequestIsWrittenInItsForm()
    {
        VaultSet set = new("Sample Production",
        [
            new VaultItem("Themis", HasNote: true, [Sets.Field("connection", "Host=db")]),
            new VaultItem("Janus", HasNote: false, [new VaultField("keys.1", new byte[] { 0xFB, 0xFF, 0x00, 0x01 }), Sets.Field("keys.current", "1")]),
        ]);

        Assert.True(RequestFormat.TryWritePush(set, replace: false, out PinnedDocument? document));
        using (document)
        {
            Assert.Equal(
                """{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Themis","fields":[{"label":"connection","value":"SG9zdD1kYg"}]},{"title":"Janus","fields":[{"label":"keys.1","value":"-_8AAQ"},{"label":"keys.current","value":"MQ"}]}]}""",
                Text(document));
        }
    }

    [Fact]
    public void TryWritePush_PayloadOverTwoMib_AnswersFalse()
    {
        // Thirty-two values of 48 KiB are 2 MiB in base64url, so the payload passes its bound.
        VaultSet set = new("Sample Production", [new VaultItem("Themis", HasNote: false, [.. Enumerable.Range(0, 32).Select(index => new VaultField($"field{index}", new byte[48 * 1024]))])]);

        bool written = RequestFormat.TryWritePush(set, replace: true, out PinnedDocument? document);
        using (document)
        {
            Assert.False(written);
            Assert.Null(document);
        }
    }

    [Fact]
    public void Read_ReadRequest_AnswersItsAddress()
    {
        Result<WireRequest> request = Read("""{"version":1,"kind":"read","address":"Themis/connection"}""");

        Assert.Equal(new WireRequest.Read(SecretAddress.Parse("Themis/connection")), request.Value);
    }

    [Fact]
    public void Read_MembersInAnotherOrderAndOnesItDoesNotKnow_AnswersTheRequest()
    {
        Result<WireRequest> request = Read("""{"later":{"nested":[1,2]},"address":"Themis/connection","kind":"read","version":1}""");

        Assert.Equal(new WireRequest.Read(SecretAddress.Parse("Themis/connection")), request.Value);
    }

    [Fact]
    public void Read_StatusRequest_AnswersIt()
    {
        Assert.IsType<WireRequest.Status>(Read("""{"version":1,"kind":"status"}""").Value);
    }

    [Fact]
    public void Read_PushRequest_AnswersItsSetAndValues()
    {
        Result<WireRequest> request = Read(
            """{"version":1,"kind":"push","vault":"Sample Production","replace":true,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-_8AAQ"},{"label":"keys.current","value":"MQ"}]}]}""",
            Limits.AnswerBytes);

        WireRequest.Push push = Assert.IsType<WireRequest.Push>(request.Value);
        using (push.Values)
        {
            Assert.Equal("Sample Production", push.Set.Vault);
            Assert.True(push.Replace);
            VaultItem item = Assert.Single(push.Set.Items);
            Assert.Equal("Janus", item.Title);
            Assert.False(item.HasNote);
            Assert.Equal(["keys.1", "keys.current"], item.Fields.Select(field => field.Label));
            Assert.Equal(new byte[] { 0xFB, 0xFF, 0x00, 0x01 }, item.Fields[0].Value.ToArray());
            Assert.Equal("1"u8.ToArray(), item.Fields[1].Value.ToArray());
        }
    }

    [Fact]
    public void Read_PushRequestWithAnEscapedValue_IsInvalid()
    {
        // The escape stands for an underscore, so the text is base64url once unescaped (P-023).
        Result<WireRequest> request = Read(
            """{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-\u005f8AAQ"}]}]}""",
            Limits.AnswerBytes);

        Assert.Equal(Codes.RequestInvalid, request.Failure!.Code);
    }

    [Theory]
    [InlineData("""{"version":2,"kind":"read","address":"Themis/connection"}""")]
    [InlineData("""{"version":0}""")]
    [InlineData("""{"kind":"anything","version":7,"address":5}""")]
    public void Read_AnotherProtocolVersion_AnswersTheVersionFailureNamingThisOne(string document)
    {
        Failure failure = Read(document).Failure!;

        Assert.Equal(Codes.RequestVersion, failure.Code);
        Assert.Equal("1", failure.Details["version"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"kind":"read","address":"Themis/connection"}""")]
    [InlineData("""{"version":"1","kind":"read","address":"Themis/connection"}""")]
    [InlineData("""{"version":1.0,"kind":"read","address":"Themis/connection"}""")]
    [InlineData("""{"version":1,"kind":"read"}""")]
    [InlineData("""{"version":1,"kind":"read","address":"Themis/keys.current"}""")]
    [InlineData("""{"version":1,"kind":"read","address":"Themis"}""")]
    [InlineData("""{"version":1,"kind":"read","address":{"item":"Themis"}}""")]
    [InlineData("""{"version":1,"kind":"read","address":"Themis/connection","address":"Themis/connection"}""")]
    [InlineData("""{"version":1,"kind":"status","address":"Themis/connection"}""")]
    [InlineData("""{"version":1,"kind":"write"}""")]
    [InlineData("""{"version":1,"kind":{"name":"status"}}""")]
    [InlineData("""{"version":1,"kind":"status"} {}""")]
    [InlineData("""{"version":1,"kind":"status",}""")]
    [InlineData("""{"version":1,"kind":"status" /* comment */}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":"no","items":[]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus"}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1"}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-_8AAQ="}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-_8AAQ=="}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"+/8AAQ"}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-_8A AQ"}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-_8=AQ"}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"-_8AA"}]}]}""")]
    [InlineData("""{"version":1,"kind":"push","vault":"Sample Production","replace":false,"items":[{"title":"Janus","fields":[{"label":"keys.1","value":"MR"}]}]}""")]
    public void Read_DocumentOutsideItsForm_IsInvalid(string document)
    {
        Assert.Equal(Codes.RequestInvalid, Read(document, Limits.AnswerBytes).Failure!.Code);
    }

    [Fact]
    public void Read_DocumentOverItsBound_IsInvalid()
    {
        string padding = new(' ', Limits.RequestBytes);
        string document = """{"version":1,"kind":"status"}""";

        Assert.IsType<WireRequest.Status>(Read(document + padding[..(Limits.RequestBytes - document.Length)]).Value);
        Assert.Equal(Codes.RequestInvalid, Read(document + padding[..(Limits.RequestBytes - document.Length + 1)]).Failure!.Code);
    }

    private static Result<WireRequest> Read(string document, int bound = Limits.RequestBytes)
    {
        return RequestFormat.Read(Encoding.UTF8.GetBytes(document), bound);
    }

    private static string Text(PinnedDocument? document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Encoding.UTF8.GetString(document.Span);
    }
}