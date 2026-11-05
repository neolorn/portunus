using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Portunus.Core;

/// <summary>
/// The requests of <c>07</c> section 6: a read, a status request and a push, each one exact
/// form, its members in the order <c>07</c> gives (<c>06</c> PRT-ENG-006).
/// </summary>
internal static class RequestFormat
{
    /// <summary>Writes <c>{"version":1,"kind":"read","address":"Themis/connection"}</c>.</summary>
    internal static PinnedDocument WriteRead(SecretAddress address)
    {
        return WireJson.WriteWithin(Limits.RequestBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("kind", "read");
            writer.WriteString("address", address.ToString());
            writer.WriteEndObject();
        });
    }

    /// <summary>Writes <c>{"version":1,"kind":"status"}</c>.</summary>
    internal static PinnedDocument WriteStatus()
    {
        return WireJson.WriteWithin(Limits.RequestBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("kind", "status");
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Writes a push of <paramref name="set"/>, its items in their order, each without its note,
    /// or answers false, writing nothing it keeps, where the payload passes 2 MiB, which the tool
    /// refuses with <c>secrets.push.toolarge</c> (<c>02</c> PRT-PSH-003).
    /// </summary>
    internal static bool TryWritePush(VaultSet set, bool replace, [NotNullWhen(true)] out PinnedDocument? document)
    {
        ArgumentNullException.ThrowIfNull(set);
        using PinnedBufferWriter buffer = WireJson.Write(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("kind", "push");
            writer.WriteString("vault", set.Vault);
            writer.WriteBoolean("replace", replace);
            writer.WriteStartArray("items");
            foreach (VaultItem item in set.Items)
            {
                writer.WriteStartObject();
                writer.WriteString("title", item.Title);
                writer.WriteStartArray("fields");
                foreach (VaultField field in item.Fields)
                {
                    writer.WriteStartObject();
                    writer.WriteString("label", field.Label);
                    writer.WriteBase64String("value", field.Value.Span);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        document = buffer.IsOverBound ? null : buffer.Detach();
        return document is not null;
    }

    /// <summary>
    /// Reads a request of at most <paramref name="bound"/> bytes, members it does not know left
    /// out; answers <c>secrets.request.version</c>, naming this protocol's version, for a request
    /// of another, and <c>secrets.request.invalid</c> for one over its bound or outside its form.
    /// </summary>
    internal static Result<WireRequest> Read(ReadOnlySpan<byte> document, int bound)
    {
        if (document.Length > bound)
        {
            return Result.Fail<WireRequest>(WireJson.Invalid());
        }

        PinnedValues? values = new();
        try
        {
            Result<WireRequest> request = Parse(document, values);

            // A push's values stay in their arrays for the request's taker, who zeroes them.
            if (request.Value is WireRequest.Push)
            {
                values = null;
            }

            return request;
        }
        catch (JsonException)
        {
            // A document that is not JSON is a request outside its format, which the agent refuses.
            return Result.Fail<WireRequest>(WireJson.Invalid());
        }
        finally
        {
            values?.Dispose();
        }
    }

    private static Result<WireRequest> Parse(ReadOnlySpan<byte> document, PinnedValues values)
    {
        Utf8JsonReader reader = WireJson.Reader(document);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return Result.Fail<WireRequest>(WireJson.Invalid());
        }

        int? version = null;
        string? kind = null;
        string? address = null;
        string? vault = null;
        bool? replace = null;
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
                case "version":
                    version = WireJson.ReadInt(ref reader);
                    valid &= version is not null;
                    break;
                case "kind":
                    kind = WireJson.ReadString(ref reader);
                    break;
                case "address":
                    address = WireJson.ReadString(ref reader);
                    valid &= address is not null;
                    break;
                case "vault":
                    vault = WireJson.ReadString(ref reader);
                    valid &= vault is not null;
                    break;
                case "replace":
                    replace = WireJson.ReadBoolean(ref reader);
                    valid &= replace is not null;
                    break;
                case "items":
                    items = ReadItems(ref reader, values);
                    valid &= items is not null;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        valid &= WireJson.IsAtEnd(ref reader);

        if (version is null)
        {
            return Result.Fail<WireRequest>(WireJson.Invalid());
        }

        if (version != WireJson.ProtocolVersion)
        {
            return Result.Fail<WireRequest>(WireJson.OtherVersion(WireJson.ProtocolVersion));
        }

        WireRequest? request = kind switch
        {
            "read" when valid && address is not null && vault is null && replace is null && items is null
                && SecretAddress.TryParse(address, out SecretAddress parsed) => new WireRequest.Read(parsed),
            "status" when valid && address is null && vault is null && replace is null && items is null => new WireRequest.Status(),
            "push" when valid && address is null && vault is not null && replace is not null && items is not null
                => new WireRequest.Push(new VaultSet(vault, items), replace.Value, values),
            _ => null,
        };
        return request is null ? Result.Fail<WireRequest>(WireJson.Invalid()) : Result.Success(request);
    }

    private static List<VaultItem>? ReadItems(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        List<VaultItem> items = [];
        bool valid = true;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            VaultItem? item = ReadItem(ref reader, values);
            if (item is null)
            {
                valid = false;
            }
            else
            {
                items.Add(item);
            }
        }

        return valid ? items : null;
    }

    private static VaultItem? ReadItem(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? title = null;
        List<VaultField>? fields = null;
        bool valid = true;
        HashSet<string> seen = new(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            valid &= seen.Add(name);
            reader.Read();
            switch (name)
            {
                case "title":
                    title = WireJson.ReadString(ref reader);
                    break;
                case "fields":
                    fields = ReadFields(ref reader, values);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return valid && title is not null && fields is not null ? new VaultItem(title, HasNote: false, fields) : null;
    }

    private static List<VaultField>? ReadFields(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        List<VaultField> fields = [];
        bool valid = true;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            VaultField? field = ReadField(ref reader, values);
            if (field is null)
            {
                valid = false;
            }
            else
            {
                fields.Add(field);
            }
        }

        return valid ? fields : null;
    }

    private static VaultField? ReadField(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? label = null;
        ReadOnlyMemory<byte>? value = null;
        bool valid = true;
        HashSet<string> seen = new(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            valid &= seen.Add(name);
            reader.Read();
            switch (name)
            {
                case "label":
                    label = WireJson.ReadString(ref reader);
                    break;
                case "value":
                    if (WireJson.TryReadValue(ref reader, values, out ReadOnlyMemory<byte> read))
                    {
                        value = read;
                    }
                    else
                    {
                        reader.Skip();
                        valid = false;
                    }

                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return valid && label is not null && value is not null ? new VaultField(label, value.Value) : null;
    }
}