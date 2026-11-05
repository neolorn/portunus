using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Portunus.Core;

namespace Portunus.Agent;

/// <summary>
/// The agent's configuration (<c>01</c> PRT-AGT-002, <c>05</c> PRT-OPS-001): each key of
/// <c>Portunus:Agent</c> checked on its raw value against its rule of <c>07</c> section 4, each
/// declaration against <c>01</c> PRT-AGT-007's, then bound by the configuration binder's source
/// generator and validated by the generated validator. An absent key takes its default.
/// </summary>
internal static class AgentConfiguration
{
    private const string Section = "Portunus:Agent";

    private const string Processes = "Portunus:Agent:Processes";

    private const int SocketPathBytes = 107;

    private const int MostDeclarations = 64;

    private const int MostAddresses = 256;

    /// <summary>
    /// Reads the configuration of <paramref name="instance"/> from <paramref name="file"/>,
    /// answering <c>secrets.agent.configurationinvalid</c> naming a key outside its rule, or
    /// <c>secrets.agent.declarationinvalid</c> naming a declaration's process and member.
    /// </summary>
    internal static Result<AgentSettings> Read(string file, string instance)
    {
        ArgumentNullException.ThrowIfNull(file);
        IConfiguration configuration;
        try
        {
            configuration = new ConfigurationBuilder().AddJsonFile(file, optional: false, reloadOnChange: false).Build();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException or UnauthorizedAccessException)
        {
            // A file that cannot be read as JSON holds no key within its rule.
            return Invalid(Section);
        }

        IConfigurationSection processes = configuration.GetSection(Processes);
        string[] names = [.. processes.GetChildren().Select(child => child.Key).Order(StringComparer.Ordinal)];
        if (CheckKeys(configuration, instance, names) is Failure keys)
        {
            return Result.Fail<AgentSettings>(keys);
        }

        if (CheckDeclarations(processes, names) is Failure declarations)
        {
            return Result.Fail<AgentSettings>(declarations);
        }

        AgentOptions options = configuration.GetSection(Section).Get<AgentOptions>() ?? new AgentOptions();
        options.TokenFile ??= Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY") is string credentials ? Path.Combine(credentials, "token") : null;
        ValidateOptionsResult validation = new AgentOptionsValidator().Validate(null, options);
        if (validation.Failed)
        {
            throw new InvalidOperationException("The agent's configuration passed its rules before it was bound: " + validation.FailureMessage);
        }

        Declaration[] declared =
        [
            .. options.Processes.OrderBy(process => process.Key, StringComparer.Ordinal).Select(process => new Declaration(
                process.Key,
                process.Value.Uid,
                process.Value.Gid,
                process.Value.Secrets.Select(SecretAddress.Parse).ToFrozenSet())),
        ];
        return Result.Success(new AgentSettings(instance, options, declared));
    }

    private static Failure? CheckKeys(IConfiguration configuration, string instance, IReadOnlyList<string> processes)
    {
        string? vault = configuration["Portunus:Agent:Vault"];
        if (!Names.IsVaultTitle(vault))
        {
            return InvalidKey("Portunus:Agent:Vault");
        }

        if (configuration["Portunus:Agent:Source"] is not (null or "vault" or "push"))
        {
            return InvalidKey("Portunus:Agent:Source");
        }

        string? root = configuration["Portunus:Agent:Root"];
        if (root is not null && !Path.IsPathFullyQualified(root))
        {
            return InvalidKey("Portunus:Agent:Root");
        }

        // Each socket's path, <root>/<instance>/<process>/agent.sock, is at most 107 bytes.
        string under = Path.Combine(root ?? "/run/portunus", instance);
        if (processes.Append(AgentNames.PushDirectory).Any(process => Encoding.UTF8.GetByteCount(Path.Combine(under, process, AgentNames.Socket)) > SocketPathBytes))
        {
            return InvalidKey("Portunus:Agent:Root");
        }

        foreach (string key in new[] { "Portunus:Agent:TokenFile", "Portunus:Agent:OpPath" })
        {
            if (configuration[key] is string path && !Path.IsPathFullyQualified(path))
            {
                return InvalidKey(key);
            }
        }

        if (!IsDuration(configuration["Portunus:Agent:OpTimeout"], TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5)))
        {
            return InvalidKey("Portunus:Agent:OpTimeout");
        }

        if (!IsDuration(configuration["Portunus:Agent:Retry:First"], TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10)))
        {
            return InvalidKey("Portunus:Agent:Retry:First");
        }

        TimeSpan first = configuration["Portunus:Agent:Retry:First"] is string text ? TimeSpan.ParseExact(text, "c", CultureInfo.InvariantCulture) : TimeSpan.FromSeconds(30);
        if (!IsDuration(configuration["Portunus:Agent:Retry:Max"], first, TimeSpan.FromDays(1)))
        {
            return InvalidKey("Portunus:Agent:Retry:Max");
        }

        // The default of Max is within its rule only where First is at most an hour.
        if (configuration["Portunus:Agent:Retry:Max"] is null && first > TimeSpan.FromHours(1))
        {
            return InvalidKey("Portunus:Agent:Retry:Max");
        }

        return IsIdentifier(configuration["Portunus:Agent:PushGroup"]) ? null : InvalidKey("Portunus:Agent:PushGroup");
    }

    private static Failure? CheckDeclarations(IConfigurationSection processes, string[] names)
    {
        if (names.Length is 0 or > MostDeclarations)
        {
            return InvalidDeclaration(names.Length > MostDeclarations ? names[MostDeclarations] : string.Empty, "Processes");
        }

        foreach (string name in names)
        {
            IConfigurationSection declaration = processes.GetSection(name);
            if (!Names.IsName(name) || name == AgentNames.PushDirectory)
            {
                return InvalidDeclaration(name, "name");
            }

            foreach (string member in new[] { "Uid", "Gid" })
            {
                if (!IsIdentifier(declaration[member]))
                {
                    return InvalidDeclaration(name, member);
                }
            }

            // Secrets is a list: its members are the section's children, and a plain value is none.
            IConfigurationSection secrets = declaration.GetSection("Secrets");
            string?[] addresses = [.. secrets.GetChildren().Select(child => child.Value)];
            HashSet<SecretAddress> seen = [];
            bool valid = secrets.Value is null && addresses.Length is > 0 and <= MostAddresses
                && addresses.All(address => SecretAddress.TryParse(address, out SecretAddress parsed) && seen.Add(parsed));
            if (!valid)
            {
                return InvalidDeclaration(name, "Secrets");
            }
        }

        return null;
    }

    private static bool IsDuration(string? text, TimeSpan least, TimeSpan most)
    {
        return text is null || (TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan duration) && duration >= least && duration <= most);
    }

    /// <summary>A user or group identifier: 1 to 4,294,967,294 in decimal digits.</summary>
    private static bool IsIdentifier(string? text)
    {
        return text is not null && text.Length > 0 && text.All(char.IsAsciiDigit)
            && uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint identifier) && identifier is >= 1 and <= 4294967294;
    }

    private static Result<AgentSettings> Invalid(string key)
    {
        return Result.Fail<AgentSettings>(InvalidKey(key));
    }

    private static Failure InvalidKey(string key)
    {
        return new Failure { Code = Codes.AgentConfigurationInvalid, Details = Details(("key", key)) };
    }

    private static Failure InvalidDeclaration(string process, string member)
    {
        return new Failure { Code = Codes.AgentDeclarationInvalid, Details = Details(("process", process), ("member", member)) };
    }

    private static FrozenDictionary<string, string> Details(params (string Name, string Value)[] details)
    {
        return details.ToFrozenDictionary(detail => detail.Name, detail => detail.Value, StringComparer.Ordinal);
    }
}