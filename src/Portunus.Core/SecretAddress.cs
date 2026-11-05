using System;

namespace Portunus.Core;

/// <summary>
/// The address of one secret of one item: the item's title and the secret's name
/// (<c>00</c> section 1.5), written <c>&lt;Item&gt;/&lt;Name&gt;</c>, as <c>Themis/connection</c>.
/// Two addresses are equal where their parts are alike without regard to the case of ASCII
/// letters, as the vault compares names (<c>03</c> PRT-VLT-003).
/// </summary>
/// <param name="Item">The item's title (<c>03</c> PRT-VLT-002).</param>
/// <param name="Name">The secret's name (<c>03</c> PRT-VLT-003): a single secret's field, or a versioned secret's fields without their suffixes.</param>
public readonly record struct SecretAddress(string Item, string Name)
{
    /// <summary>The item's title.</summary>
    /// <exception cref="ArgumentException">The title is outside the rule of <c>03</c> PRT-VLT-002.</exception>
    public string Item { get; init => field = CheckItem(value); } = CheckItem(Item);

    /// <summary>The secret's name.</summary>
    /// <exception cref="ArgumentException">The name is outside the rule of <c>03</c> PRT-VLT-003.</exception>
    public string Name { get; init => field = CheckName(value); } = CheckName(Name);

    /// <summary>Reads an address written <c>&lt;Item&gt;/&lt;Name&gt;</c>.</summary>
    /// <param name="address">The address, as <c>Themis/connection</c>.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="address"/> holds no single <c>/</c>, or a part outside its rule.</exception>
    public static SecretAddress Parse(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        int slash = address.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || address.IndexOf('/', slash + 1) >= 0)
        {
            throw new ArgumentException("An address is an item title, '/' and a secret name.", nameof(address));
        }

        return new SecretAddress(address[..slash], address[(slash + 1)..]);
    }

    /// <summary>Reads <paramref name="address"/> as <see cref="Parse(string)"/> does, answering false where it would throw.</summary>
    internal static bool TryParse(string? address, out SecretAddress parsed)
    {
        parsed = default;
        int slash = address?.IndexOf('/', StringComparison.Ordinal) ?? -1;
        if (address is null || slash < 0 || address.IndexOf('/', slash + 1) >= 0)
        {
            return false;
        }

        string item = address[..slash];
        string name = address[(slash + 1)..];
        if (!Names.IsItemTitle(item) || !Names.IsSecretName(name))
        {
            return false;
        }

        parsed = new SecretAddress(item, name);
        return true;
    }

    /// <summary>Whether <paramref name="other"/> names the same secret, its parts compared without regard to the case of ASCII letters.</summary>
    /// <param name="other">The other address.</param>
    /// <returns>Whether the two are alike.</returns>
    public bool Equals(SecretAddress other)
    {
        return string.Equals(Item, other.Item, StringComparison.OrdinalIgnoreCase) && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A hash alike for addresses alike without regard to case.</summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Item is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Item),
            Name is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
    }

    /// <summary>The address as it is written, <c>&lt;Item&gt;/&lt;Name&gt;</c>.</summary>
    /// <returns>The address.</returns>
    public override string ToString()
    {
        return $"{Item}/{Name}";
    }

    private static string CheckItem(string item)
    {
        if (!Names.IsItemTitle(item))
        {
            throw new ArgumentException("An address's item is an item title of 03 PRT-VLT-002.");
        }

        return item;
    }

    private static string CheckName(string name)
    {
        if (!Names.IsSecretName(name))
        {
            throw new ArgumentException("An address's name is a secret name of 03 PRT-VLT-003.");
        }

        return name;
    }
}