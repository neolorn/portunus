using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Portunus.Core.Tests;

/// <summary>
/// Sets a test writes for the layout's check: a vault of the rule and items of fields whose
/// values are the UTF-8 bytes of short texts, none of them a secret.
/// </summary>
internal static class Sets
{
    internal const string Vault = "Sample Production";

    internal static VaultSet Of(params VaultItem[] items)
    {
        return new VaultSet(Vault, items);
    }

    internal static VaultItem Item(string title, params (string Label, string Value)[] fields)
    {
        return new VaultItem(title, HasNote: true, [.. fields.Select(field => Field(field.Label, field.Value))]);
    }

    internal static VaultItem ItemWithoutNote(string title, params (string Label, string Value)[] fields)
    {
        return Item(title, fields) with { HasNote = false };
    }

    internal static VaultField Field(string label, string value)
    {
        return new VaultField(label, Encoding.UTF8.GetBytes(value));
    }

    internal static VaultItem Item(string title, IReadOnlyList<VaultField> fields)
    {
        return new VaultItem(title, HasNote: true, fields);
    }
}