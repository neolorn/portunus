using System;
using System.Text;

namespace Portunus.Core;

/// <summary>
/// One field of an item (<c>03</c> PRT-VLT-003): its label and its value, the UTF-8 bytes of
/// its text, held by whoever read the set, who zeroes them (<c>06</c> PRT-ENG-005).
/// </summary>
/// <param name="Label">The field's label.</param>
/// <param name="Value">The field's value.</param>
internal sealed record VaultField(string Label, ReadOnlyMemory<byte> Value)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Label = ").Append(Label);
        return true;
    }
}