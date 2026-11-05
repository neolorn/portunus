using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus.Agent.Fetch;

/// <summary>
/// One fetch of the environment's set (<c>01</c> PRT-AGT-003): <c>op item list</c> naming the
/// vault by its title, then <c>op item get</c> for each item, naming it and the vault by
/// identifier, so a vault of <c>n</c> items costs <c>3 + n</c> reads; the set is then checked
/// against the layout, kept whole where it passes and not at all where it fails.
/// </summary>
internal sealed class Fetcher(AgentSettings settings, TimeProvider time, ILogger log)
{
    /// <summary>Fetches once with <paramref name="token"/>.</summary>
    internal async Task<FetchOutcome> FetchAsync(int attempt, ReadOnlyMemory<byte> token, CancellationToken cancellationToken)
    {
        AgentLog.FetchStarted(log, attempt);
        string[] listCommand = ["item", "list", "--vault", settings.Options.Vault];
        OpOutcome list = await RunAsync(listCommand, token, cancellationToken).ConfigureAwait(false);
        if (list.Output is not PinnedDocument listed)
        {
            return FetchOutcome.Failed("list", list);
        }

        (string Vault, IReadOnlyList<(string Id, string Title)> Items)? listing;
        using (listed)
        {
            listing = OpJson.ReadList(listed.Span);
        }

        if (listing is not (string vault, IReadOnlyList<(string Id, string Title)> entries))
        {
            return FetchOutcome.Failed("list", new OpOutcome(0, null, FailureClass.Unreachable));
        }

        AgentLog.FetchListed(log, attempt, entries.Count, 3 + entries.Count);
        PinnedValues? values = new();
        try
        {
            List<VaultItem> items = [];
            foreach ((string id, string _) in entries)
            {
                string[] getting = ["item", "get", id, "--vault", vault, "--reveal"];
                OpOutcome get = await RunAsync(getting, token, cancellationToken).ConfigureAwait(false);
                if (get.Output is not PinnedDocument got)
                {
                    return FetchOutcome.Failed("get", get);
                }

                using (got)
                {
                    if (OpJson.ReadItem(got.Span, values) is not VaultItem item)
                    {
                        return FetchOutcome.Failed("get", new OpOutcome(0, null, FailureClass.Unreachable));
                    }

                    items.Add(item);
                }
            }

            VaultSet set = new(settings.Options.Vault, items);
            if (LayoutValidator.FirstError(LayoutValidator.Check(set)) is LayoutFinding error)
            {
                return new FetchOutcome(null, null, null, null, error);
            }

            FetchOutcome fetched = new(set, values, null, null, null);
            values = null;
            return fetched;
        }
        finally
        {
            values?.Dispose();
        }
    }

    private Task<OpOutcome> RunAsync(IReadOnlyList<string> command, ReadOnlyMemory<byte> token, CancellationToken cancellationToken)
    {
        string[] arguments = [.. command, "--format", "json", "--config", settings.OpDirectory];
        return OpRunner.RunAsync(settings.Options.OpPath, arguments, token, settings.OpDirectory, settings.Options.OpTimeout, time, cancellationToken);
    }
}

/// <summary>
/// A fetch's outcome: the set and the owner of its values where it succeeded; the command and
/// its failure where one failed; the layout's first error where the set broke the layout.
/// </summary>
/// <param name="Set">The set, where the fetch succeeded.</param>
/// <param name="Values">The owner of the set's values, the caller's.</param>
/// <param name="Command">The command that failed: <c>list</c> or <c>get</c>.</param>
/// <param name="Failure">The failed command's outcome.</param>
/// <param name="Layout">The layout's first error, where the set broke it.</param>
internal sealed record FetchOutcome(VaultSet? Set, PinnedValues? Values, string? Command, OpOutcome? Failure, LayoutFinding? Layout)
{
    internal static FetchOutcome Failed(string command, OpOutcome failure)
    {
        return new FetchOutcome(null, null, command, failure, null);
    }
}