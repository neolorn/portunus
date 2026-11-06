using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Portunus.Fakes;

/// <summary>
/// A scenario of the fake <c>op</c>: whether it was recorded from a test vault (P-005) or
/// written from a documented class (P-028), and the commands it answers.
/// </summary>
/// <param name="Recorded">Whether the scenario was recorded; a written one says so here.</param>
/// <param name="Commands">The commands it answers.</param>
internal sealed record Scenario(bool Recorded, IReadOnlyList<Command> Commands);

/// <summary>One command a scenario answers.</summary>
/// <param name="Arguments">Its arguments, <c>--config</c>'s value left out.</param>
/// <param name="Output">The file beside the scenario holding its standard output, if any.</param>
/// <param name="Error">The file beside the scenario holding its standard error, if any.</param>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="PauseMilliseconds">How long it waits before it answers.</param>
internal sealed record Command(IReadOnlyList<string> Arguments, string? Output, string? Error, int ExitCode, int PauseMilliseconds);

/// <summary>One call the fake answered: its arguments, its environment's names and its process.</summary>
/// <param name="Arguments">The arguments.</param>
/// <param name="Environment">The names of its environment's variables, in ordinal order.</param>
/// <param name="ProcessId">The identifier of the fake's process, so a test tells whether it still runs.</param>
internal sealed record Call(IReadOnlyList<string> Arguments, IReadOnlyList<string> Environment, int ProcessId);

/// <summary>The fakes' source-generated JSON context (<c>06</c> PRT-ENG-004).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Scenario))]
[JsonSerializable(typeof(Call))]
internal sealed partial class FakesJson : JsonSerializerContext;