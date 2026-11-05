using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Portunus.Core;

/// <summary>
/// The buffer a wire format is written into (<c>06</c> PRT-ENG-005, PRT-ENG-006): pinned
/// arrays only, grown up to a bound, each array it leaves zeroed. What goes beyond the bound is
/// not kept but counted, so a writer learns that its document is over its bound, and by how
/// much, without the document's bytes ever reaching an array that is not pinned.
/// </summary>
internal sealed class PinnedBufferWriter : IBufferWriter<byte>, IDisposable
{
    private const int FirstCapacity = 1024;

    private readonly int _bound;

    private byte[] _buffer;

    private byte[] _beyond = [];

    private bool _writingBeyond;

    private int _written;

    internal PinnedBufferWriter(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        _bound = bound;
        _buffer = GC.AllocateArray<byte>(Math.Min(FirstCapacity, bound), pinned: true);
    }

    /// <summary>Whether what was written passed the bound.</summary>
    internal bool IsOverBound => Length > _bound;

    /// <summary>The length of what was written, counted beyond the bound too.</summary>
    internal long Length { get; private set; }

    /// <summary>Moves what was written into a document of its own, and leaves this writer empty.</summary>
    /// <exception cref="InvalidOperationException">What was written passed the bound.</exception>
    internal PinnedDocument Detach()
    {
        if (IsOverBound)
        {
            throw new InvalidOperationException("A document over its bound is not kept.");
        }

        PinnedDocument document = new(_buffer, _written);
        _buffer = [];
        _written = 0;
        Length = 0;
        return document;
    }

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (_writingBeyond)
        {
            if (!IsOverBound && Length + count <= _bound)
            {
                Reserve(count);
                _beyond.AsSpan(0, count).CopyTo(_buffer.AsSpan(_written));
                _written += count;
            }

            CryptographicOperations.ZeroMemory(_beyond.AsSpan(0, count));
            _writingBeyond = false;
        }
        else
        {
            _written += count;
        }

        Length += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        int needed = Math.Max(sizeHint, 1);
        if (!IsOverBound && _written + needed <= _bound)
        {
            Reserve(needed);
            _writingBeyond = false;
            return _buffer.AsMemory(_written);
        }

        // Near or past the bound, a writer may ask for more than it then writes, so what it
        // writes goes to a scratch array first and is kept only where it falls within the bound.
        if (_beyond.Length < needed)
        {
            CryptographicOperations.ZeroMemory(_beyond);
            _beyond = GC.AllocateArray<byte>(needed, pinned: true);
        }

        _writingBeyond = true;
        return _beyond;
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return GetMemory(sizeHint).Span;
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_buffer);
        CryptographicOperations.ZeroMemory(_beyond);
        _buffer = [];
        _beyond = [];
    }

    private void Reserve(int needed)
    {
        if (_written + needed <= _buffer.Length)
        {
            return;
        }

        int capacity = Math.Min(_bound, Math.Max(_buffer.Length * 2, _written + needed));
        byte[] grown = GC.AllocateArray<byte>(capacity, pinned: true);
        _buffer.AsSpan(0, _written).CopyTo(grown);
        CryptographicOperations.ZeroMemory(_buffer);
        _buffer = grown;
    }
}