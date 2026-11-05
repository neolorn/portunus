using System.IO;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary>Each adapter built against its sibling's package as released (<c>00</c> PRT-OVR-002, <c>04</c> PRT-LIB-001).</summary>
[Trait("Category", "unit")]
public sealed class SiblingPackageTests
{
    [Fact]
    public void PRT_OVR_002_AC1_EachAdapterBuildsAgainstItsSiblingsReleasedPackage()
    {
        Assert.Empty(PackageRules.SiblingBuildViolations(RepositoryRoot.Path));
        Assert.Empty(LayoutRules.VendorViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_OVR_002_AC1_AdapterResolvingAnotherVersionOfItsSiblingIsFound()
    {
        using ScratchFolder folder = new();
        Release(folder);
        folder.Write("src/Portunus.Themis/packages.lock.json", Lock("Themis.Core", "Direct", "1.0.0-rc.1"));

        Assert.Contains(
            "src/Portunus.Themis does not build against Themis.Core 1.0.0-rc.2: its lockfile resolves 1.0.0-rc.1.",
            PackageRules.SiblingBuildViolations(folder.Path));
    }

    [Fact]
    public void PRT_OVR_002_AC1_AdapterTakingItsSiblingOnlyThroughAnotherPackageIsFound()
    {
        using ScratchFolder folder = new();
        Release(folder);
        folder.Write("src/Portunus.Hermes/packages.lock.json", Lock("Hermes.Core", "Transitive", "1.0.0-rc.6"));

        Assert.Contains(
            "src/Portunus.Hermes does not build against Hermes.Core 1.0.0-rc.6: its lockfile resolves no direct dependency on it.",
            PackageRules.SiblingBuildViolations(folder.Path));
    }

    [Fact]
    public void PRT_OVR_002_AC1_SiblingsReleaseMissingFromVendorIsFound()
    {
        using ScratchFolder folder = new();
        Release(folder);
        File.Delete(Path.Combine(folder.Path, "vendor", "Janus.Hosting.1.0.0-rc.4.nupkg"));

        Assert.Contains(
            "vendor/ does not hold Janus.Hosting.1.0.0-rc.4.nupkg, the release src/Portunus.Janus builds against.",
            PackageRules.SiblingBuildViolations(folder.Path));
    }

    [Fact]
    public void SiblingBuildViolations_EachAdapterAtItsPinnedRelease_AreNone()
    {
        using ScratchFolder folder = new();
        Release(folder);

        Assert.Empty(PackageRules.SiblingBuildViolations(folder.Path));
    }

    /// <summary>Writes each adapter's lockfile resolving its sibling directly at its pinned version, and each sibling's file under <c>vendor/</c>.</summary>
    private static void Release(ScratchFolder folder)
    {
        folder.Write("src/Portunus.Janus/packages.lock.json", Lock("Janus.Hosting", "Direct", "1.0.0-rc.4"));
        folder.Write("src/Portunus.Hermes/packages.lock.json", Lock("Hermes.Core", "Direct", "1.0.0-rc.6"));
        folder.Write("src/Portunus.Themis/packages.lock.json", Lock("Themis.Core", "Direct", "1.0.0-rc.2"));
        folder.Write("vendor/Janus.Hosting.1.0.0-rc.4.nupkg", "a package");
        folder.Write("vendor/Hermes.Core.1.0.0-rc.6.nupkg", "a package");
        folder.Write("vendor/Themis.Core.1.0.0-rc.2.nupkg", "a package");
    }

    private static string Lock(string package, string type, string version)
    {
        return $$"""{ "version": 2, "dependencies": { "net10.0": { "{{package}}": { "type": "{{type}}", "resolved": "{{version}}" } } } }""";
    }
}