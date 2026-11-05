using System;
using System.Collections.Generic;
using System.Text.Json;

using Portunus.Core;

namespace Portunus.Agent.Fetch;

/// <summary>
/// What the agent reads of <c>op</c>'s JSON (<c>01</c> PRT-AGT-003): of the list, each entry's
/// <c>id</c>, <c>title</c> and <c>vault.id</c>; of an item, its <c>title</c> and each field's
/// <c>label</c> and <c>value</c>, the field labelled <c>notesPlain</c> being the item's note,
/// never a secret. A value is copied as its UTF-8 bytes into a pinned array
/// (<c>Utf8JsonReader.CopyString</c>), never as a string; every other member is left out.
/// </summary>
internal static class OpJson
{
    private const string NoteLabel = "notesPlain";

    /// <summary>The vault's identifier and each item's identifier and title, or none where the list is outside that shape.</summary>
    internal static (string Vault, IReadOnlyList<(string Id, string Title)> Items)? ReadList(ReadOnlySpan<byte> document)
    {
        try
        {
            Utf8JsonReader reader = new(document);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                return null;
            }

            List<(string Id, string Title)> items = [];
            string? vault = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                string? id = null;
                string? title = null;
                string? itemVault = null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    string name = reader.GetString()!;
                    reader.Read();
                    switch (name)
                    {
                        case "id":
                            id = String(ref reader);
                            break;
                        case "title":
                            title = String(ref reader);
                            break;
                        case "vault":
                            itemVault = VaultId(ref reader);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                if (id is null || title is null || itemVault is null || (vault is not null && vault != itemVault))
                {
                    return null;
                }

                vault = itemVault;
                items.Add((id, title));
            }

            return reader.TokenType == JsonTokenType.EndArray && !reader.Read() && (vault is not null || items.Count == 0) ? (vault ?? string.Empty, items) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The item's title, whether it carries a note, and its fields, values in arrays of <paramref name="values"/>; or none where it is outside that shape.</summary>
    internal static VaultItem? ReadItem(ReadOnlySpan<byte> document, PinnedValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        try
        {
            Utf8JsonReader reader = new(document);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            string? title = null;
            bool note = false;
            List<VaultField> fields = [];
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString()!;
                reader.Read();
                switch (name)
                {
                    case "title":
                        title = String(ref reader);
                        break;
                    case "fields" when reader.TokenType == JsonTokenType.StartArray:
                        while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                        {
                            (string? label, ReadOnlyMemory<byte> value) = Field(ref reader, values);
                            if (label is null)
                            {
                                return null;
                            }

                            if (label == NoteLabel)
                            {
                                note |= value.Length > 0;
                            }
                            else
                            {
                                fields.Add(new VaultField(label, value));
                            }
                        }

                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return title is null || !(reader.TokenType == JsonTokenType.EndObject && !reader.Read()) ? null : new VaultItem(title, note, fields);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string? Label, ReadOnlyMemory<byte> Value) Field(ref Utf8JsonReader reader, PinnedValues values)
    {
        string? label = null;
        ReadOnlyMemory<byte> value = ReadOnlyMemory<byte>.Empty;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            reader.Read();
            switch (name)
            {
                case "label":
                    label = String(ref reader);
                    break;
                case "value" when reader.TokenType == JsonTokenType.String:
                    byte[] array = values.Allocate(reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length);
                    value = array.AsMemory(0, reader.CopyString(array));
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return (label, value);
    }

    private static string? VaultId(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? id = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            reader.Read();
            if (name == "id")
            {
                id = String(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }

        return id;
    }

    private static string? String(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }
}