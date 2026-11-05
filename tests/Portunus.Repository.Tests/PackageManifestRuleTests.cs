using System;
using System.Xml.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "unit")]
public sealed class PackageManifestRuleTests
{
    private const string Core = """
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>Portunus.Core</id>
            <version>1.2.3</version>
            <authors>Hossam Ismail</authors>
            <description>The public types and contracts the other packages share</description>
            <repository type="git" url="https://github.com/neolorn/portunus" />
          </metadata>
        </package>
        """;

    private const string Tool = """
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>Portunus.Cli</id>
            <version>1.2.3</version>
            <authors>Hossam Ismail</authors>
            <packageTypes>
              <packageType name="DotnetTool" />
            </packageTypes>
            <description>A .NET tool, command portunus, that pushes an environment's secrets, checks a vault's layout and reads an agent's status</description>
            <repository type="git" url="https://github.com/neolorn/portunus" />
          </metadata>
        </package>
        """;

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("[1.2.2]")]
    [InlineData("[1.2.3, )")]
    public void PRT_LIB_001_AC2_DependencyOnALibraryPackageNotAtTheExactVersionPackedIsFound(string version)
    {
        PackageManifest[] manifests = [Parse(Core), Parse(Client(version))];

        Assert.Equal(
            [$"Portunus depends on Portunus.Core at {version}, not the exact version packed, [1.2.3]."],
            PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void PRT_LIB_001_AC2_DependencyAtTheExactVersionPackedIsAccepted()
    {
        PackageManifest[] manifests = [Parse(Core), Parse(Client("[1.2.3]"))];

        Assert.Empty(PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void PRT_LIB_001_AC2_DependencyOnALibraryPackageNotPackedWithItIsFound()
    {
        PackageManifest[] manifests = [Parse(Client("[1.2.3]"))];

        Assert.Equal(["Portunus depends on Portunus.Core, which was not packed with it."], PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void PackageTable_Descriptions_AreTheCellsWithoutMarkupOrReferences()
    {
        Assert.Equal("A .NET tool, command portunus, that pushes an environment's secrets, checks a vault's layout and reads an agent's status", PackageTable.Descriptions[Packages.Cli]);
        Assert.Equal("The Janus.Core.ISecretSource over the client", PackageTable.Descriptions[Packages.Janus]);
        Assert.Equal("A fixed secret client a host's tests set", PackageTable.Descriptions[Packages.Testing]);
        Assert.Equal(Packages.All.Count, PackageTable.Descriptions.Count);
    }

    [Fact]
    public void ManifestViolations_PackageAsTheRuleGivesIt_HasNone()
    {
        Assert.Empty(PackageRules.ManifestViolations(Parse(Core)));
    }

    [Fact]
    public void ManifestViolations_ToolWithItsPackageType_HasNone()
    {
        Assert.Empty(PackageRules.ManifestViolations(Parse(Tool)));
    }

    [Fact]
    public void ManifestViolations_PackageTypeOfAPackageOtherThanTheTool_IsFound()
    {
        PackageManifest manifest = Parse(Core.Replace("</metadata>", "<packageTypes><packageType name=\"DotnetTool\" /></packageTypes></metadata>", StringComparison.Ordinal));

        Assert.Equal(["Portunus.Core carries the package property packageTypes, which is left unset."], PackageRules.ManifestViolations(manifest));
    }

    [Theory]
    [InlineData("<tags>secrets</tags>", "Portunus.Core carries the package property tags, which is left unset.")]
    [InlineData("<license type=\"expression\">MIT</license>", "Portunus.Core carries the package property license, which is left unset.")]
    [InlineData("<projectUrl>https://example.invalid/</projectUrl>", "Portunus.Core carries the package property projectUrl, which is left unset.")]
    public void ManifestViolations_AnotherPackageProperty_IsFound(string element, string violation)
    {
        PackageManifest manifest = Parse(Core.Replace("</metadata>", element + "</metadata>", StringComparison.Ordinal));

        Assert.Equal([violation], PackageRules.ManifestViolations(manifest));
    }

    [Theory]
    [InlineData("<authors>Hossam Ismail</authors>", "<authors>someone</authors>", "Portunus.Core's authors are someone, not Hossam Ismail.")]
    [InlineData("the other packages share", "every package shares", "Portunus.Core's description is not its cell of 00 section 2.2.")]
    [InlineData("github.com/neolorn/portunus", "github.com/someone/portunus", "Portunus.Core's repository is https://github.com/someone/portunus, not https://github.com/neolorn/portunus.")]
    public void ManifestViolations_PropertyNotAsTheRuleGivesIt_IsFound(string text, string replacement, string violation)
    {
        PackageManifest manifest = Parse(Core.Replace(text, replacement, StringComparison.Ordinal));

        Assert.Equal([violation], PackageRules.ManifestViolations(manifest));
    }

    private static string Client(string coreVersion)
    {
        return $"""
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Portunus</id>
                <version>1.2.3</version>
                <dependencies>
                  <group targetFramework="net10.0">
                    <dependency id="Portunus.Core" version="{coreVersion}" exclude="Build,Analyzers" />
                    <dependency id="Microsoft.Extensions.Options" version="10.0.0" exclude="Build,Analyzers" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;
    }

    private static PackageManifest Parse(string nuspec)
    {
        return PackageManifest.Parse(XDocument.Parse(nuspec));
    }
}