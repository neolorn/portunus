using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus.Cli;

/// <summary>
/// The commands of <c>portunus</c> (<c>02</c> PRT-PSH-002, PRT-PSH-004; <c>07</c> section 3):
/// <c>push</c>, <c>check</c> and <c>status</c>. The tool's own refusal is one JSON line on
/// standard error and exit code 1; a fault that carries no code is <c>secrets.fault</c> the same
/// way (<c>06</c> PRT-ENG-003). No value reaches the screen, a file, an argument or an
/// environment variable.
/// </summary>
internal static class ToolCommand
{
    /// <summary>Runs the command <paramref name="arguments"/> name, and answers its exit code.</summary>
    /// <param name="arguments">The command and its options.</param>
    /// <param name="environment">The environment <c>op</c> and <c>ssh</c> are found in and run with.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    internal static async Task<int> MainAsync(IReadOnlyList<string> arguments, ToolEnvironment environment, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            Result<ToolArguments> read = ToolArguments.Read(arguments);
            if (read.Value is not ToolArguments options)
            {
                return Refuse(error, read.Failure!);
            }

            return options.Command switch
            {
                ToolArguments.Push => await PushAsync(options, environment, output, error).ConfigureAwait(false),
                ToolArguments.Check => await CheckAsync(options, environment, output, error).ConfigureAwait(false),
                _ => await StatusAsync(options, environment, output, error).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A fault is written with no detail, its message never shown, since it may name a path.
            return Refuse(error, new Failure { Code = Codes.Fault });
        }
    }

    /// <summary>Reads the vault, refuses a set that breaks the layout, and sends it to the agent through <c>ssh</c> and <c>receive</c> (<c>02</c> PRT-PSH-002).</summary>
    private static async Task<int> PushAsync(ToolArguments options, ToolEnvironment environment, TextWriter output, TextWriter error)
    {
        if (await VaultReader.CheckVersionAsync(environment, CancellationToken.None).ConfigureAwait(false) is Failure version)
        {
            return Refuse(error, version);
        }

        Result<ReadVault> read = await VaultReader.ReadAsync(environment, options.Vault!, options.Account, CancellationToken.None).ConfigureAwait(false);
        if (read.Value is not ReadVault vault)
        {
            return Refuse(error, read.Failure!);
        }

        using (vault.Values)
        {
            if (LayoutValidator.FirstError(LayoutValidator.Check(vault.Set)) is LayoutFinding finding)
            {
                return Refuse(error, new Failure { Code = finding.Code, Details = Names(finding) });
            }

            if (!RequestFormat.TryWritePush(vault.Set, options.Replace, out PinnedDocument? payload))
            {
                return Refuse(error, new Failure { Code = Codes.PushTooLarge });
            }

            (int ExitCode, string? Line)? sent;
            using (payload)
            {
                sent = await SendAsync(options, environment, "receive", payload.Memory).ConfigureAwait(false);
            }

            if (await AnswerAsync(sent, output, error).ConfigureAwait(false) is int refused)
            {
                return refused;
            }

            return PushAnswerFormat.Read(Encoding.UTF8.GetBytes(sent!.Value.Line!)).Value is PushAnswer.Accepted ? 0 : 1;
        }
    }

    /// <summary>Reads the vault as a push does and writes each finding of the layout, warnings included, as one line; exits 1 where any is an error (<c>02</c> PRT-PSH-004).</summary>
    private static async Task<int> CheckAsync(ToolArguments options, ToolEnvironment environment, TextWriter output, TextWriter error)
    {
        if (await VaultReader.CheckVersionAsync(environment, CancellationToken.None).ConfigureAwait(false) is Failure version)
        {
            return Refuse(error, version);
        }

        Result<ReadVault> read = await VaultReader.ReadAsync(environment, options.Vault!, options.Account, CancellationToken.None).ConfigureAwait(false);
        if (read.Value is not ReadVault vault)
        {
            return Refuse(error, read.Failure!);
        }

        using (vault.Values)
        {
            IReadOnlyList<LayoutFinding> findings = LayoutValidator.Check(vault.Set);
            foreach (LayoutFinding finding in findings)
            {
                await output.WriteLineAsync(Line(writer =>
                {
                    writer.WriteString("code", finding.Code);
                    if (finding.Item is not null)
                    {
                        writer.WriteString("item", finding.Item);
                    }

                    if (finding.Field is not null)
                    {
                        writer.WriteString("field", finding.Field);
                    }
                })).ConfigureAwait(false);
            }

            return findings.Any(finding => !finding.IsWarning) ? 1 : 0;
        }
    }

    /// <summary>Runs <c>portunus-agent status</c> through <c>ssh</c>, writes its answer, and exits as it does: 0, 3, or 1 (<c>02</c> PRT-PSH-004).</summary>
    private static async Task<int> StatusAsync(ToolArguments options, ToolEnvironment environment, TextWriter output, TextWriter error)
    {
        (int ExitCode, string? Line)? sent = await SendAsync(options, environment, "status", ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
        if (await AnswerAsync(sent, output, error).ConfigureAwait(false) is int refused)
        {
            return refused;
        }

        return sent!.Value.ExitCode is 0 or 3 ? sent.Value.ExitCode : 1;
    }

    /// <summary>
    /// Writes the agent's answer as one line; or refuses where no <c>ssh</c> is on the
    /// <c>PATH</c>, or where <c>ssh</c> could not reach the server, and exits 1 where the agent's
    /// command wrote its own refusal, which reached the terminal through <c>ssh</c>.
    /// </summary>
    private static async Task<int?> AnswerAsync((int ExitCode, string? Line)? sent, TextWriter output, TextWriter error)
    {
        if (sent is not { } answered)
        {
            return Refuse(error, new Failure { Code = Codes.CliSshMissing });
        }

        (int exitCode, string? line) = answered;

        if (line is null)
        {
            return exitCode == SshCommand.SshFailure ? Refuse(error, new Failure { Code = Codes.PushUnreachable }) : 1;
        }

        await output.WriteLineAsync(line).ConfigureAwait(false);
        return null;
    }

    /// <summary>Runs <paramref name="command"/> on the server with <paramref name="payload"/>, and answers its exit code and the agent's answer, which holds no value; or none where no <c>ssh</c> is on the <c>PATH</c>.</summary>
    private static async Task<(int ExitCode, string? Line)?> SendAsync(ToolArguments options, ToolEnvironment environment, string command, ReadOnlyMemory<byte> payload)
    {
        (int ExitCode, PinnedDocument Answer)? sent = await SshCommand.RunAsync(environment, options.To!, command, options.Instance!, payload, CancellationToken.None).ConfigureAwait(false);
        if (sent is not (int exitCode, PinnedDocument answer))
        {
            return null;
        }

        using (answer)
        {
            ReadOnlySpan<byte> line = answer.Span.TrimEnd("\r\n"u8);
            return (exitCode, line.IsEmpty ? null : Encoding.UTF8.GetString(line));
        }
    }

    private static Dictionary<string, string> Names(LayoutFinding finding)
    {
        return new Dictionary<string, string> { ["item"] = finding.Item ?? string.Empty, ["field"] = finding.Field ?? string.Empty };
    }

    /// <summary>Writes <paramref name="failure"/> as one line, <c>{"code":"...","details":{...}}</c>, its details in ordinal order, and answers 1.</summary>
    internal static int Refuse(TextWriter error, Failure failure)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(failure);
        error.WriteLine(Line(writer =>
        {
            writer.WriteString("code", failure.Code);
            writer.WriteStartObject("details");
            foreach (KeyValuePair<string, string> detail in failure.Details.OrderBy(detail => detail.Key, StringComparer.Ordinal))
            {
                writer.WriteString(detail.Key, detail.Value);
            }

            writer.WriteEndObject();
        }));
        return 1;
    }

    /// <summary>One JSON object of names and codes, never a value, as a line.</summary>
    private static string Line(Action<Utf8JsonWriter> members)
    {
        using MemoryStream line = new();
        using (Utf8JsonWriter writer = new(line))
        {
            writer.WriteStartObject();
            members(writer);
            writer.WriteEndObject();
        }

        string written = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length);
        CryptographicOperations.ZeroMemory(line.GetBuffer());
        return written;
    }
}