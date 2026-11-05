using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus.Agent.Fetch;

/// <summary>
/// Runs one <c>op</c> command (<c>01</c> PRT-AGT-003): in an environment holding the token, if
/// any, <c>OP_CACHE</c> <c>false</c> and <c>HOME</c> its configuration directory, and nothing
/// else; bounded by <c>Portunus:Agent:OpTimeout</c>, its whole process tree killed when it
/// passes it; its standard output read as bytes from the base stream into a pinned buffer of
/// at most 4 MiB, and its standard error read for its class alone (PRT-AGT-004), never kept.
/// </summary>
internal static class OpRunner
{
    /// <summary>One <c>op</c> command's output (<c>07</c> section 5).</summary>
    internal const int OutputBytes = 4 * 1024 * 1024;

    /// <summary>Runs <paramref name="op"/> with <paramref name="arguments"/>.</summary>
    /// <param name="op">The pinned <c>op</c>'s path.</param>
    /// <param name="arguments">The command's arguments.</param>
    /// <param name="token">The token, or none for a command that needs none; made a string only for the child's environment, as the process API demands (<c>06</c> PRT-ENG-005).</param>
    /// <param name="home">The directory <c>op</c> takes as its home.</param>
    /// <param name="timeout">The command's bound.</param>
    /// <param name="time">The clock the bound is measured by.</param>
    /// <param name="cancellationToken">Cancels the command, which is killed.</param>
    internal static async Task<OpOutcome> RunAsync(string op, IReadOnlyList<string> arguments, ReadOnlyMemory<byte>? token, string home, TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(op)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Clear();
        if (token is ReadOnlyMemory<byte> held)
        {
            start.Environment["OP_SERVICE_ACCOUNT_TOKEN"] = Encoding.UTF8.GetString(held.Span);
        }

        start.Environment["OP_CACHE"] = "false";
        start.Environment["HOME"] = home;

        using Process process = new() { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // An op that cannot be started fails as one that cannot reach the vault.
            return new OpOutcome(null, null, FailureClass.Unreachable);
        }

        process.StandardInput.Close();
        using CancellationTokenSource bound = new(timeout, time);
        using CancellationTokenSource running = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, cancellationToken);
        using PinnedBufferWriter output = new(OutputBytes);
        using PinnedBufferWriter error = new(OutputBytes);
#pragma warning disable CA2025 // Both reads are awaited below, on every path, before the buffers are disposed; the rule does not follow a task held in a local.
        Task reading = ReadAsync(process.StandardOutput.BaseStream, output, running.Token);
        Task readingError = ReadAsync(process.StandardError.BaseStream, error, running.Token);
#pragma warning restore CA2025
        bool killed = false;
        try
        {
            await process.WaitForExitAsync(running.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            killed = true;
        }

        // Both reads end before their buffers are disposed, the killed command's too.
        await Task.WhenAll(reading, readingError).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (killed || !reading.IsCompletedSuccessfully || !readingError.IsCompletedSuccessfully)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new OpOutcome(null, null, FailureClass.Unreachable);
        }

        using PinnedDocument standardError = error.IsOverBound ? new PinnedDocument([], 0) : error.Detach();
        return process.ExitCode != 0 || output.IsOverBound
            ? new OpOutcome(process.ExitCode, null, Classify(standardError.Span))
            : new OpOutcome(0, output.Detach(), null);
    }

    /// <summary>
    /// The class of a failed command's standard error (<c>01</c> PRT-AGT-004): <c>(429)</c> is a
    /// rate limit, <c>(401)</c> or <c>(403)</c> a refusal, anything else unreachable.
    /// </summary>
    internal static FailureClass Classify(ReadOnlySpan<byte> standardError)
    {
        if (standardError.IndexOf("(429)"u8) >= 0)
        {
            return FailureClass.RateLimited;
        }

        return standardError.IndexOf("(401)"u8) >= 0 || standardError.IndexOf("(403)"u8) >= 0 ? FailureClass.Refused : FailureClass.Unreachable;
    }

    private static async Task ReadAsync(Stream stream, PinnedBufferWriter buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            int count = await stream.ReadAsync(buffer.GetMemory(81920), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }

            buffer.Advance(count);
        }
    }
}

/// <summary>An <c>op</c> command's outcome: its exit code, its output where it succeeded, or the class of its failure.</summary>
/// <param name="ExitCode">The exit code, or none where it was killed or could not start.</param>
/// <param name="Output">The standard output, the caller's to dispose, where the command succeeded.</param>
/// <param name="Failure">The failure's class, where it failed.</param>
internal sealed record OpOutcome(int? ExitCode, PinnedDocument? Output, FailureClass? Failure);

/// <summary>The class of a failed attempt (<c>01</c> PRT-AGT-004), each the agent's reason while it waits.</summary>
internal enum FailureClass
{
    /// <summary><c>unreachable</c>: any failure but those below, a timeout included.</summary>
    Unreachable,

    /// <summary><c>ratelimited</c>: a standard error holding <c>(429)</c>.</summary>
    RateLimited,

    /// <summary><c>refused</c>: a standard error holding <c>(401)</c> or <c>(403)</c>.</summary>
    Refused,
}