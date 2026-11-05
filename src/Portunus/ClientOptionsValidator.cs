using Microsoft.Extensions.Options;

namespace Portunus;

/// <summary>The source-generated validation of <see cref="ClientOptions"/>, run once they are bound (<c>06</c> PRT-ENG-004).</summary>
[OptionsValidator]
internal sealed partial class ClientOptionsValidator : IValidateOptions<ClientOptions>;