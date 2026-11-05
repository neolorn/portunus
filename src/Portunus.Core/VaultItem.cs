using System.Collections.Generic;

namespace Portunus.Core;

/// <summary>
/// One item of a vault (<c>03</c> PRT-VLT-002): its title, whether it carries a note, and its
/// fields, every section of it together. A pushed item carries no note, since its note is not
/// sent (<c>07</c> section 6); the note's warning is <c>portunus check</c>'s alone.
/// </summary>
/// <param name="Title">The item's title.</param>
/// <param name="HasNote">Whether the item carries a note.</param>
/// <param name="Fields">The item's fields.</param>
internal sealed record VaultItem(string Title, bool HasNote, IReadOnlyList<VaultField> Fields);