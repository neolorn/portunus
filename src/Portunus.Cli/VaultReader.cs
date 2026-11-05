using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus.Cli;

/// <summary>
/// Reads a vault through the owner's own 1Password app (<c>02</c> PRT-PSH-002 steps 1 to 3):
/// <c>op --version</c>, refused below the pinned version or of another major version; then
/// <c>op item list</c> naming the vault by its title and <c>op item get</c> for each item naming
/// it and the vault by identifier, each with <c>--format json</c>, and <c>--account</c> where
/// it is given. Values go into pinned arrays the caller owns.
/// </summary>
internal static class VaultReader
{
    /// <summary>The lowest <c>op</c> the tool runs (<c>07</c> section 7): 2.41.1, or a later 2.x.</summary>
    private static readonly Version Pinned = new(2, 41, 1);

    /// <summary>Answers <c>secrets.cli.opversion</c>, naming what <c>op --version</c> answered, empty where it did not run, unless it is the pinned version or a later one of its major version.</summary>
    internal static async Task<Failure?> CheckVersionAsync(ToolEnvironment environment, CancellationToken cancellationToken)
    {
        string found = string.Empty;
        using (PinnedDocument? output = await OpCommand.RunAsync(environment, ["--version"], cancellationToken).ConfigureAwait(false))
        {
            if (output is not null)
            {
                found = Encoding.UTF8.GetString(output.Span).Trim();
            }
        }

        return IsSupported(found) ? null : new Failure { Code = Codes.CliOpVersion, Details = new Dictionary<string, string> { ["found"] = found } };
    }

    /// <summary>Whether <paramref name="found"/> is a version of three numbers, of the pinned one's major version and not below it.</summary>
    internal static bool IsSupported(string found)
    {
        string[] parts = found.Split('.');
        if (parts.Length != 3 || !Array.TrueForAll(parts, part => part.Length is > 0 and <= 9 && part.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0))
        {
            return false;
        }

        Version version = new(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture));
        return version.Major == Pinned.Major && version >= Pinned;
    }

    /// <summary>Reads <paramref name="vault"/>, or answers <c>secrets.cli.opfailed</c> naming the step that failed.</summary>
    internal static async Task<Result<ReadVault>> ReadAsync(ToolEnvironment environment, string vault, string? account, CancellationToken cancellationToken)
    {
        (string Vault, IReadOnlyList<(string Id, string Title)> Items)? listing;
        using (PinnedDocument? listed = await OpCommand.RunAsync(environment, Command(["item", "list", "--vault", vault], account), cancellationToken).ConfigureAwait(false))
        {
            listing = listed is null ? null : OpJson.ReadList(listed.Span);
        }

        if (listing is not (string vaultId, IReadOnlyList<(string Id, string Title)> entries))
        {
            return Failed("list");
        }

        PinnedValues? values = new();
        try
        {
            List<VaultItem> items = [];
            foreach ((string id, string _) in entries)
            {
                using PinnedDocument? got = await OpCommand.RunAsync(environment, Command(["item", "get", id, "--vault", vaultId], account, reveal: true), cancellationToken).ConfigureAwait(false);
                if (got is null || OpJson.ReadItem(got.Span, values) is not VaultItem item)
                {
                    return Failed("get");
                }

                items.Add(item);
            }

            ReadVault read = new(new VaultSet(vault, items), values);
            values = null;
            return Result.Success(read);
        }
        finally
        {
            values?.Dispose();
        }
    }

    private static string[] Command(string[] command, string? account, bool reveal = false)
    {
        List<string> arguments = [.. command, "--format", "json"];
        if (reveal)
        {
            arguments.Add("--reveal");
        }

        if (account is not null)
        {
            arguments.Add("--account");
            arguments.Add(account);
        }

        return [.. arguments];
    }

    private static Result<ReadVault> Failed(string step)
    {
        return Result.Fail<ReadVault>(new Failure { Code = Codes.CliOpFailed, Details = new Dictionary<string, string> { ["step"] = step } });
    }
}

/// <summary>A vault read: its set, and the owner of the set's values, which the caller disposes.</summary>
/// <param name="Set">The set.</param>
/// <param name="Values">The owner of its values.</param>
internal sealed record ReadVault(VaultSet Set, PinnedValues Values);