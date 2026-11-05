using System;
using System.IO;

using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "unit")]
public sealed class SettingRuleTests
{
    private const string TestProject = "tests/Portunus.Core.Tests/Portunus.Core.Tests.csproj";

    [Fact]
    public void PRT_ENG_002_AC1_ProjectsAndSharedFilesSetOnlyWhatTheRuleAllows()
    {
        Assert.Empty(SettingRules.SettingViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>", "sets TargetFramework")]
    [InlineData("<PropertyGroup><Nullable>disable</Nullable></PropertyGroup>", "sets Nullable")]
    [InlineData("<PropertyGroup><TreatWarningsAsErrors>false</TreatWarningsAsErrors></PropertyGroup>", "sets TreatWarningsAsErrors")]
    [InlineData("<PropertyGroup><MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors></PropertyGroup>", "sets MSBuildTreatWarningsAsErrors")]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\" VersionOverride=\"3.0.0\" /></ItemGroup>", "sets the version of xunit.v3")]
    public void PRT_ENG_002_AC1_ProjectSettingWhatOnlyTheSharedFilesSetIsFound(string body, string violation)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, body);

        Assert.Contains(SettingRules.SettingViolations(fixture.Root), found => found.Contains(violation, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("NoWarn")]
    [InlineData("WarningsNotAsErrors")]
    [InlineData("WarningLevel")]
    [InlineData("MSBuildWarningsAsMessages")]
    [InlineData("MSBuildWarningsNotAsErrors")]
    [InlineData("RunAnalyzers")]
    [InlineData("RunAnalyzersDuringBuild")]
    [InlineData("EnableNETAnalyzers")]
    [InlineData("CodeAnalysisRuleSet")]
    [InlineData("NuGetAuditLevel")]
    [InlineData("AnalysisMode")]
    [InlineData("AnalysisLevelDesign")]
    [InlineData("AnalysisModeSecurity")]
    public void PRT_ENG_002_AC1_ProjectPropertyThatTurnsAWarningOffIsFound(string property)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, $"<PropertyGroup><{property}>x</{property}></PropertyGroup>");

        Assert.Contains(
            $"{TestProject} sets {property}, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_PropertyThatTurnsAWarningOffInATargetIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, "<Target Name=\"Quiet\" BeforeTargets=\"Build\"><PropertyGroup><NoWarn>CS0168</NoWarn></PropertyGroup></Target>");

        Assert.Contains(
            $"{TestProject} sets NoWarn, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("NuGetAuditSuppress", "https://github.com/advisories/GHSA-5crp-9r3c-p9vr")]
    [InlineData("EditorConfigFiles", "quiet.editorconfig")]
    [InlineData("GlobalAnalyzerConfigFiles", "quiet.globalconfig")]
    public void PRT_ENG_002_AC1_ItemThatTurnsAWarningOffIsFound(string item, string include)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, $"<ItemGroup><{item} Include=\"{include}\" /></ItemGroup>");

        Assert.Contains(
            $"{TestProject} adds a {item} item, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\" NoWarn=\"NU1903\" /></ItemGroup>")]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\"><NoWarn>NU1903</NoWarn></PackageReference></ItemGroup>")]
    public void PRT_ENG_002_AC1_ReferenceThatTurnsAWarningOffIsFound(string body)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, body);

        Assert.Contains($"{TestProject} sets NoWarn on its PackageReference item xunit.v3, which turns a warning off.", SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_SharedBuildSettingsTurningAWarningOffIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", "  <PropertyGroup><WarningsNotAsErrors>CS0168</WarningsNotAsErrors></PropertyGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            "Directory.Build.props sets WarningsNotAsErrors, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_PackageVersionsSettingAnotherPropertyIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="xunit.v3" Version="4.0.2" NoWarn="NU1903" />
              </ItemGroup>
            </Project>
            """);

        string[] violations = [.. SettingRules.SettingViolations(fixture.Root)];

        Assert.Contains("Directory.Packages.props sets CentralPackageTransitivePinningEnabled; it sets no property but ManagePackageVersionsCentrally.", violations);
        Assert.Contains("Directory.Packages.props sets NoWarn on its PackageVersion item xunit.v3, which turns a warning off.", violations);
    }

    [Theory]
    [InlineData("src/Portunus.Core/.editorconfig")]
    [InlineData(".globalconfig")]
    [InlineData("tests/Portunus.Core.Tests/quiet.globalconfig")]
    [InlineData("artifacts/.editorconfig")]
    public void PRT_ENG_002_AC1_AnalyserConfigurationBesideTheRootEditorConfigIsFound(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, "[*.cs]\ndotnet_diagnostic.CS0168.severity = none\n");

        Assert.Contains($"{path} configures the analysers; only the root .editorconfig does.", SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData(".editorconfig")]
    [InlineData("tmp/.editorconfig")]
    [InlineData("src/Portunus.Core/bin/.editorconfig")]
    [InlineData("src/Portunus.Core/obj/quiet.globalconfig")]
    public void PRT_ENG_002_AC1_RootEditorConfigTmpAndBuildOutputAreLeftOut(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, "[*.cs]\ndotnet_diagnostic.CS0168.severity = none\n");

        Assert.Empty(SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("<TargetFramework>net10.0</TargetFramework>", "<TargetFramework>net9.0</TargetFramework>", "TargetFramework to net10.0")]
    [InlineData("<Nullable>enable</Nullable>", "<Nullable>disable</Nullable>", "Nullable to enable")]
    [InlineData("<ImplicitUsings>disable</ImplicitUsings>", "<ImplicitUsings>enable</ImplicitUsings>", "ImplicitUsings to disable")]
    [InlineData("<AnalysisLevel>latest-all</AnalysisLevel>", "<AnalysisLevel>latest</AnalysisLevel>", "AnalysisLevel to latest-all")]
    [InlineData("<MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors>", "", "MSBuildTreatWarningsAsErrors to true")]
    [InlineData("<NuGetAuditMode>all</NuGetAuditMode>", "<NuGetAuditMode>direct</NuGetAuditMode>", "NuGetAuditMode to all")]
    [InlineData("<Deterministic>true</Deterministic>", "", "Deterministic to true")]
    public void PRT_ENG_002_AC1_SharedBuildSettingsOtherThanTheRulesAreFound(string setting, string replacement, string expected)
    {
        ArgumentNullException.ThrowIfNull(setting);
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        Assert.Contains(setting, props, StringComparison.Ordinal);
        fixture.Write("Directory.Build.props", props.Replace(setting, replacement, StringComparison.Ordinal));

        Assert.Contains($"Directory.Build.props does not set {expected}.", SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Directory.Build.targets")]
    [InlineData("src/Directory.Build.props")]
    [InlineData("tests/Portunus.Core.Tests/Directory.Packages.props")]
    [InlineData("Directory.Solution.props")]
    public void PRT_ENG_002_AC1_SharedBuildFileBesideTheRootsIsFound(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, "<Project />\n");

        Assert.Contains($"{path} sets what only the root's Directory.Build.props and Directory.Packages.props set.", SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("""{ "sdk": { "version": "9.0.300", "rollForward": "latestPatch" } }""", "global.json does not name a .NET 10 SDK.")]
    [InlineData("""{ "sdk": { "rollForward": "latestPatch" } }""", "global.json does not name a .NET 10 SDK.")]
    [InlineData("""{ "sdk": { "version": "10.0.403", "rollForward": "latestFeature" } }""", "global.json does not set rollForward to latestPatch.")]
    [InlineData("""{ "sdk": { "version": "10.0.403" } }""", "global.json does not set rollForward to latestPatch.")]
    public void PRT_ENG_002_AC1_SdkOtherThanTheRulesIsFound(string global, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("global.json", global);

        Assert.Contains(expected, SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_RepositoryWithoutGlobalJsonIsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "global.json"));

        Assert.Contains("global.json is missing.", SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("src/Portunus.Core/Portunus.Core.csproj", "<PropertyGroup><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>")]
    [InlineData(TestProject, "<PropertyGroup><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>")]
    [InlineData("tests/Portunus.Agent.Tests/Portunus.Agent.Tests.csproj", "<Target Name=\"Unsafe\" BeforeTargets=\"Build\"><PropertyGroup><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup></Target>")]
    public void PRT_ENG_002_AC1_UnsafeBlocksOutsideTheAgentAreFound(string project, string body)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(project, body);

        Assert.Contains($"{project} sets AllowUnsafeBlocks, which src/Portunus.Agent/Portunus.Agent.csproj alone sets.", SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_UnsafeBlocksOfTheAgentAreAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", "<PropertyGroup><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>");

        Assert.Empty(SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_UnsafeBlocksOfTheAgentOtherThanTrueAreFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", "<PropertyGroup><AllowUnsafeBlocks>false</AllowUnsafeBlocks></PropertyGroup>");

        Assert.Contains("src/Portunus.Agent/Portunus.Agent.csproj sets AllowUnsafeBlocks to false, not true.", SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_002_AC1_UnsafeBlocksInTheSharedBuildSettingsAreFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", "  <PropertyGroup><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains("Directory.Build.props sets AllowUnsafeBlocks, which src/Portunus.Agent/Portunus.Agent.csproj alone sets.", SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("src/Portunus.Agent/Portunus.Agent.csproj", "RuntimeIdentifier", "linux-x64")]
    [InlineData("src/Portunus.Agent/Portunus.Agent.csproj", "RuntimeIdentifiers", "linux-x64;linux-arm64")]
    [InlineData("src/Portunus.Agent/Portunus.Agent.csproj", "SelfContained", "true")]
    [InlineData("src/Portunus.Agent/Portunus.Agent.csproj", "PublishSingleFile", "true")]
    [InlineData(TestProject, "PublishSingleFile", "true")]
    [InlineData("Directory.Build.props", "SelfContained", "true")]
    public void PRT_ENG_002_AC1_PublicationSetByAProjectIsFound(string file, string property, string value)
    {
        ArgumentNullException.ThrowIfNull(file);
        using RepositoryFixture fixture = new();
        string setting = $"<PropertyGroup><{property}>{value}</{property}></PropertyGroup>";
        if (string.Equals(file, "Directory.Build.props", StringComparison.Ordinal))
        {
            string props = File.ReadAllText(Path.Combine(fixture.Root, file));
            fixture.Write(file, props.Replace("</Project>", $"  {setting}\n</Project>", StringComparison.Ordinal));
        }
        else
        {
            fixture.WriteProject(file, setting);
        }

        Assert.Contains($"{file} sets {property}, which the release's publish step passes as an argument.", SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void SettingViolations_ProjectWithoutItsLockfile_IsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "src", "Portunus.Core", "packages.lock.json"));

        Assert.Contains("src/Portunus.Core/Portunus.Core.csproj has no packages.lock.json.", SettingRules.SettingViolations(fixture.Root));
    }
}