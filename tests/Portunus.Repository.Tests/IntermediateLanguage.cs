using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Portunus.Repository.Tests;

/// <summary>
/// Reads a method body's IL instruction by instruction, by the operand each opcode takes
/// (ECMA-335 partition III), the opcodes taken from <see cref="OpCodes"/>.
/// </summary>
internal static class IntermediateLanguage
{
    private static readonly FrozenDictionary<int, OperandType> Operands = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToFrozenDictionary(code => (int)(ushort)code.Value, code => code.OperandType);

    /// <summary>The tokens of the instructions that name a method or a field: calls, loads, stores and <c>ldtoken</c>.</summary>
    internal static IReadOnlyList<EntityHandle> MemberTokens(BlobReader il)
    {
        List<EntityHandle> tokens = [];
        foreach ((OperandType operand, int value) in Tokens(il))
        {
            if (operand is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok)
            {
                EntityHandle token = MetadataTokens.EntityHandle(value);
                if (token.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
                {
                    tokens.Add(token);
                }
            }
        }

        return tokens;
    }

    /// <summary>
    /// The strings the body's <c>ldstr</c> instructions load, as their tokens on the user string
    /// heap, each with the method or field the next instruction that names one names, or none.
    /// </summary>
    internal static IReadOnlyList<(UserStringHandle String, EntityHandle? Next)> StringTokens(BlobReader il)
    {
        List<(OperandType Operand, int Value)> tokens = Tokens(il);
        List<(UserStringHandle String, EntityHandle? Next)> strings = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Operand != OperandType.InlineString)
            {
                continue;
            }

            EntityHandle? next = tokens.Skip(index + 1)
                .Where(token => token.Operand is OperandType.InlineMethod or OperandType.InlineField)
                .Select(token => (EntityHandle?)MetadataTokens.EntityHandle(token.Value))
                .FirstOrDefault();
            strings.Add((MetadataTokens.UserStringHandle(tokens[index].Value & 0x00FFFFFF), next));
        }

        return strings;
    }

    /// <summary>Each instruction whose operand is a metadata token, with the kind of its operand and the token.</summary>
    private static List<(OperandType Operand, int Value)> Tokens(BlobReader il)
    {
        List<(OperandType Operand, int Value)> tokens = [];
        while (il.RemainingBytes > 0)
        {
            int code = il.ReadByte();
            if (code == 0xFE)
            {
                code = 0xFE00 | il.ReadByte();
            }

            OperandType operand = Operands[code];
            switch (operand)
            {
                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok or OperandType.InlineString:
                    tokens.Add((operand, il.ReadInt32()));
                    break;
                case OperandType.InlineSwitch:
                    int targets = il.ReadInt32();
                    il.Offset += 4 * targets;
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    il.Offset += 1;
                    break;
                case OperandType.InlineVar:
                    il.Offset += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    il.Offset += 8;
                    break;
                default:
                    il.Offset += 4;
                    break;
            }
        }

        return tokens;
    }
}