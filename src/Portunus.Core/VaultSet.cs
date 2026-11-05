using System.Collections.Generic;

namespace Portunus.Core;

/// <summary>
/// A set as the layout's rule reads it (<c>03</c> PRT-VLT-007): the vault's title and every
/// item of the vault, from a fetch, a push or the development file.
/// </summary>
/// <param name="Vault">The vault's title.</param>
/// <param name="Items">The vault's items.</param>
internal sealed record VaultSet(string Vault, IReadOnlyList<VaultItem> Items);