using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus.Cli;

/// <summary>
/// Runs one command of the computer's <c>op</c> (<c>02</c> PRT-PSH-002): in the tool's own
/// environment less the variables that would sign it in otherwise than through the desktop app,
/// so it signs in through the app and its prompt, never with a token; with no standard input;
/// its standard output read as bytes from the base stream into a pinned buffer of at most
/// 4 MiB, as the agent reads it (<c>01</c> PRT-AGT-003), and its standard error read and never
/// kept.
/// </summary>
internal static class OpCommand
{
    /// <summary>One <c>op</c> command's output (<c>07</c> section 5).</summary>
    internal const int OutputBytes = 4 * 1024 * 1024;

    /// <summary>Runs <c>op</c> with <paramref name="arguments"/>, and answers its output, the caller's to dispose, where it ran and exited 0; or none.</summary>
    internal static async Task<PinnedDocument?> RunAsync(ToolEnvironment environment, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (environment.Find("op") is not string op)
        {
            return null;
        }

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
        foreach (KeyValuePair<string, string> variable in environment.ForOp)
        {
            start.Environment[variable.Key] = variable.Value;
        }

        using Process process = new() { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }

        process.StandardInput.Close();
        using PinnedBufferWriter output = new(OutputBytes);
        using PinnedBufferWriter error = new(OutputBytes);
#pragma warning disable CA2025 // Both reads are awaited below before the buffers are disposed; the rule does not follow a task held in a local.
        Task reading = ReadAsync(process.StandardOutput.BaseStream, output, cancellationToken);
        Task readingError = ReadAsync(process.StandardError.BaseStream, error, cancellationToken);
#pragma warning restore CA2025
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await Task.WhenAll(reading, readingError).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        return process.ExitCode == 0 && reading.IsCompletedSuccessfully && !output.IsOverBound ? output.Detach() : null;
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