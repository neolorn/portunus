using System;
using System.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

/// <remarks>
/// The packages are packed once for the class, from the working tree, into a folder under
/// <c>tmp/</c>.
/// </remarks>
[Trait("Category", "integration")]
public sealed class PackTests(PackedPackages packed) : IClassFixture<PackedPackages>
{
    [Fact]
    public void PRT_LIB_001_AC2_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked()
    {
        Assert.Equal(PackedPackages.Projects.Count, packed.Manifests.Count);
        Assert.Contains(packed.Manifests, manifest => manifest.Dependencies.Any(dependency => Packages.Packed.Contains(dependency.Id, StringComparer.Ordinal)));
        Assert.Empty(PackageRules.DependencyViolations(packed.Manifests));
    }

    [Fact]
    public void Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone()
    {
        Assert.Equal(PackedPackages.Projects.Count, packed.Manifests.Count);
        Assert.Empty(packed.Manifests.SelectMany(PackageRules.ManifestViolations));
    }
}