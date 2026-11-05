using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

using JanusCore = Janus.Core;

namespace Portunus.Janus;

/// <summary>
/// A social provider's signing credential (<c>04</c> PRT-LIB-006): the JSON object
/// <c>{ "issuer", "keyId", "key" }</c>, each member a string and each once, <c>key</c> base64 of
/// the PKCS #8 private key, decoded into a pinned array; any other object is not of its form.
/// </summary>
internal static class SignedCredential
{
    /// <summary>The credential, or none where <paramref name="value"/> is not of the form.</summary>
    internal static JanusCore.ProviderCredential? Read(ReadOnlySpan<byte> value)
    {
        string? issuer = null;
        string? keyId = null;
        byte[]? key = null;
        try
        {
            Utf8JsonReader reader = new(value);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString()!;
                if (!seen.Add(name) || !reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    return null;
                }

                switch (name)
                {
                    case "issuer":
                        issuer = reader.GetString();
                        break;
                    case "keyId":
                        keyId = reader.GetString();
                        break;
                    case "key":
                        key = Key(ref reader);
                        if (key is null)
                        {
                            return null;
                        }

                        break;
                    default:
                        return null;
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read() || issuer is null || keyId is null || key is null)
            {
                return null;
            }

            JanusCore.ProviderCredential signed = JanusCore.ProviderCredential.Signed(issuer, keyId, key);
            key = null;
            return signed;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    /// <summary>The key's base64 text copied into a pinned array, decoded into another, the copy zeroed.</summary>
    private static byte[]? Key(ref Utf8JsonReader reader)
    {
        byte[] text = GC.AllocateArray<byte>(reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length, pinned: true);
        try
        {
            int length = reader.CopyString(text);
            return Base64Text.Decode(text.AsSpan(0, length));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(text);
        }
    }
}