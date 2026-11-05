using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus.Cli;

/// <summary>
/// Runs a command of <c>portunus-agent</c> on the server through the system's <c>ssh</c>
/// (<c>02</c> PRT-PSH-002 step 4, PRT-PSH-004): <c>ssh -T -o StrictHostKeyChecking=yes --
/// &lt;destination&gt; portunus-agent &lt;command&gt; &lt;instance&gt;</c>, in the tool's
/// environment, the payload written to its standard input and closed, and the agent's answer
/// read from its standard output; its standard error is the owner's terminal's, which carries
/// <c>ssh</c>'s own prompts and never the payload.
/// </summary>
internal static class SshCommand
{
    /// <summary>The exit code with which <c>ssh</c> reports its own failure, the server not reached.</summary>
    internal const int SshFailure = 255;

    /// <summary>Runs <paramref name="command"/> for <paramref name="instance"/> on <paramref name="destination"/>, answering its exit code and the answer, the caller's to dispose; or none where no <c>ssh</c> is on the <c>PATH</c>.</summary>
    internal static async Task<(int ExitCode, PinnedDocument Answer)?> RunAsync(ToolEnvironment environment, string destination, string command, string instance, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (environment.Find("ssh") is not string ssh)
        {
            return null;
        }

        ProcessStartInfo start = new(ssh)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        foreach (string argument in new[] { "-T", "-o", "StrictHostKeyChecking=yes", "--", destination, "portunus-agent", command, instance })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Clear();
        foreach (KeyValuePair<string, string> variable in environment.Variables)
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

        using PinnedBufferWriter answer = new(Limits.AnswerBytes);
#pragma warning disable CA2025 // The read is awaited below before the buffer is disposed; the rule does not follow a task held in a local.
        Task reading = ReadAsync(process, answer, cancellationToken);
#pragma warning restore CA2025
        try
        {
            try
            {
                await process.StandardInput.BaseStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch (System.IO.IOException)
            {
                // ssh ended before it took the whole payload; its exit code says why.
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A cancelled command is killed with every process it started, as the agent's op is.
            process.Kill(entireProcessTree: true);
            throw;
        }
        finally
        {
            await reading.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        return (process.ExitCode, reading.IsCompletedSuccessfully && !answer.IsOverBound ? answer.Detach() : new PinnedDocument([], 0));
    }

    private static async Task ReadAsync(Process process, PinnedBufferWriter answer, CancellationToken cancellationToken)
    {
        while (true)
        {
            int count = await process.StandardOutput.BaseStream.ReadAsync(answer.GetMemory(Limits.RequestBytes), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }

            answer.Advance(count);
        }
    }
}