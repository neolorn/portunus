namespace Portunus;

/// <summary>Where the client reads its secrets: the closed value <c>Portunus:Client:Source</c> of <c>07</c> section 2.</summary>
internal enum ClientSource
{
    /// <summary>From the agent, through its socket (<c>04</c> PRT-LIB-004); spelled <c>agent</c>.</summary>
    Agent,

    /// <summary>From the development file (<c>04</c> PRT-LIB-005); spelled <c>file</c>.</summary>
    File,
}