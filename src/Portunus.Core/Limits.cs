namespace Portunus.Core;

/// <summary>
/// The limits of <c>07</c> section 5 the library keeps.
/// </summary>
internal static class Limits
{
    /// <summary>Items in a vault (<c>03</c> PRT-VLT-003).</summary>
    internal const int Items = 64;

    /// <summary>Fields in an item (<c>03</c> PRT-VLT-003).</summary>
    internal const int Fields = 128;

    /// <summary>A value: 64 KiB (<c>03</c> PRT-VLT-003).</summary>
    internal const int ValueBytes = 64 * 1024;

    /// <summary>The values of a set together: 1 MiB (<c>03</c> PRT-VLT-003).</summary>
    internal const int SetBytes = 1024 * 1024;

    /// <summary>Live versions of a set (<c>03</c> PRT-VLT-004).</summary>
    internal const int Versions = 64;

    /// <summary>The highest version number (<c>03</c> PRT-VLT-004).</summary>
    internal const int VersionNumber = 9999;
}