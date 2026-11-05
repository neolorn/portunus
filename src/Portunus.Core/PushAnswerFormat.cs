using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Portunus.Core;

/// <summary>
/// The answer of <c>07</c> section 6 to a push: <c>{"version":1,"accepted":true,"items":4}</c>,
/// or <c>{"version":1,"accepted":false,"failure":{...}}</c> (<c>06</c> PRT-ENG-006).
/// </summary>
internal static class PushAnswerFormat
{
    /// <summary>Writes that the agent took a set of <paramref name="items"/> items.</summary>
    internal static PinnedDocument WriteAccepted(int items)
    {
        return WireJson.WriteWithin(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteBoolean("accepted", true);
            writer.WriteNumber("items", items);
            writer.WriteEndObject();
        });
    }

    /// <summary>Writes that the agent refused the push with <paramref name="failure"/>.</summary>
    internal static PinnedDocument WriteRefused(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return WireJson.WriteWithin(Limits.AnswerBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", WireJson.ProtocolVersion);
            writer.WriteBoolean("accepted", false);
            WireJson.WriteFailure(writer, failure);
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Reads a push's answer of at most 2 MiB, members it does not know left out; answers
    /// <c>secrets.request.version</c>, naming the agent's version, for an answer of another, and
    /// <c>secrets.request.invalid</c> for one over its bound or outside both forms.
    /// </summary>
    internal static Result<PushAnswer> Read(ReadOnlySpan<byte> document)
    {
        if (document.Length > Limits.AnswerBytes)
        {
            return Result.Fail<PushAnswer>(WireJson.Invalid());
        }

        try
        {
            return Parse(document);
        }
        catch (JsonException)
        {
            // A document that is not JSON is an answer outside both forms.
            return Result.Fail<PushAnswer>(WireJson.Invalid());
        }
    }

    private static Result<PushAnswer> Parse(ReadOnlySpan<byte> document)
    {
        Utf8JsonReader reader = WireJson.Reader(document);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return Result.Fail<PushAnswer>(WireJson.Invalid());
        }

        int? version = null;
        bool? accepted = null;
        int? items = null;
        Failure? failure = null;
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
                    break;
                case "accepted":
                    accepted = WireJson.ReadBoolean(ref reader);
                    valid &= accepted is not null;
                    break;
                case "items":
                    items = WireJson.ReadInt(ref reader);
                    valid &= items is >= 0;
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
            return Result.Fail<PushAnswer>(WireJson.Invalid());
        }

        if (version != WireJson.ProtocolVersion)
        {
            return Result.Fail<PushAnswer>(WireJson.OtherVersion(version.Value));
        }

        PushAnswer? answer = (valid, accepted, items, failure) switch
        {
            (true, true, int count, null) => new PushAnswer.Accepted(count),
            (true, false, null, Failure refusal) => new PushAnswer.Refused(refusal),
            _ => null,
        };
        return answer is null ? Result.Fail<PushAnswer>(WireJson.Invalid()) : Result.Success(answer);
    }
}