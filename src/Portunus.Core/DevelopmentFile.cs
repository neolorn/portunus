using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Portunus.Core;

/// <summary>
/// The development file (<c>03</c> PRT-VLT-008):
/// <c>{ "vault": "&lt;vault title&gt;", "items": { "&lt;item title&gt;": { "&lt;label&gt;": "&lt;value&gt;", ... } } }</c>,
/// each value a field's text as the vault holds it, read into a pinned array (<c>06</c>
/// PRT-ENG-005). Its titles and labels are checked by the layout's check, which also names a
/// title or a label given twice; its items carry no note.
/// </summary>
internal static class DevelopmentFile
{
    /// <summary>
    /// Reads <paramref name="document"/>, members it does not know left out, its values into
    /// the arrays of <paramref name="values"/>, which the caller disposes; answers none for a
    /// document that is not JSON or is outside the form, its arrays then already zeroed.
    /// </summary>
    internal static VaultSet? Read(ReadOnlySpan<byte> document, out PinnedValues values)
    {
        values = new PinnedValues();
        try
        {
            VaultSet? set = Parse(document, values);
            if (set is null)
            {
                values.Dispose();
            }

            return set;
        }
        catch (JsonException)
        {
            // A document that is not JSON is outside the form.
            values.Dispose();
            return null;
        }
    }

    private static VaultSet? Parse(ReadOnlySpan<byte> document, PinnedValues values)
    {
        Utf8JsonReader reader = WireJson.Reader(document);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        string? vault = null;
        List<VaultItem>? items = null;
        bool valid = true;
        HashSet<string> seen = new(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            valid &= seen.Add(name);
            reader.Read();
            switch (name)
            {
                case "vault":
                    vault = WireJson.ReadString(ref reader);
                    break;
                case "items":
                    items = ReadItems(ref reader, values);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return valid && vault is not null && items is not null && WireJson.IsAtEnd(ref reader) ? new VaultSet(vault, items) : null;
    }

    private static List<VaultItem>? ReadItems(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        List<VaultItem> items = [];
        bool valid = true;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string title = reader.GetString()!;
            reader.Read();
            List<VaultField>? fields = ReadFields(ref reader, values);
            valid &= fields is not null;
            if (fields is not null)
            {
                items.Add(new VaultItem(title, HasNote: false, fields));
            }
        }

        return valid ? items : null;
    }

    private static List<VaultField>? ReadFields(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        List<VaultField> fields = [];
        bool valid = true;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string label = reader.GetString()!;
            reader.Read();
            if (reader.TokenType != JsonTokenType.String)
            {
                reader.Skip();
                valid = false;
                continue;
            }

            fields.Add(new VaultField(label, ReadText(ref reader, values)));
        }

        return valid ? fields : null;
    }

    /// <summary>A field's text, unescaped by <see cref="Utf8JsonReader.CopyString(Span{byte})"/> into a pinned array of <paramref name="values"/> (<c>06</c> PRT-ENG-005).</summary>
    private static ReadOnlyMemory<byte> ReadText(ref Utf8JsonReader reader, PinnedValues values)
    {
        // Unescaping never lengthens a text, so its written length is room enough.
        byte[] value = values.Allocate(reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length);
        return value.AsMemory(0, reader.CopyString(value));
    }
}