using System;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class SecretAddressTests
{
    [Fact]
    public void Parse_AddressOfTheRule_AnswersItsParts()
    {
        SecretAddress address = SecretAddress.Parse("Themis/connection");

        Assert.Equal("Themis", address.Item);
        Assert.Equal("connection", address.Name);
        Assert.Equal("Themis/connection", address.ToString());
    }

    [Theory]
    [InlineData("Example Provider/socialProvider.example")]
    [InlineData("Janus/keyEncryptionKeys")]
    public void Parse_AddressOfTheRule_IsWrittenAsItWasRead(string text)
    {
        Assert.Equal(text, SecretAddress.Parse(text).ToString());
    }

    [Theory]
    [InlineData("Themis")]
    [InlineData("Themis/")]
    [InlineData("/connection")]
    [InlineData("Themis/connection/x")]
    [InlineData("Themis//connection")]
    [InlineData("1Themis/connection")]
    [InlineData("Themis /connection")]
    [InlineData("Themis/connection.")]
    [InlineData("Themis/keys.1")]
    [InlineData("Themis/keys.current")]
    [InlineData("Themis/keys.1.retired")]
    [InlineData("Thémis/connection")]
    [InlineData("")]
    public void Parse_AddressOutsideTheRule_Throws(string text)
    {
        Assert.Throws<ArgumentException>(() => SecretAddress.Parse(text));
        Assert.False(SecretAddress.TryParse(text, out _));
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SecretAddress.Parse(null!));
        Assert.False(SecretAddress.TryParse(null, out _));
    }

    [Theory]
    [InlineData("Themis ", "connection")]
    [InlineData("Themis", "connection key")]
    [InlineData("", "connection")]
    public void Constructor_PartOutsideItsRule_Throws(string item, string name)
    {
        Assert.Throws<ArgumentException>(() => new SecretAddress(item, name));
    }

    [Fact]
    public void With_PartOutsideItsRule_Throws()
    {
        SecretAddress address = SecretAddress.Parse("Themis/connection");

        Assert.Throws<ArgumentException>(() => address with { Name = "keys.current" });
        Assert.Throws<ArgumentException>(() => address with { Item = "Themis/x" });
    }

    [Fact]
    public void Equals_PartsAlikeButForCase_IsTrue()
    {
        SecretAddress address = SecretAddress.Parse("Themis/connection");
        SecretAddress other = SecretAddress.Parse("THEMIS/Connection");

        Assert.Equal(address, other);
        Assert.Equal(address.GetHashCode(), other.GetHashCode());
        Assert.NotEqual(address, SecretAddress.Parse("Themis/listenConnection"));
    }

    [Fact]
    public void TryParse_AddressOfTheRule_AnswersIt()
    {
        Assert.True(SecretAddress.TryParse("Themis/connection", out SecretAddress address));
        Assert.Equal(SecretAddress.Parse("Themis/connection"), address);
    }
}