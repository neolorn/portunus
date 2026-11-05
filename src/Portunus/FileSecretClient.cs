using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// The client of a Development host whose source is <c>file</c> (<c>04</c> PRT-LIB-005): the
/// development file (<c>03</c> PRT-VLT-008), read once into pinned arrays and checked by the
/// layout's check, answers each read as the agent would, every address allowed. Its arrays
/// are zeroed when it is disposed.
/// </summary>
internal sealed class FileSecretClient : ISecretClient, IDisposable
{
    private readonly VaultSet _set;

    private readonly PinnedValues _values;

    private readonly DateTimeOffset _loadedAt;

    private readonly ILogger _log;

    private FileSecretClient(VaultSet set, PinnedValues values, DateTimeOffset loadedAt, ILogger log)
    {
        _set = set;
        _values = values;
        _loadedAt = loadedAt;
        _log = log;
    }

    /// <summary>Reads the development file at <paramref name="path"/>.</summary>
    /// <exception cref="SecretsStartupException">The file cannot be read, is not JSON or breaks the layout: <c>secrets.startup.fileinvalid</c>, naming the layout's first finding's item and field where it names them.</exception>
    internal static FileSecretClient Read(string path, TimeProvider time, ILogger log)
    {
        byte[] document = ReadAll(path) ?? throw FileInvalid(null);
        PinnedValues? values = null;
        try
        {
            VaultSet set = DevelopmentFile.Read(document, out values) ?? throw FileInvalid(null);
            if (LayoutValidator.FirstError(LayoutValidator.Check(set)) is LayoutFinding error)
            {
                throw FileInvalid(error);
            }

            FileSecretClient client = new(set, values, time.GetUtcNow(), log);
            values = null;
            return client;
        }
        finally
        {
            values?.Dispose();
            CryptographicOperations.ZeroMemory(document);
        }
    }

    public ValueTask<Result<Secret>> ReadAsync(SecretAddress address, CancellationToken cancellationToken)
    {
        if (_set.Find(address) is not Secret found)
        {
            ClientLog.ReadFailed(_log, address.ToString(), Codes.SecretUnknown);
            return ValueTask.FromResult(Result.Fail<Secret>(new Failure { Code = Codes.SecretUnknown }));
        }

        return ValueTask.FromResult(Result.Success(OwnedSecret.Copy(found.Current, found.Versions)));
    }

    public ValueTask<Result<AgentStatus>> StatusAsync(CancellationToken cancellationToken)
    {
        AgentStatus status = new() { State = AgentState.Ready, Since = _loadedAt, Source = SetSource.Vault, LoadedAt = _loadedAt, Items = _set.Items.Count };
        return ValueTask.FromResult(Result.Success(status));
    }

    public void Dispose()
    {
        _values.Dispose();
    }

    /// <summary>The file's bytes in a pinned array, or none where it cannot be read whole.</summary>
    private static byte[]? ReadAll(string path)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            long length = RandomAccess.GetLength(handle);
            if (length > Array.MaxLength)
            {
                return null;
            }

            byte[] document = GC.AllocateArray<byte>((int)length, pinned: true);
            int read = 0;
            while (read < document.Length)
            {
                int count = RandomAccess.Read(handle, document.AsSpan(read), read);
                if (count == 0)
                {
                    CryptographicOperations.ZeroMemory(document);
                    return null;
                }

                read += count;
            }

            return document;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static SecretsStartupException FileInvalid(LayoutFinding? finding)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal);
        if (finding?.Item is string item)
        {
            details["item"] = item;
        }

        if (finding?.Field is string field)
        {
            details["field"] = field;
        }

        return new SecretsStartupException(Codes.StartupFileInvalid, details.ToFrozenDictionary(StringComparer.Ordinal));
    }
}