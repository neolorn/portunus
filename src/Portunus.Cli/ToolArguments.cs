using System;
using System.Collections.Generic;
using System.Linq;

using Portunus.Core;

namespace Portunus.Cli;

/// <summary>
/// The tool's command and options (<c>07</c> section 3), each checked against its rule before
/// anything runs (<c>02</c> PRT-PSH-002): an option missing, given twice, without its value or
/// outside its rule refuses with <c>secrets.cli.inputinvalid</c> naming it as <c>07</c> writes it.
/// </summary>
/// <param name="Command">The command: <c>push</c>, <c>check</c> or <c>status</c>.</param>
/// <param name="Vault">The vault's title, for <c>push</c> and <c>check</c>.</param>
/// <param name="To">The SSH destination, for <c>push</c> and <c>status</c>.</param>
/// <param name="Instance">The agent's instance, for <c>push</c> and <c>status</c>.</param>
/// <param name="Account">The 1Password account, where given.</param>
/// <param name="Replace">Whether the push replaces a ready agent's set.</param>
internal sealed record ToolArguments(string Command, string? Vault, string? To, string? Instance, string? Account, bool Replace)
{
    internal const string Push = "push";

    internal const string Check = "check";

    internal const string Status = "status";

    private const string VaultOption = "--vault";

    private const string ToOption = "--to";

    private const string InstanceOption = "--instance";

    private const string AccountOption = "--account";

    private const string ReplaceOption = "--replace";

    private const int MostLength = 255;

    /// <summary>The options each command takes, the required ones first (<c>07</c> section 3).</summary>
    private static readonly Dictionary<string, (string[] Required, string[] Optional)> Options = new(StringComparer.Ordinal)
    {
        [Push] = ([VaultOption, ToOption, InstanceOption], [AccountOption, ReplaceOption]),
        [Check] = ([VaultOption], [AccountOption]),
        [Status] = ([ToOption, InstanceOption], []),
    };

    /// <summary>
    /// Reads <paramref name="arguments"/>: a command <c>07</c> does not list, or a word that is no
    /// option of it, is a mistake only the caller makes, <c>secrets.fault</c> (<c>06</c> PRT-ENG-003).
    /// </summary>
    internal static Result<ToolArguments> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0 || !Options.TryGetValue(arguments[0], out (string[] Required, string[] Optional) taken))
        {
            return Result.Fail<ToolArguments>(new Failure { Code = Codes.Fault });
        }

        Dictionary<string, string?> given = new(StringComparer.Ordinal);
        for (int index = 1; index < arguments.Count; index++)
        {
            string option = arguments[index];
            if (!taken.Required.Contains(option) && !taken.Optional.Contains(option))
            {
                return Result.Fail<ToolArguments>(new Failure { Code = Codes.Fault });
            }

            if (given.ContainsKey(option))
            {
                return Invalid(option);
            }

            if (option == ReplaceOption)
            {
                given[option] = null;
                continue;
            }

            if (index + 1 == arguments.Count)
            {
                return Invalid(option);
            }

            given[option] = arguments[++index];
        }

        if (taken.Required.FirstOrDefault(option => !given.ContainsKey(option)) is string missing)
        {
            return Invalid(missing);
        }

        string? vault = given.GetValueOrDefault(VaultOption);
        string? to = given.GetValueOrDefault(ToOption);
        string? instance = given.GetValueOrDefault(InstanceOption);
        string? account = given.GetValueOrDefault(AccountOption);
        if (vault is not null && !Names.IsVaultTitle(vault))
        {
            return Invalid(VaultOption);
        }

        if (to is not null && !IsDestination(to))
        {
            return Invalid(ToOption);
        }

        if (instance is not null && !Names.IsName(instance))
        {
            return Invalid(InstanceOption);
        }

        if (account is not null && !IsAccount(account))
        {
            return Invalid(AccountOption);
        }

        return Result.Success(new ToolArguments(arguments[0], vault, to, instance, account, given.ContainsKey(ReplaceOption)));
    }

    /// <summary>An SSH destination: 1 to 255 ASCII letters, digits, <c>.</c>, <c>-</c>, <c>_</c> and one <c>@</c> at most, not starting with <c>-</c> (<c>07</c> section 3).</summary>
    internal static bool IsDestination(string text)
    {
        return IsWithin(text) && text.Count(character => character == '@') <= 1;
    }

    /// <summary>An account: 1 to 255 ASCII letters, digits, <c>.</c>, <c>-</c>, <c>_</c> and <c>@</c>, not starting with <c>-</c> (<c>07</c> section 3).</summary>
    internal static bool IsAccount(string text)
    {
        return IsWithin(text);
    }

    private static bool IsWithin(string text)
    {
        return text.Length is >= 1 and <= MostLength
            && text[0] != '-'
            && text.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' or '@');
    }

    private static Result<ToolArguments> Invalid(string option)
    {
        return Result.Fail<ToolArguments>(new Failure { Code = Codes.CliInputInvalid, Details = new Dictionary<string, string> { ["member"] = option } });
    }
}