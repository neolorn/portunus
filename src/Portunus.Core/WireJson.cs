using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Portunus.Core;

/// <summary>
/// What the wire formats share (<c>06</c> PRT-ENG-006): UTF-8 JSON without indentation, the
/// protocol version first, a value in base64, a failure's code and details, each written into
/// pinned arrays and read from them (PRT-ENG-005).
/// </summary>
internal static class WireJson
{
    /// <summary>The protocol version of <c>07</c> section 7.</summary>
    internal const int ProtocolVersion = 1;

    private const int MaximumDepth = 8;

    /// <summary>Writes a document with <paramref name="write"/> into a buffer of <paramref name="bound"/> bytes, which tells whether it passed the bound.</summary>
    internal static PinnedBufferWriter Write(int bound, Action<Utf8JsonWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        PinnedBufferWriter buffer = new(bound);
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            write(writer);
        }

        return buffer;
    }

    /// <summary>Writes a document that cannot pass its bound, as every form but a push's payload is.</summary>
    /// <exception cref="InvalidOperationException">The document passed its bound.</exception>
    internal static PinnedDocument WriteWithin(int bound, Action<Utf8JsonWriter> write)
    {
        using PinnedBufferWriter buffer = Write(bound, write);
        return buffer.Detach();
    }

    /// <summary>Writes a failure's code and details, the details in ordinal order of their names.</summary>
    internal static void WriteFailure(Utf8JsonWriter writer, Failure failure)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(failure);
        writer.WriteStartObject("failure");
        writer.WriteString("code", failure.Code);
        writer.WriteStartObject("details");
        foreach (KeyValuePair<string, string> detail in failure.Details.OrderBy(detail => detail.Key, StringComparer.Ordinal))
        {
            writer.WriteString(detail.Key, detail.Value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>A reader of a document, bounded in depth, which allows no comment and no trailing comma.</summary>
    internal static Utf8JsonReader Reader(ReadOnlySpan<byte> document)
    {
        return new Utf8JsonReader(document, new JsonReaderOptions { MaxDepth = MaximumDepth });
    }

    /// <summary>Whether the reader stands at the end of the document's one object, nothing after it but whitespace.</summary>
    internal static bool IsAtEnd(ref Utf8JsonReader reader)
    {
        return reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0 && !reader.Read();
    }

    /// <summary>Reads a number that is an <see cref="int"/>, or answers none for any other token, which it skips.</summary>
    internal static int? ReadInt(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value))
        {
            return value;
        }

        reader.Skip();
        return null;
    }

    /// <summary>Reads a string, or answers none for any other token, which it skips.</summary>
    internal static string? ReadString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }

    /// <summary>Reads a boolean, or answers none for any other token, which it skips.</summary>
    internal static bool? ReadBoolean(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            default:
                reader.Skip();
                return null;
        }
    }

    /// <summary>Reads a value in base64 into an array of <paramref name="values"/>, answering false for any other token or text.</summary>
    internal static bool TryReadValue(ref Utf8JsonReader reader, PinnedValues values, out ReadOnlyMemory<byte> value)
    {
        value = ReadOnlyMemory<byte>.Empty;
        if (reader.TokenType != JsonTokenType.String)
        {
            return false;
        }

        if (!reader.ValueIsEscaped)
        {
            return Base64Value.TryDecode(reader.ValueSpan, values, out value);
        }

        byte[] unescaped = GC.AllocateArray<byte>(reader.ValueSpan.Length, pinned: true);
        try
        {
            int length = reader.CopyString(unescaped);
            return Base64Value.TryDecode(unescaped.AsSpan(0, length), values, out value);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unescaped);
        }
    }

    /// <summary>Reads a failure's object, or answers none where it is not one: a code, and details of strings alone.</summary>
    internal static Failure? ReadFailure(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? code = null;
        Dictionary<string, string>? details = null;
        bool valid = true;
        HashSet<string> seen = new(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            valid &= seen.Add(name);
            reader.Read();
            switch (name)
            {
                case "code":
                    code = ReadString(ref reader);
                    break;
                case "details":
                    details = ReadDetails(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return valid && code is { Length: > 0 } && details is not null
            ? new Failure { Code = code, Details = details.ToFrozenDictionary(StringComparer.Ordinal) }
            : null;
    }

    /// <summary>The failure of a document outside its format.</summary>
    internal static Failure Invalid()
    {
        return new Failure { Code = Codes.RequestInvalid };
    }

    /// <summary>The failure of a document of another protocol version, naming <paramref name="version"/>.</summary>
    internal static Failure OtherVersion(int version)
    {
        return new Failure
        {
            Code = Codes.RequestVersion,
            Details = new Dictionary<string, string> { ["version"] = version.ToString(CultureInfo.InvariantCulture) }.ToFrozenDictionary(StringComparer.Ordinal),
        };
    }

    private static Dictionary<string, string>? ReadDetails(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        Dictionary<string, string> details = new(StringComparer.Ordinal);
        bool valid = true;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            reader.Read();
            string? value = ReadString(ref reader);
            if (value is null)
            {
                valid = false;
            }
            else
            {
                valid &= details.TryAdd(name, value);
            }
        }

        return valid ? details : null;
    }
}