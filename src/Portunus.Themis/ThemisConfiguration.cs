using System;
using System.Collections.Frozen;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using Portunus.Core;

namespace Portunus.Themis;

/// <summary>
/// The adapter's keys (<c>07</c> section 4; <c>04</c> PRT-LIB-006): <c>Portunus:Themis:Item</c>,
/// the item title the secrets are read from, by default <c>Themis</c>; and
/// <c>Portunus:Themis:Items:&lt;secret name&gt;</c>, the item title one secret is read from
/// instead. Each value is checked on its raw value, as an item title of <c>03</c> PRT-VLT-002,
/// and each key's secret name as a secret name, by the address's own rule (<c>00</c> section
/// 1.5); one outside its rule stops the start naming the key (<c>05</c> PRT-OPS-001).
/// </summary>
/// <param name="Item">The item the secrets are read from.</param>
/// <param name="Items">The item each named secret is read from instead.</param>
internal sealed record ThemisConfiguration(string Item, IReadOnlyDictionary<string, string> Items)
{
    private const string ItemKey = "Portunus:Themis:Item";

    private const string ItemsKey = "Portunus:Themis:Items";

    private const string DefaultItem = "Themis";

    /// <summary>A secret name that is always within its rule, to check an item title alone by the address's rule.</summary>
    private const string AnyName = "connection";

    private const string ConfigurationInvalid = "secrets.startup.configurationinvalid";

    /// <summary>Reads and checks the adapter's keys.</summary>
    /// <exception cref="SecretsStartupException">A key is outside its rule.</exception>
    internal static ThemisConfiguration Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string item = configuration[ItemKey] ?? DefaultItem;
        if (!IsAddress(item, AnyName))
        {
            throw Invalid(ItemKey);
        }

        Dictionary<string, string> items = new(StringComparer.OrdinalIgnoreCase);
        foreach (IConfigurationSection entry in configuration.GetSection(ItemsKey).GetChildren())
        {
            string key = $"{ItemsKey}:{entry.Key}";
            if (entry.Value is not string title || !IsAddress(title, entry.Key) || !items.TryAdd(entry.Key, title))
            {
                throw Invalid(key);
            }
        }

        return new ThemisConfiguration(item, items.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>The address of the secret <paramref name="name"/>: its own item where one is set, else the adapter's.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is outside the secret name's rule.</exception>
    internal SecretAddress Address(string name)
    {
        return new SecretAddress(Items.TryGetValue(name, out string? item) ? item : Item, name);
    }

    private static bool IsAddress(string item, string name)
    {
        try
        {
            _ = new SecretAddress(item, name);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static SecretsStartupException Invalid(string key)
    {
        return new SecretsStartupException(ConfigurationInvalid, new Dictionary<string, string> { ["key"] = key });
    }
}