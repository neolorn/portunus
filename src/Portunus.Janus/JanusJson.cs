using System.Text.Json.Serialization;

namespace Portunus.Janus;

/// <summary>The adapter's source-generated JSON context, for the name a refusal's detail carries (<c>06</c> PRT-ENG-004).</summary>
[JsonSerializable(typeof(string))]
internal sealed partial class JanusJson : JsonSerializerContext;