using System;
using System.Collections.Generic;
using System.Linq;

namespace Portunus.Core;

/// <summary>
/// A set as the layout's rule reads it (<c>03</c> PRT-VLT-007): the vault's title and every
/// item of the vault, from a fetch, a push or the development file.
/// </summary>
/// <param name="Vault">The vault's title.</param>
/// <param name="Items">The vault's items.</param>
internal sealed record VaultSet(string Vault, IReadOnlyList<VaultItem> Items)
{
    /// <summary>
    /// Answers the secret the set holds at <paramref name="address"/>, item and name alike
    /// without regard to case, or none where it holds none: a single secret as version 1,
    /// current 1, and a versioned one as its live versions and the version its
    /// <c>.current</c> names (<c>03</c> PRT-VLT-004). A retired version is never answered, so
    /// a secret holding only retired versions is none (<c>03</c> PRT-VLT-005). The values are
    /// the set's own arrays; the set is one the layout's check passed.
    /// </summary>
    internal Secret? Find(SecretAddress address)
    {
        VaultItem? item = Items.FirstOrDefault(item => string.Equals(item.Title, address.Item, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }

        Dictionary<int, ReadOnlyMemory<byte>> versions = [];
        int? current = null;
        foreach (VaultField field in item.Fields)
        {
            if (FieldLabel.Parse(field.Label) is not FieldLabel label || !string.Equals(label.Name, address.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (label.Kind)
            {
                case FieldKind.Single:
                    return new Secret { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = field.Value } };
                case FieldKind.Version:
                    versions[label.Number] = field.Value;
                    break;
                case FieldKind.Current:
                    current = VersionNumber.Parse(field.Value.Span);
                    break;
                case FieldKind.Retired:
                    break;
                default:
                    throw new InvalidOperationException("A field's label is one of the kinds the layout gives.");
            }
        }

        return versions.Count > 0 && current is int number ? new Secret { Current = number, Versions = versions } : null;
    }
}