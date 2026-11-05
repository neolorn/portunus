using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Portunus.Repository.Tests;

/// <summary>
/// The packages <c>Janus.Hosting</c> 1.0.0-rc.4 brings to <c>Portunus.Janus</c>, each with its
/// licence as its package declares it (<c>06</c> PRT-ENG-009, P-036), and the patched versions
/// pinned beside it (P-038). The adapter's lockfile may hold no other package but those of
/// PRT-ENG-009's table and the library's own projects.
/// </summary>
internal static class JanusClosure
{
    /// <summary>The packages pinned at their patched versions by central transitive pinning (P-038), each with its licence.</summary>
    internal static FrozenDictionary<string, string> Pinned { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["System.Security.Cryptography.Pkcs"] = "MIT",
        ["System.Security.Cryptography.Xml"] = "MIT",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The packages <c>Janus.Hosting</c> takes, each with its licence; <c>Otp.NET</c> declares its licence by its file, MIT.</summary>
    internal static FrozenDictionary<string, string> Taken { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Dapper"] = "Apache-2.0",
        ["Fido2"] = "MIT",
        ["Fido2.Models"] = "MIT",
        ["Konscious.Security.Cryptography.Argon2"] = "MIT",
        ["Konscious.Security.Cryptography.Blake2"] = "MIT",
        ["Microsoft.AspNetCore.Cryptography.Internal"] = "MIT",
        ["Microsoft.AspNetCore.DataProtection"] = "MIT",
        ["Microsoft.AspNetCore.DataProtection.Abstractions"] = "MIT",
        ["Microsoft.Bcl.Cryptography"] = "MIT",
        ["Microsoft.Bcl.Memory"] = "MIT",
        ["Microsoft.EntityFrameworkCore"] = "MIT",
        ["Microsoft.EntityFrameworkCore.Abstractions"] = "MIT",
        ["Microsoft.EntityFrameworkCore.Analyzers"] = "MIT",
        ["Microsoft.EntityFrameworkCore.Relational"] = "MIT",
        ["Microsoft.Extensions.AmbientMetadata.Application"] = "MIT",
        ["Microsoft.Extensions.Caching.Abstractions"] = "MIT",
        ["Microsoft.Extensions.Caching.Memory"] = "MIT",
        ["Microsoft.Extensions.Compliance.Abstractions"] = "MIT",
        ["Microsoft.Extensions.Configuration"] = "MIT",
        ["Microsoft.Extensions.DependencyInjection"] = "MIT",
        ["Microsoft.Extensions.DependencyInjection.AutoActivation"] = "MIT",
        ["Microsoft.Extensions.Diagnostics"] = "MIT",
        ["Microsoft.Extensions.Diagnostics.Abstractions"] = "MIT",
        ["Microsoft.Extensions.Diagnostics.ExceptionSummarization"] = "MIT",
        ["Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions"] = "MIT",
        ["Microsoft.Extensions.FileProviders.Abstractions"] = "MIT",
        ["Microsoft.Extensions.Http"] = "MIT",
        ["Microsoft.Extensions.Http.Diagnostics"] = "MIT",
        ["Microsoft.Extensions.Http.Polly"] = "MIT",
        ["Microsoft.Extensions.Http.Resilience"] = "MIT",
        ["Microsoft.Extensions.Logging"] = "MIT",
        ["Microsoft.Extensions.Logging.Configuration"] = "MIT",
        ["Microsoft.Extensions.ObjectPool"] = "MIT",
        ["Microsoft.Extensions.Primitives"] = "MIT",
        ["Microsoft.Extensions.Resilience"] = "MIT",
        ["Microsoft.Extensions.Telemetry"] = "MIT",
        ["Microsoft.Extensions.Telemetry.Abstractions"] = "MIT",
        ["Microsoft.IdentityModel.Abstractions"] = "MIT",
        ["Microsoft.IdentityModel.JsonWebTokens"] = "MIT",
        ["Microsoft.IdentityModel.Logging"] = "MIT",
        ["Microsoft.IdentityModel.Protocols"] = "MIT",
        ["Microsoft.IdentityModel.Tokens"] = "MIT",
        ["NSec.Cryptography"] = "MIT",
        ["Npgsql"] = "PostgreSQL",
        ["Npgsql.EntityFrameworkCore.PostgreSQL"] = "PostgreSQL",
        ["OpenIddict"] = "Apache-2.0",
        ["OpenIddict.Abstractions"] = "Apache-2.0",
        ["OpenIddict.AspNetCore"] = "Apache-2.0",
        ["OpenIddict.Client"] = "Apache-2.0",
        ["OpenIddict.Client.AspNetCore"] = "Apache-2.0",
        ["OpenIddict.Client.DataProtection"] = "Apache-2.0",
        ["OpenIddict.Client.SystemIntegration"] = "Apache-2.0",
        ["OpenIddict.Client.SystemNetHttp"] = "Apache-2.0",
        ["OpenIddict.Client.WebIntegration"] = "Apache-2.0",
        ["OpenIddict.Core"] = "Apache-2.0",
        ["OpenIddict.Server"] = "Apache-2.0",
        ["OpenIddict.Server.AspNetCore"] = "Apache-2.0",
        ["OpenIddict.Server.DataProtection"] = "Apache-2.0",
        ["OpenIddict.Validation"] = "Apache-2.0",
        ["OpenIddict.Validation.AspNetCore"] = "Apache-2.0",
        ["OpenIddict.Validation.DataProtection"] = "Apache-2.0",
        ["OpenIddict.Validation.ServerIntegration"] = "Apache-2.0",
        ["OpenIddict.Validation.SystemNetHttp"] = "Apache-2.0",
        ["Otp.NET"] = "MIT",
        ["Polly"] = "BSD-3-Clause",
        ["Polly.Core"] = "BSD-3-Clause",
        ["Polly.Extensions"] = "BSD-3-Clause",
        ["Polly.Extensions.Http"] = "BSD-3-Clause",
        ["Polly.RateLimiting"] = "BSD-3-Clause",
        ["System.Formats.Cbor"] = "MIT",
        ["System.Threading.RateLimiting"] = "MIT",
        ["libsodium"] = "ISC",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Answers each package of <c>Portunus.Janus</c>'s lockfile in the repository at <paramref name="root"/> that is neither in PRT-ENG-009's table, nor in the closure, nor pinned.</summary>
    internal static IReadOnlyList<string> Violations(string root)
    {
        string lockFile = Path.Combine(root, "src", Packages.Janus, "packages.lock.json");
        if (!File.Exists(lockFile))
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockFile));
        return [.. document.RootElement.GetProperty("dependencies").EnumerateObject()
            .SelectMany(framework => framework.Value.EnumerateObject())
            .Where(package => !string.Equals(package.Value.GetProperty("type").GetString(), "Project", StringComparison.Ordinal))
            .Select(package => package.Name)
            .Distinct(StringComparer.Ordinal)
            .Where(package => !PackageRules.Allowed.Contains(package, StringComparer.Ordinal) && !Taken.ContainsKey(package) && !Pinned.ContainsKey(package))
            .Order(StringComparer.Ordinal)
            .Select(package => $"src/{Packages.Janus}/packages.lock.json takes {package}, which is neither in PRT-ENG-009's table nor in Janus.Hosting's closure.")];
    }
}