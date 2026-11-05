using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "unit")]
public sealed class PackageAllowListTests
{
    [Fact]
    public void PRT_ENG_009_AC1_PackagesAreWithinTheTable()
    {
        Assert.Empty(PackageRules.AllowListViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_ENG_009_AC1_PackageOutsideTheTableIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Shouldly" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj references Shouldly, which is not in PRT-ENG-009's table.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Polly")]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Dapper")]
    [InlineData("Janus")]
    public void PRT_ENG_009_AC1_PackageVersionOutsideTheTableIsFound(string package)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", $"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="{package}" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Contains($"Directory.Packages.props names {package}, which is not in PRT-ENG-009's table.", PackageRules.AllowListViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Microsoft.Extensions.Hosting.Systemd")]
    [InlineData("Microsoft.Extensions.Configuration.Json")]
    [InlineData("Hermes.Core")]
    public void PRT_ENG_009_AC1_PackageVersionWithinTheTableIsAccepted(string package)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", $"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="{package}" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_009_AC1_PackageTakenByAReferenceOutsideTheProjectFileIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus.Core/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "Newtonsoft.Json": { "type": "Direct", "requested": "[13.0.3, )", "resolved": "13.0.3" } } } }
            """);

        Assert.Contains(
            "src/Portunus.Core/Portunus.Core.csproj references Newtonsoft.Json, which is not in PRT-ENG-009's table.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_008_AC1_NoMockingPackageIsReferenced()
    {
        Assert.Empty(PackageRules.MockingViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_ENG_008_AC1_MockingPackageReferencedIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Moq" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj takes the mocking package Moq.",
            PackageRules.MockingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_008_AC1_MockingPackageTakenThroughAnotherIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Portunus.Core.Tests/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "NSubstitute": { "type": "Transitive", "resolved": "5.0.0" } } } }
            """);

        Assert.Contains(
            "tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj takes the mocking package NSubstitute.",
            PackageRules.MockingViolations(fixture.Root));
    }
}