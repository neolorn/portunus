using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// The client's configuration (<c>05</c> PRT-OPS-001): each key of <c>Portunus:Client</c>
/// checked on its raw value against its rule of <c>07</c> section 4, then bound by the
/// configuration binder's source generator and validated by the generated validator. An
/// absent key takes its default.
/// </summary>
internal static class ClientConfiguration
{
    private const int SocketPathBytes = 107;

    /// <summary>Reads the client's configuration from <paramref name="configuration"/>, the host's whole configuration.</summary>
    /// <exception cref="SecretsStartupException">A key's value is outside its rule: <c>secrets.startup.configurationinvalid</c>, its detail <c>key</c> the key.</exception>
    internal static ClientOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string? source = configuration[ClientKeys.Source];
        if (source is not (null or "agent" or "file"))
        {
            throw Invalid(ClientKeys.Source);
        }

        CheckPath(configuration, ClientKeys.Socket, SocketPathBytes);
        CheckPath(configuration, ClientKeys.File, int.MaxValue);
        if (source is "file" && configuration[ClientKeys.File] is null)
        {
            throw Invalid(ClientKeys.File);
        }

        CheckDuration(configuration, ClientKeys.Timeout, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        CheckDuration(configuration, ClientKeys.WaitTimeout, TimeSpan.Zero, TimeSpan.FromHours(1));
        CheckDuration(configuration, ClientKeys.PollInterval, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        ClientOptions options = configuration.GetSection(ClientKeys.Section).Get<ClientOptions>() ?? new ClientOptions();
        ValidateOptionsResult validation = new ClientOptionsValidator().Validate(null, options);
        if (validation.Failed)
        {
            throw new InvalidOperationException("The client's configuration passed its rules before it was bound: " + validation.FailureMessage);
        }

        return options;
    }

    /// <summary>A path of the platform's own absolute form, of at most <paramref name="maximumBytes"/> bytes in UTF-8.</summary>
    private static void CheckPath(IConfiguration configuration, string key, int maximumBytes)
    {
        string? path = configuration[key];
        if (path is not null && (!Path.IsPathFullyQualified(path) || Encoding.UTF8.GetByteCount(path) > maximumBytes))
        {
            throw Invalid(key);
        }
    }

    /// <summary>A duration in .NET's <see cref="TimeSpan"/> form, from <paramref name="least"/> to <paramref name="most"/>.</summary>
    private static void CheckDuration(IConfiguration configuration, string key, TimeSpan least, TimeSpan most)
    {
        string? text = configuration[key];
        if (text is not null && !(TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan duration) && duration >= least && duration <= most))
        {
            throw Invalid(key);
        }
    }

    private static SecretsStartupException Invalid(string key)
    {
        return new SecretsStartupException(Codes.StartupConfigurationInvalid, new Dictionary<string, string> { ["key"] = key }.ToFrozenDictionary(StringComparer.Ordinal));
    }
}