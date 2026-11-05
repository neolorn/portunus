using System;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class PinnedBufferWriterTests
{
    [Fact]
    public void Advance_WithinTheBound_KeepsWhatWasWritten()
    {
        using PinnedBufferWriter buffer = new(4096);
        for (int round = 0; round < 3; round++)
        {
            Span<byte> span = buffer.GetSpan(1000);
            span[..1000].Fill((byte)('a' + round));
            buffer.Advance(1000);
        }

        using PinnedDocument document = buffer.Detach();

        Assert.False(buffer.IsOverBound);
        Assert.Equal(3000, document.Length);
        Assert.Equal((byte)'a', document.Span[0]);
        Assert.Equal((byte)'c', document.Span[2999]);
    }

    [Fact]
    public void GetMemory_AskingBeyondTheBoundButWritingWithin_KeepsWhatWasWritten()
    {
        using PinnedBufferWriter buffer = new(10);
        buffer.GetSpan(8)[..8].Fill(1);
        buffer.Advance(8);

        Span<byte> span = buffer.GetSpan(100);
        span[..2].Fill(2);
        buffer.Advance(2);

        Assert.False(buffer.IsOverBound);
        using PinnedDocument document = buffer.Detach();
        Assert.Equal(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 2, 2 }, document.Span.ToArray());
    }

    [Fact]
    public void Advance_PastTheBound_CountsWhatItDoesNotKeep()
    {
        using PinnedBufferWriter buffer = new(10);
        buffer.GetSpan(8)[..8].Fill(1);
        buffer.Advance(8);
        buffer.GetSpan(3)[..3].Fill(2);
        buffer.Advance(3);
        buffer.GetSpan(5)[..5].Fill(3);
        buffer.Advance(5);

        Assert.True(buffer.IsOverBound);
        Assert.Equal(16, buffer.Length);
        Assert.Throws<InvalidOperationException>(() => buffer.Detach());
    }

    [Fact]
    public void Dispose_PinnedDocument_ZeroesItsBytes()
    {
        using PinnedBufferWriter buffer = new(16);
        buffer.GetSpan(4)[..4].Fill(9);
        buffer.Advance(4);
        PinnedDocument document = buffer.Detach();
        ReadOnlySpan<byte> before = document.Span;
        Assert.Equal(9, before[0]);

        document.Dispose();

        Assert.Equal(0, document.Length);
        Assert.Equal(new byte[4], before[..4].ToArray());
    }
}