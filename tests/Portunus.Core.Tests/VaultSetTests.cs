using System.Linq;
using System.Text;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class VaultSetTests
{
    [Fact]
    public void Find_SingleSecret_AnswersItAsVersionOne()
    {
        VaultSet set = Sets.Of(Sets.Item("Themis", ("connection", "Host=db")));

        Secret secret = set.Find(SecretAddress.Parse("Themis/connection"))!;

        Assert.Equal(1, secret.Current);
        Assert.Equal([1], secret.Versions.Keys);
        Assert.Equal("Host=db", Encoding.UTF8.GetString(secret.CurrentValue.Span));
    }

    [Fact]
    public void Find_VersionedSecret_AnswersItsLiveVersionsAndItsCurrentOne()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.1.retired", "a"), ("keys.2", "b"), ("keys.3", "c"), ("keys.current", "2")));

        Secret secret = set.Find(SecretAddress.Parse("Janus/keys"))!;

        Assert.Equal(2, secret.Current);
        Assert.Equal([2, 3], secret.Versions.Keys.Order());
        Assert.Equal("b", Encoding.UTF8.GetString(secret.CurrentValue.Span));
    }

    [Fact]
    public void Find_AddressOfAnotherCase_AnswersTheSecret()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("Keys.1", "a"), ("KEYS.CURRENT", "1")));

        Assert.NotNull(set.Find(SecretAddress.Parse("janus/keys")));
    }

    [Fact]
    public void Find_SecretHoldingOnlyRetiredVersions_AnswersNone()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.1.retired", "a")));

        Assert.Null(set.Find(SecretAddress.Parse("Janus/keys")));
    }

    [Fact]
    public void Find_AddressTheSetLacks_AnswersNone()
    {
        VaultSet set = Sets.Of(Sets.Item("Themis", ("connection", "Host=db")));

        Assert.Null(set.Find(SecretAddress.Parse("Themis/other")));
        Assert.Null(set.Find(SecretAddress.Parse("Hermes/connection")));
    }
}