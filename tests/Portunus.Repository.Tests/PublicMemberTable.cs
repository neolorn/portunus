using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portunus.Repository.Tests;

/// <summary>
/// The members the tables of <c>04</c> PRT-LIB-002 give each public type, by part, as the
/// public API files write them, with the types the tables give. A part whose types come in a
/// later phase holds none yet.
/// </summary>
internal static class PublicMemberTable
{
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> Members = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] =
        [
            "Portunus.Core.SecretAddress.SecretAddress(string! Item, string! Name) -> void",
            "static Portunus.Core.SecretAddress.Parse(string! address) -> Portunus.Core.SecretAddress",
            "override Portunus.Core.SecretAddress.ToString() -> string!",
            "Portunus.Core.SecretAddress.Equals(Portunus.Core.SecretAddress other) -> bool",
            "Portunus.Core.SecretAddress.Item.get -> string!",
            "Portunus.Core.SecretAddress.Name.get -> string!",
            "Portunus.Core.Secret.Current.get -> int",
            "Portunus.Core.Secret.Versions.get -> System.Collections.Generic.IReadOnlyDictionary<int, System.ReadOnlyMemory<byte>>!",
            "Portunus.Core.Secret.CurrentValue.get -> System.ReadOnlyMemory<byte>",
            "Portunus.Core.AgentStatus.State.get -> Portunus.Core.AgentState",
            "Portunus.Core.AgentStatus.Reason.get -> Portunus.Core.WaitReason?",
            "Portunus.Core.AgentStatus.Since.get -> System.DateTimeOffset",
            "Portunus.Core.AgentStatus.Source.get -> Portunus.Core.SetSource?",
            "Portunus.Core.AgentStatus.LoadedAt.get -> System.DateTimeOffset?",
            "Portunus.Core.AgentStatus.Items.get -> int",
            "Portunus.Core.Result.IsSuccess.get -> bool",
            "Portunus.Core.Result.Failure.get -> Portunus.Core.Failure?",
            "static Portunus.Core.Result.Success() -> Portunus.Core.Result!",
            "static Portunus.Core.Result.Fail(Portunus.Core.Failure! failure) -> Portunus.Core.Result!",
            "static Portunus.Core.Result.Success<T>(T value) -> Portunus.Core.Result<T>!",
            "static Portunus.Core.Result.Fail<T>(Portunus.Core.Failure! failure) -> Portunus.Core.Result<T>!",
            "Portunus.Core.Result<T>.IsSuccess.get -> bool",
            "Portunus.Core.Result<T>.Value.get -> T?",
            "Portunus.Core.Result<T>.Failure.get -> Portunus.Core.Failure?",
            "Portunus.Core.Failure.Code.get -> string!",
            "Portunus.Core.Failure.Details.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!",
            "Portunus.Core.SecretsStartupException.Code.get -> string!",
            "Portunus.Core.SecretsStartupException.Details.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!",
            "Portunus.Core.ISecretClient.ReadAsync(Portunus.Core.SecretAddress address, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<Portunus.Core.Result<Portunus.Core.Secret!>!>",
            "Portunus.Core.ISecretClient.StatusAsync(System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<Portunus.Core.Result<Portunus.Core.AgentStatus!>!>",
        ],
        [Packages.Testing] =
        [
            "Portunus.Testing.FixedSecretClient.FixedSecretClient() -> void",
            "Portunus.Testing.FixedSecretClient.Set(Portunus.Core.SecretAddress address, Portunus.Core.Secret! secret) -> void",
            "Portunus.Testing.FixedSecretClient.Remove(Portunus.Core.SecretAddress address) -> void",
            "Portunus.Testing.FixedSecretClient.Deny(Portunus.Core.SecretAddress address) -> void",
            "Portunus.Testing.FixedSecretClient.Wait(Portunus.Core.WaitReason reason) -> void",
            "Portunus.Testing.FixedSecretClient.Release() -> void",
            "Portunus.Testing.FixedSecretClient.Reads.get -> System.Collections.Generic.IReadOnlyList<Portunus.Core.SecretAddress>!",
            "Portunus.Testing.FixedSecretClient.ReadAsync(Portunus.Core.SecretAddress address, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<Portunus.Core.Result<Portunus.Core.Secret!>!>",
            "Portunus.Testing.FixedSecretClient.StatusAsync(System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<Portunus.Core.Result<Portunus.Core.AgentStatus!>!>",
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Answers each member the tables give <paramref name="part"/> that the public API files of <paramref name="project"/> do not list.</summary>
    internal static IEnumerable<string> Violations(string part, string project)
    {
        HashSet<string> listed = new(StringComparer.Ordinal);
        foreach (string file in new[] { "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt" })
        {
            string path = Path.Combine(project, file);
            if (File.Exists(path))
            {
                listed.UnionWith(File.ReadLines(path).Select(line => line.Trim()));
            }
        }

        return Members.GetValueOrDefault(part, [])
            .Where(member => !listed.Contains(member))
            .Select(member => $"{part}'s public API files do not list {member}, which 04 PRT-LIB-002 gives.");
    }
}