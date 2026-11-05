using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Portunus.Core;

/// <summary>
/// The answers of <c>07</c> section 6 to a read or a status request: while waiting, the
/// status while ready, a secret and a failure, each one exact form, its members in the order
/// <c>07</c> gives (<c>06</c> PRT-ENG-006).
/// </summary>
internal static class AnswerFormat
{
    /// <summary>Writes <c>{"version":1,"state":"waiting","reason":"ratelimited","since":"2026-11-05T09:00:00Z"}</c>.</summary>
    internal static PinnedDocument WriteWaiting(WaitReason reason, DateTimeOffset since)
    {
        return WireJson.WriteWithin(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("state", Spellings.Of(AgentState.Waiting));
            writer.WriteString("reason", Spellings.Of(reason));
            writer.WriteString("since", Instant.Format(since));
            writer.WriteEndObject();
        });
    }

    /// <summary>Writes <c>{"version":1,"state":"ready","since":"...","source":"vault","loadedAt":"...","items":4}</c>.</summary>
    internal static PinnedDocument WriteReady(DateTimeOffset since, SetSource source, DateTimeOffset loadedAt, int items)
    {
        return WireJson.WriteWithin(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("state", Spellings.Of(AgentState.Ready));
            writer.WriteString("since", Instant.Format(since));
            writer.WriteString("source", Spellings.Of(source));
            writer.WriteString("loadedAt", Instant.Format(loadedAt));
            writer.WriteNumber("items", items);
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Writes <c>{"version":1,"state":"ready","secret":{"current":2,"versions":{"1":"&lt;b64&gt;","2":"&lt;b64&gt;"}}}</c>,
    /// the versions in ascending order of number.
    /// </summary>
    internal static PinnedDocument WriteSecret(int current, IReadOnlyDictionary<int, ReadOnlyMemory<byte>> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        return WireJson.WriteWithin(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("state", Spellings.Of(AgentState.Ready));
            writer.WriteStartObject("secret");
            writer.WriteNumber("current", current);
            writer.WriteStartObject("versions");
            foreach (KeyValuePair<int, ReadOnlyMemory<byte>> version in versions.OrderBy(version => version.Key))
            {
                writer.WritePropertyName(version.Key.ToString(CultureInfo.InvariantCulture));
                Base64Value.Write(writer, version.Value.Span);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    /// <summary>Writes <c>{"version":1,"state":"&lt;state&gt;","failure":{"code":"secrets.secret.denied","details":{}}}</c>.</summary>
    internal static PinnedDocument WriteFailure(AgentState state, Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return WireJson.WriteWithin(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteString("state", Spellings.Of(state));
            WireJson.WriteFailure(writer, failure);
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Reads an answer of at most 2 MiB, members it does not know left out; answers
    /// <c>secrets.request.version</c>, naming the agent's version, for an answer of another, and
    /// <c>secrets.request.invalid</c> for one over its bound or outside every form.
    /// </summary>
    internal static Result<WireAnswer> Read(ReadOnlySpan<byte> document)
    {
        if (document.Length > Limits.AnswerBytes)
        {
            return Result.Fail<WireAnswer>(WireJson.Invalid());
        }

        PinnedValues? values = new();
        try
        {
            Result<WireAnswer> answer = Parse(document, values);

            // A secret's values stay in their arrays for the answer's reader, who zeroes them.
            if (answer.Value is WireAnswer.Value)
            {
                values = null;
            }

            return answer;
        }
        catch (JsonException)
        {
            // A document that is not JSON is an answer outside every form.
            return Result.Fail<WireAnswer>(WireJson.Invalid());
        }
        finally
        {
            values?.Dispose();
        }
    }

    private static Result<WireAnswer> Parse(ReadOnlySpan<byte> document, PinnedValues values)
    {
        Utf8JsonReader reader = WireJson.Reader(document);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return Result.Fail<WireAnswer>(WireJson.Invalid());
        }

        int? version = null;
        AgentState? state = null;
        WaitReason? reason = null;
        DateTimeOffset? since = null;
        SetSource? source = null;
        DateTimeOffset? loadedAt = null;
        int? items = null;
        WireAnswer.Value? secret = null;
        Failure? failure = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        bool valid = true;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            valid &= seen.Add(name);
            reader.Read();
            switch (name)
            {
                case "version":
                    version = WireJson.ReadInt(ref reader);
                    break;
                case "state":
                    state = Spellings.ReadAgentState(WireJson.ReadString(ref reader));
                    valid &= state is not null;
                    break;
                case "reason":
                    reason = Spellings.ReadWaitReason(WireJson.ReadString(ref reader));
                    valid &= reason is not null;
                    break;
                case "since":
                    since = ReadInstant(ref reader);
                    valid &= since is not null;
                    break;
                case "source":
                    source = Spellings.ReadSetSource(WireJson.ReadString(ref reader));
                    valid &= source is not null;
                    break;
                case "loadedAt":
                    loadedAt = ReadInstant(ref reader);
                    valid &= loadedAt is not null;
                    break;
                case "items":
                    items = WireJson.ReadInt(ref reader);
                    valid &= items is >= 0;
                    break;
                case "secret":
                    secret = ReadSecret(ref reader, values);
                    valid &= secret is not null;
                    break;
                case "failure":
                    failure = WireJson.ReadFailure(ref reader);
                    valid &= failure is not null;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        valid &= WireJson.IsAtEnd(ref reader);

        if (version is null)
        {
            return Result.Fail<WireAnswer>(WireJson.Invalid());
        }

        if (version != WireJson.ProtocolVersion)
        {
            return Result.Fail<WireAnswer>(WireJson.OtherVersion(version.Value));
        }

        bool statusMembers = reason is not null || since is not null || source is not null || loadedAt is not null || items is not null;
        WireAnswer? answer = (valid, state, failure, secret) switch
        {
            (false, _, _, _) or (_, null, _, _) => null,
            (_, AgentState s, Failure f, null) when !statusMembers => new WireAnswer.Refused(s, f),
            (_, AgentState.Ready, null, WireAnswer.Value v) when !statusMembers => v,
            (_, AgentState.Ready, null, null) when reason is null && since is not null && source is not null && loadedAt is not null && items is not null
                => new WireAnswer.Ready(since.Value, source.Value, loadedAt.Value, items.Value),
            (_, AgentState.Waiting, null, null) when reason is not null && since is not null && source is null && loadedAt is null && items is null
                => new WireAnswer.Waiting(reason.Value, since.Value),
            _ => null,
        };
        return answer is null ? Result.Fail<WireAnswer>(WireJson.Invalid()) : Result.Success(answer);
    }

    private static DateTimeOffset? ReadInstant(ref Utf8JsonReader reader)
    {
        string? text = WireJson.ReadString(ref reader);
        return text is null ? null : Instant.Parse(text);
    }

    private static WireAnswer.Value? ReadSecret(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        int? current = null;
        Dictionary<int, ReadOnlyMemory<byte>>? versions = null;
        bool valid = true;
        HashSet<string> seen = new(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            valid &= seen.Add(name);
            reader.Read();
            switch (name)
            {
                case "current":
                    current = WireJson.ReadInt(ref reader);
                    break;
                case "versions":
                    versions = ReadVersions(ref reader, values);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        bool whole = valid && current is not null && versions is not null && versions.ContainsKey(current.Value);
        return whole ? new WireAnswer.Value(current!.Value, versions!.ToFrozenDictionary(), values) : null;
    }

    private static Dictionary<int, ReadOnlyMemory<byte>>? ReadVersions(ref Utf8JsonReader reader, PinnedValues values)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        Dictionary<int, ReadOnlyMemory<byte>> versions = [];
        bool valid = true;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            int? number = VersionNumber.Parse(reader.GetString().AsSpan());
            reader.Read();
            if (number is not null && WireJson.TryReadValue(ref reader, values, out ReadOnlyMemory<byte> value))
            {
                valid &= versions.TryAdd(number.Value, value);
            }
            else
            {
                reader.Skip();
                valid = false;
            }
        }

        return valid ? versions : null;
    }
}