using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class SecretTests
{
    // Known bytes, which the printed secret must not hold in any form.
    private static readonly byte[] First = Encoding.UTF8.GetBytes("first-known-value");

    private static readonly byte[] Second = Encoding.UTF8.GetBytes("second-known-value");

    [Fact]
    public void PRT_LIB_002_AC3_FormattingASecretWritesNoneOfItsValues()
    {
        Secret secret = new()
        {
            Current = 2,
            Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [2] = Second, [1] = First },
        };

        string printed = secret.ToString();

        Assert.Equal("Secret { Current = 2, Versions = [1, 2] }", printed);
        foreach (byte[] value in new[] { First, Second })
        {
            Assert.DoesNotContain(Encoding.UTF8.GetString(value), printed, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToBase64String(value), printed, StringComparison.Ordinal);
            Assert.DoesNotContain(System.Buffers.Text.Base64Url.EncodeToString(value), printed, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToHexString(value), printed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(string.Join(", ", value.Select(octet => octet.ToString(System.Globalization.CultureInfo.InvariantCulture))), printed, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CurrentValue_CurrentAmongTheVersions_AnswersItsValue()
    {
        Secret secret = new()
        {
            Current = 1,
            Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = First, [2] = Second },
        };

        Assert.True(secret.CurrentValue.Span.SequenceEqual(First));
    }

    [Fact]
    public void CurrentValue_CurrentNotAmongTheVersions_Throws()
    {
        Secret secret = new()
        {
            Current = 3,
            Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = First },
        };

        Assert.Throws<InvalidOperationException>(() => secret.CurrentValue);
    }
}