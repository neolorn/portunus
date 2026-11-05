using System.Linq;
using System.Text;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class DevelopmentFileTests
{
    [Fact]
    public void Read_FileOfItsForm_AnswersTheVaultItsItemsAndTheirValues()
    {
        VaultSet set = Read("""{"vault":"Sample Development","items":{"Themis":{"connection":"Host=db"},"Janus":{"keys.1":"a","keys.current":"1"}}}""", out PinnedValues values)!;
        using (values)
        {
            Assert.Equal("Sample Development", set.Vault);
            Assert.Equal(["Themis", "Janus"], set.Items.Select(item => item.Title));
            Assert.Equal(["keys.1", "keys.current"], set.Items[1].Fields.Select(field => field.Label));
            Assert.Equal("Host=db", Encoding.UTF8.GetString(set.Items[0].Fields[0].Value.Span));
            Assert.All(set.Items, item => Assert.False(item.HasNote));
        }
    }

    [Fact]
    public void Read_EscapedValue_AnswersItsText()
    {
        VaultSet set = Read("""{"vault":"Sample Development","items":{"Themis":{"connection":"pass\"word"}}}""", out PinnedValues values)!;
        using (values)
        {
            Assert.Equal("pass\"word", Encoding.UTF8.GetString(set.Items[0].Fields[0].Value.Span));
        }
    }

    [Fact]
    public void Read_MembersItDoesNotKnow_AnswersTheSet()
    {
        VaultSet set = Read("""{"items":{"Themis":{"connection":"Host=db"}},"later":[1],"vault":"Sample Development"}""", out PinnedValues values)!;
        using (values)
        {
            Assert.Single(set.Items);
        }
    }

    [Fact]
    public void Read_ItemTwice_AnswersBothForTheLayoutsCheck()
    {
        // Two members of one name are the layout's duplicate, which its check names.
        VaultSet set = Read("""{"vault":"Sample Development","items":{"Themis":{"connection":"a"},"Themis":{"connection":"b"}}}""", out PinnedValues values)!;
        using (values)
        {
            Assert.Equal(2, set.Items.Count);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"items":{}}""")]
    [InlineData("""{"vault":"Sample Development"}""")]
    [InlineData("""{"vault":7,"items":{}}""")]
    [InlineData("""{"vault":"Sample Development","items":[]}""")]
    [InlineData("""{"vault":"Sample Development","items":{"Themis":"connection"}}""")]
    [InlineData("""{"vault":"Sample Development","items":{"Themis":{"connection":1}}}""")]
    [InlineData("""{"vault":"Sample Development","items":{"Themis":{"connection":{"value":"a"}}}}""")]
    [InlineData("""{"vault":"Sample Development","vault":"Sample Development","items":{}}""")]
    [InlineData("""{"vault":"Sample Development","items":{}} {}""")]
    [InlineData("""{"vault":"Sample Development","items":{},}""")]
    public void Read_DocumentOutsideItsForm_AnswersNone(string document)
    {
        Assert.Null(Read(document, out PinnedValues values));
        values.Dispose();
    }

    private static VaultSet? Read(string document, out PinnedValues values)
    {
        return DevelopmentFile.Read(Encoding.UTF8.GetBytes(document), out values);
    }
}