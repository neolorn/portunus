using Xunit;

namespace Portunus.Core.Tests;

/// <remarks>
/// The names of <c>00</c> section 1.5: an instance's or a process's name, and a vault title,
/// an item title and a secret name, which the layout's tests also break.
/// </remarks>
[Trait("Category", "unit")]
public sealed class NamesTests
{
    [Theory]
    [InlineData("sample-production")]
    [InlineData("a")]
    [InlineData("web2")]
    [InlineData("a-1-b")]
    public void IsName_NameOfTheRule_IsTrue(string name)
    {
        Assert.True(Names.IsName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sample-production")]
    [InlineData("1web")]
    [InlineData("-web")]
    [InlineData("web-")]
    [InlineData("web--api")]
    [InlineData("web_api")]
    [InlineData("web api")]
    [InlineData("wéb")]
    [InlineData("web\n")]
    [InlineData(null)]
    public void IsName_NameOutsideTheRule_IsFalse(string? name)
    {
        Assert.False(Names.IsName(name));
    }

    [Fact]
    public void IsName_SixtyFiveCharacters_IsFalse()
    {
        string name = "a" + new string('b', 63);

        Assert.True(Names.IsName(name));
        Assert.False(Names.IsName(name + "c"));
    }

    [Theory]
    [InlineData("Themis")]
    [InlineData("Example Provider")]
    [InlineData("Sms_provider-2.eu")]
    public void IsItemTitle_TitleOfTheRule_IsTrue(string title)
    {
        Assert.True(Names.IsItemTitle(title));
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("keyEncryptionKeys")]
    [InlineData("socialProvider.example")]
    [InlineData("a.b-c_d")]
    [InlineData("keys.v2")]
    public void IsSecretName_NameOfTheRule_IsTrue(string name)
    {
        Assert.True(Names.IsSecretName(name));
    }

    [Theory]
    [InlineData("keys.1")]
    [InlineData("keys.current")]
    [InlineData("keys.Current")]
    [InlineData("keys.RETIRED")]
    [InlineData("keys.")]
    [InlineData("keys\n")]
    [InlineData(null)]
    public void IsSecretName_NameEndingInAVersionsMark_IsFalse(string? name)
    {
        Assert.False(Names.IsSecretName(name));
    }
}