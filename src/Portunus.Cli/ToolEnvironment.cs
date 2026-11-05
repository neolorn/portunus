using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Portunus.Cli;

/// <summary>
/// The environment the tool runs in and passes on: its variables, which <c>op</c> gets less the
/// ones that would sign it in otherwise than through the desktop app, and which <c>ssh</c> gets
/// whole (<c>02</c> PRT-PSH-002); and the <c>PATH</c> both are found on.
/// </summary>
internal sealed class ToolEnvironment(IReadOnlyDictionary<string, string> variables)
{
    /// <summary>The variables <c>op</c> never gets: a token or a Connect server would sign it in without the owner (<c>02</c> PRT-PSH-002).</summary>
    private static readonly string[] Withheld = ["OP_SERVICE_ACCOUNT_TOKEN", "OP_CONNECT_HOST", "OP_CONNECT_TOKEN"];

    private const string SessionPrefix = "OP_SESSION_";

    /// <summary>The variables, compared without regard to case, as Windows compares them.</summary>
    internal IReadOnlyDictionary<string, string> Variables { get; } = variables;

    /// <summary>The variables <c>op</c> runs with: these less <c>OP_SERVICE_ACCOUNT_TOKEN</c>, <c>OP_CONNECT_HOST</c>, <c>OP_CONNECT_TOKEN</c> and every <c>OP_SESSION_</c> one.</summary>
    internal IEnumerable<KeyValuePair<string, string>> ForOp => Variables.Where(variable =>
        !Withheld.Contains(variable.Key, StringComparer.OrdinalIgnoreCase) && !variable.Key.StartsWith(SessionPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>The process's own environment.</summary>
    internal static ToolEnvironment OfProcess()
    {
        Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            variables[(string)variable.Key] = (string?)variable.Value ?? string.Empty;
        }

        return new ToolEnvironment(variables);
    }

    /// <summary>
    /// The first file named <paramref name="program"/> in a folder of the <c>PATH</c>, on Windows
    /// with an extension of <c>PATHEXT</c>; or none. A folder that is not an absolute path is
    /// skipped, since it names the working directory, where a planted program would run.
    /// </summary>
    internal string? Find(string program)
    {
        string[] names = OperatingSystem.IsWindows()
            ? [.. Value("PATHEXT", ".EXE;.CMD;.BAT;.COM").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(extension => program + extension)]
            : [program];
        foreach (string folder in Value("PATH", string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Where(Path.IsPathFullyQualified))
        {
            foreach (string name in names)
            {
                string candidate = Path.Combine(folder, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private string Value(string name, string otherwise)
    {
        return Variables.TryGetValue(name, out string? value) && value.Length > 0 ? value : otherwise;
    }
}