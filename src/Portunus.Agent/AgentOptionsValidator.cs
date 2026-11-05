using Microsoft.Extensions.Options;

namespace Portunus.Agent;

/// <summary>The source-generated validation of <see cref="AgentOptions"/>, run once they are bound (<c>06</c> PRT-ENG-004).</summary>
[OptionsValidator]
internal sealed partial class AgentOptionsValidator : IValidateOptions<AgentOptions>;