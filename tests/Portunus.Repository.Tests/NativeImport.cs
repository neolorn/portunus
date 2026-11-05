namespace Portunus.Repository.Tests;

/// <summary>
/// A method of a built assembly that calls native code, with the function it imports and the
/// library it imports the function from.
/// </summary>
internal sealed record NativeImport(string Method, string Function, string Library);