namespace Portunus.Core;

/// <summary>
/// One finding of the layout's check (<c>03</c> PRT-VLT-007): its code of <c>07</c> section 1,
/// the item and the field it names where the code names them, and whether it is a warning,
/// which refuses nothing, rather than an error, which refuses the set.
/// </summary>
/// <param name="Code">The finding's code.</param>
/// <param name="Item">The item's title, where the code names one.</param>
/// <param name="Field">The field's label, or the secret's name for a finding about a versioned secret as a whole, where the code names one.</param>
/// <param name="IsWarning">Whether the finding is a warning.</param>
internal sealed record LayoutFinding(string Code, string? Item, string? Field, bool IsWarning);