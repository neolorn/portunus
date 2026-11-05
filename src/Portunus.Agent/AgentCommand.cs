using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Portunus.Agent.Fetch;
using Portunus.Agent.Serving;
using Portunus.Core;

namespace Portunus.Agent;

/// <summary>
/// The commands of <c>portunus-agent</c> (<c>07</c> section 3): <c>run &lt;instance&gt;</c>
/// and <c>status &lt;instance&gt;</c>. A refusal is one JSON line on standard error and exit
/// code 1; a fault that carries no code is <c>secrets.fault</c> the same way (<c>06</c>
/// PRT-ENG-003). The shipped entry point passes the real system paths; only tests give others
/// (P-027).
/// </summary>
internal static class AgentCommand
{
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(5);


    /// <summary>Runs the command <paramref name="arguments"/> name, and answers its exit code.</summary>
    internal static async Task<int> MainAsync(IReadOnlyList<string> arguments, AgentPaths paths, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            return arguments switch
            {
                ["run", string instance] when Names.IsName(instance) => await RunAsync(instance, paths, error).ConfigureAwait(false),
                ["status", string instance] when Names.IsName(instance) => await StatusAsync(instance, paths, output, error).ConfigureAwait(false),
                _ => Refuse(error, new Failure { Code = Codes.Fault }),
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A fault is written with no detail, its message never shown, since it may name a path.
            return Refuse(error, new Failure { Code = Codes.Fault });
        }
    }

    /// <summary>Checks the start of <paramref name="instance"/>, then runs the agent until its stop (<c>01</c> PRT-AGT-001, PRT-AGT-002).</summary>
    private static async Task<int> RunAsync(string instance, AgentPaths paths, TextWriter error)
    {
        Native.MarkNotDumpable();
        if (StartChecks.Swap(paths.Swaps) is Failure swap)
        {
            return Refuse(error, swap);
        }

        Result<AgentSettings> read = Configuration(instance, paths);
        if (read.Value is not AgentSettings settings)
        {
            return Refuse(error, read.Failure!);
        }

        byte[]? token = null;
        if (settings.Options.Source == AgentSource.Vault)
        {
            if (await OpVersionAsync(settings).ConfigureAwait(false) is Failure version)
            {
                return Refuse(error, version);
            }

            token = TokenFile.Read(settings.Options.TokenFile);
            if (token is null)
            {
                return Refuse(error, new Failure { Code = Codes.AgentTokenUnavailable });
            }
        }

        AgentToken held = new(token);
        try
        {
            using IHost host = BuildHost(settings, held);
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }
        finally
        {
            held.Drop();
        }
    }

    /// <summary>Builds the agent's host, with the generic host's handling of <c>SIGTERM</c> and the systemd console format of the journal.</summary>
    /// <param name="settings">The checked configuration.</param>
    /// <param name="token">The token, while held.</param>
    /// <param name="replace">What a test registers over the agent's own, its clock or a log's capture; the entry point passes none (P-027).</param>
    internal static IHost BuildHost(AgentSettings settings, AgentToken token, Action<IServiceCollection>? replace = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddSystemd();
        builder.Logging.AddSystemdConsole();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(token);
        builder.Services.AddSingleton(provider => new SetHolder(provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(provider => new AgentServer(settings, provider.GetRequiredService<SetHolder>(), provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<AgentServer>>()));
        builder.Services.AddSingleton(provider => new Fetcher(settings, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<Fetcher>>()));

        // The pauses' spread comes from one generator, seeded once (06 PRT-ENG-004).
        builder.Services.AddSingleton(_ => new RetryPolicy(settings.Options.Retry.First, settings.Options.Retry.Max, new Random()));
        builder.Services.AddHostedService<AgentService>();
        replace?.Invoke(builder.Services);
        return builder.Build();
    }

    /// <summary>The configuration of <paramref name="instance"/>, its file checked as a secret's grant is first (<c>05</c> PRT-OPS-001).</summary>
    private static Result<AgentSettings> Configuration(string instance, AgentPaths paths)
    {
        string file = Path.Combine(paths.ConfigurationDirectory, instance + ".json");
        return StartChecks.Configuration(file) is Failure unsafeFile ? Result.Fail<AgentSettings>(unsafeFile) : AgentConfiguration.Read(file, instance);
    }

    /// <summary>Answers <c>secrets.agent.opversion</c> where <c>op --version</c> does not answer the pinned version, naming what it answered.</summary>
    private static async Task<Failure?> OpVersionAsync(AgentSettings settings)
    {
        SocketDirectories.PrepareInstance(settings.InstanceDirectory);
        Directory.CreateDirectory(settings.OpDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string[] version = ["--version"];
        OpOutcome outcome = await OpRunner.RunAsync(settings.Options.OpPath, version, null, settings.OpDirectory, settings.Options.OpTimeout, TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
        string found = string.Empty;
        if (outcome.Output is PinnedDocument output)
        {
            using (output)
            {
                found = Encoding.UTF8.GetString(output.Span).Trim();
            }
        }

        return found == AgentNames.OpVersion ? null : new Failure { Code = Codes.AgentOpVersion, Details = new Dictionary<string, string> { ["found"] = found } };
    }

    /// <summary>Asks the agent's status on its push socket (<c>01</c> PRT-AGT-005): exit 0 where it is ready, 3 where it waits, 1 where it cannot be reached.</summary>
    private static async Task<int> StatusAsync(string instance, AgentPaths paths, TextWriter output, TextWriter error)
    {
        Result<AgentSettings> read = Configuration(instance, paths);
        if (read.Value is not AgentSettings settings)
        {
            return Refuse(error, read.Failure!);
        }

        using CancellationTokenSource bound = new(StatusTimeout);
        using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using PinnedDocument request = RequestFormat.WriteStatus();
        using PinnedBufferWriter answer = new(Limits.AnswerBytes);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(settings.PushDirectory, AgentNames.Socket)), bound.Token).ConfigureAwait(false);
            await socket.SendAsync(request.Memory, SocketFlags.None, bound.Token).ConfigureAwait(false);
            socket.Shutdown(SocketShutdown.Send);
            int count;
            while ((count = await socket.ReceiveAsync(answer.GetMemory(4096), SocketFlags.None, bound.Token).ConfigureAwait(false)) > 0)
            {
                answer.Advance(count);
            }
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return Refuse(error, new Failure { Code = Codes.AgentUnreachable });
        }

        if (answer.IsOverBound)
        {
            return Refuse(error, new Failure { Code = Codes.AgentUnreachable });
        }

        using PinnedDocument document = answer.Detach();
        int exit = AnswerFormat.Read(document.Span).Value switch
        {
            WireAnswer.Ready => 0,
            WireAnswer.Waiting => 3,
            _ => -1,
        };
        if (exit < 0)
        {
            return Refuse(error, new Failure { Code = Codes.AgentUnreachable });
        }

        await output.WriteLineAsync(Encoding.UTF8.GetString(document.Span)).ConfigureAwait(false);
        return exit;
    }

    /// <summary>Writes <paramref name="failure"/> as one line, <c>{"code":"...","details":{...}}</c>, its details in ordinal order, and answers 1.</summary>
    internal static int Refuse(TextWriter error, Failure failure)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(failure);
        using MemoryStream line = new();
        using (Utf8JsonWriter writer = new(line))
        {
            writer.WriteStartObject();
            writer.WriteString("code", failure.Code);
            writer.WriteStartObject("details");
            foreach (KeyValuePair<string, string> detail in failure.Details.OrderBy(detail => detail.Key, StringComparer.Ordinal))
            {
                writer.WriteString(detail.Key, detail.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        error.WriteLine(Encoding.UTF8.GetString(line.ToArray()));
        CryptographicOperations.ZeroMemory(line.GetBuffer());
        return 1;
    }
}