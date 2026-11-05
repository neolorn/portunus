using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Portunus.Agent.Fetch;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>The token's reading from its file, less one final line ending (<c>01</c> PRT-AGT-002, PRT-AGT-003; P-031).</summary>
[Trait("Category", "unit")]
public sealed class TokenFileTests : IDisposable
{
    private readonly AgentFiles _files = AgentFiles.Scratch();

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Read_FinalLineEnding_IsLeftOut(string ending)
    {
        byte[] token = RandomToken();
        string file = Write([.. token, .. Encoding.ASCII.GetBytes(ending)]);

        byte[]? read = TokenFile.Read(file);

        Assert.NotNull(read);
        Assert.True(read.AsSpan().SequenceEqual(token));
    }

    [Theory]
    [InlineData("\n\n", 1)]
    [InlineData("\r\n\r\n", 2)]
    [InlineData("\r", 1)]
    [InlineData(" ", 1)]
    public void Read_AnyOtherByte_Stays(string ending, int kept)
    {
        byte[] token = RandomToken();
        byte[] trailing = Encoding.ASCII.GetBytes(ending);
        string file = Write([.. token, .. trailing]);

        byte[]? read = TokenFile.Read(file);

        Assert.NotNull(read);
        Assert.True(read.AsSpan().SequenceEqual([.. token, .. trailing.AsSpan(0, kept)]));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("")]
    public void Read_LineEndingAloneOrNothing_IsUnavailable(string content)
    {
        string file = Write(Encoding.ASCII.GetBytes(content));

        Assert.Null(TokenFile.Read(file));
    }

    [Fact]
    public void Read_OverFourKibibytes_IsUnavailable()
    {
        string file = Write(RandomNumberGenerator.GetBytes(TokenFile.MostBytes + 1));

        Assert.Null(TokenFile.Read(file));
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    /// <summary>A token made at run time, printable bytes alone, so no test holds a credential-shaped literal (<c>06</c> PRT-ENG-001).</summary>
    private static byte[] RandomToken()
    {
        byte[] token = new byte[64];
        for (int index = 0; index < token.Length; index++)
        {
            token[index] = (byte)RandomNumberGenerator.GetInt32('a', 'z' + 1);
        }

        return token;
    }

    private string Write(byte[] content)
    {
        string file = Path.Combine(_files.Folder, "token");
        File.WriteAllBytes(file, content);
        return file;
    }
}