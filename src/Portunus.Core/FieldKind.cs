namespace Portunus.Core;

/// <summary>
/// What a field of an item is to its secret (<c>03</c> PRT-VLT-004, PRT-VLT-005).
/// </summary>
internal enum FieldKind
{
    /// <summary>A single secret's one field, <c>&lt;name&gt;</c>.</summary>
    Single,

    /// <summary>A live version, <c>&lt;name&gt;.&lt;n&gt;</c>.</summary>
    Version,

    /// <summary>The marker of the current version, <c>&lt;name&gt;.current</c>.</summary>
    Current,

    /// <summary>A retired version, <c>&lt;name&gt;.&lt;n&gt;.retired</c>, kept and never delivered.</summary>
    Retired,
}