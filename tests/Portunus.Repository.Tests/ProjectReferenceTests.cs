using System;
using System.IO;

using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "unit")]
public sealed class ProjectReferenceTests
{
    private const string BuildOnly = """
            <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
            <PackageReference Include="MinVer" PrivateAssets="all" />
        """;

    [Fact]
    public void PRT_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows()
    {
        Assert.Empty(ReferenceRules.ReferenceViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData(Packages.Core, Packages.Client)]
    [InlineData(Packages.Client, Packages.Testing)]
    [InlineData(Packages.Testing, Packages.Cli)]
    [InlineData(Packages.Client, Packages.Agent)]
    public void PRT_LIB_001_AC1_ProjectReferenceOutsideTheTableIsFound(string project, string reference)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSource(project, [reference]);

        Assert.Contains(
            $"src/{project}/{project}.csproj references the project {reference}, which PRT-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Core, "Microsoft.Extensions.Options")]
    [InlineData(Packages.Core, "Microsoft.Extensions.Hosting")]
    [InlineData(Packages.Client, "Microsoft.Extensions.Hosting")]
    [InlineData(Packages.Client, "Themis.Core")]
    [InlineData(Packages.Testing, "Microsoft.Extensions.Options")]
    [InlineData(Packages.Testing, "xunit.v3")]
    public void PRT_LIB_001_AC1_PackageReferenceOutsideTheTableIsFound(string project, string package)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject($"src/{project}/{project}.csproj", $"""
              <ItemGroup>
            {BuildOnly}
                <PackageReference Include="{package}" />
              </ItemGroup>
            """);

        Assert.Contains(
            $"src/{project}/{project}.csproj references the package {package}, which PRT-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions")]
    [InlineData("Microsoft.Extensions.Configuration.Binder")]
    [InlineData("Microsoft.Extensions.Options.ConfigurationExtensions")]
    [InlineData("Microsoft.Extensions.Diagnostics.HealthChecks")]
    [InlineData("Microsoft.Extensions.Hosting.Abstractions")]
    public void PRT_LIB_001_AC1_ClientsPackageWithinItsRowIsAccepted(string package)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus/Portunus.csproj", $"""
              <ItemGroup>
            {BuildOnly}
                <PackageReference Include="{package}" />
                <ProjectReference Include="..\..\src\Portunus.Core\Portunus.Core.csproj" />
              </ItemGroup>
            """);

        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_LIB_001_AC1_TestProjectReferenceOutsideItsRowIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteTest("Portunus.Core.Tests", [Packages.Core, Packages.Client]);

        Assert.Contains(
            "tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj references the project Portunus, which PRT-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_LIB_001_AC1_TestProjectTakingTheTestingPackageIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteTest("Portunus.Tests", [Packages.Client, Packages.Testing]);

        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_LIB_001_AC1_TestProjectPackageOutsideTheTestPackagesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Microsoft.Extensions.Hosting" />
                <ProjectReference Include="..\..\src\Portunus.Core\Portunus.Core.csproj" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj references the package Microsoft.Extensions.Hosting, which PRT-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Client)]
    [InlineData(Packages.Testing)]
    public void PRT_LIB_001_AC1_ClientsTestProjectTakingTheHostIsAccepted(string part)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject($"tests/{part}.Tests/{part}.Tests.csproj", $"""
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Microsoft.Extensions.Hosting" />
                <ProjectReference Include="..\..\src\{part}\{part}.csproj" />
              </ItemGroup>
            """);

        Assert.DoesNotContain(ReferenceRules.ReferenceViolations(fixture.Root), violation => violation.Contains("Microsoft.Extensions.Hosting", StringComparison.Ordinal));
    }

    [Fact]
    public void PRT_LIB_001_AC1_ShippedProjectWithoutItsBuildOnlyReferencesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Core/Portunus.Core.csproj", """
              <ItemGroup>
                <PackageReference Include="MinVer" />
              </ItemGroup>
            """);

        string[] violations = [.. ReferenceRules.ReferenceViolations(fixture.Root)];

        Assert.Contains("src/Portunus.Core/Portunus.Core.csproj references MinVer without PrivateAssets=\"all\", though it is build-only.", violations);
        Assert.Contains("src/Portunus.Core/Portunus.Core.csproj does not reference Microsoft.CodeAnalysis.PublicApiAnalyzers as a build-only reference.", violations);
    }

    [Theory]
    [InlineData("src/Portunus/Portunus.csproj", "Microsoft.AspNetCore.App")]
    [InlineData("src/Portunus.Core/Portunus.Core.csproj", "Microsoft.AspNetCore.App")]
    [InlineData("tests/Portunus.Tests/Portunus.Tests.csproj", "Microsoft.AspNetCore.App")]
    [InlineData("src/Portunus.Testing/Portunus.Testing.csproj", "Microsoft.WindowsDesktop.App")]
    public void PRT_LIB_001_AC1_FrameworkReferenceIsFound(string project, string framework)
    {
        ArgumentNullException.ThrowIfNull(project);
        using RepositoryFixture fixture = new();
        string file = Path.Combine(fixture.Root, project);
        fixture.Write(project, File.ReadAllText(file).Replace("</Project>", $"  <ItemGroup><FrameworkReference Include=\"{framework}\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            $"{project} references the framework {framework}, which PRT-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_LIB_001_AC1_PackageReferenceInTheSharedBuildSettingsIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", "  <ItemGroup><PackageReference Include=\"Microsoft.Extensions.Options\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));
        fixture.Write("src/Portunus.Core/packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Microsoft.CodeAnalysis.PublicApiAnalyzers": { "type": "Direct", "requested": "[5.6.0, )", "resolved": "5.6.0" },
                  "Microsoft.Extensions.Options": { "type": "Direct", "requested": "[10.0.0, )", "resolved": "10.0.0" },
                  "MinVer": { "type": "Direct", "requested": "[8.0.0, )", "resolved": "8.0.0" }
                }
              }
            }
            """);

        string[] violations = [.. ReferenceRules.ReferenceViolations(fixture.Root)];

        Assert.Contains("Directory.Build.props adds a PackageReference item Microsoft.Extensions.Options; it adds no item but the CLSCompliant attribute and the exact-version target's update.", violations);
        Assert.Contains("src/Portunus.Core/Portunus.Core.csproj takes the package Microsoft.Extensions.Options by a reference outside its project file, which PRT-LIB-001 does not allow it.", violations);
        Assert.DoesNotContain(violations, violation => violation.Contains("MinVer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DependOnTheExactVersionPacked", "PackageReference")]
    [InlineData("AnotherTarget", "_ProjectReferencesWithVersions")]
    public void PRT_LIB_001_AC1_ItemInATargetOtherThanTheExactVersionUpdateIsFound(string target, string item)
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", $"  <Target Name=\"{target}\"><ItemGroup><{item} Include=\"Microsoft.Extensions.Options\" /></ItemGroup></Target>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            $"Directory.Build.props adds a {item} item Microsoft.Extensions.Options; it adds no item but the CLSCompliant attribute and the exact-version target's update.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_LIB_001_AC1_GlobalPackageReferenceIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Packages.props"));
        fixture.Write("Directory.Packages.props", props.Replace("</Project>", "  <ItemGroup><GlobalPackageReference Include=\"Microsoft.Extensions.Options\" Version=\"10.0.0\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            "Directory.Packages.props adds a GlobalPackageReference item Microsoft.Extensions.Options; it adds no item but PackageVersion.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_LIB_001_AC1_SampleHostAndItsTestsWithinTheirRowAreAccepted()
    {
        using RepositoryFixture fixture = new();
        WriteSample(fixture, "Microsoft.NET.Sdk.Web", """<PackageReference Include="Portunus" /><PackageReference Include="Portunus.Hermes" />""", """<PackageReference Include="Portunus.Testing" /><PackageReference Include="xunit.v3" /><ProjectReference Include="..\Sample\Sample.csproj" />""");

        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Microsoft.NET.Sdk", "", "", "samples/Sample/Sample.csproj is not built on Microsoft.NET.Sdk.Web, which PRT-LIB-001 gives the sample host.")]
    [InlineData("Microsoft.NET.Sdk.Web", """<PackageReference Include="Microsoft.Extensions.Hosting" />""", "", "samples/Sample/Sample.csproj references the package Microsoft.Extensions.Hosting, which PRT-LIB-001 does not allow it.")]
    [InlineData("Microsoft.NET.Sdk.Web", """<ProjectReference Include="..\..\src\Portunus\Portunus.csproj" />""", "", "samples/Sample/Sample.csproj references the project Portunus, which PRT-LIB-001 does not allow it.")]
    [InlineData("Microsoft.NET.Sdk.Web", """<FrameworkReference Include="Microsoft.AspNetCore.App" />""", "", "samples/Sample/Sample.csproj references the framework Microsoft.AspNetCore.App, which PRT-LIB-001 gives the sample host only through the Web SDK.")]
    [InlineData("Microsoft.NET.Sdk.Web", "", """<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />""", "samples/Sample.Tests/Sample.Tests.csproj references the package Microsoft.AspNetCore.Mvc.Testing, which PRT-LIB-001 does not allow it.")]
    [InlineData("Microsoft.NET.Sdk.Web", "", """<ProjectReference Include="..\..\tests\Portunus.Fakes\Portunus.Fakes.csproj" />""", "samples/Sample.Tests/Sample.Tests.csproj references the project Portunus.Fakes, which PRT-LIB-001 does not allow it.")]
    public void PRT_LIB_001_AC1_SampleReferenceOutsideItsRowIsFound(string sdk, string host, string tests, string expected)
    {
        using RepositoryFixture fixture = new();
        WriteSample(fixture, sdk, host, tests);

        Assert.Contains(expected, ReferenceRules.ReferenceViolations(fixture.Root));
    }

    private static void WriteSample(RepositoryFixture fixture, string sdk, string host, string tests)
    {
        fixture.Write("samples/Sample/Sample.csproj", $"""<Project Sdk="{sdk}"><ItemGroup>{host}</ItemGroup></Project>""");
        fixture.Write("samples/Sample.Tests/Sample.Tests.csproj", $"""<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>{tests}</ItemGroup></Project>""");
    }
}