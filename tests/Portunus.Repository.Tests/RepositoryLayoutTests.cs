using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "unit")]
public sealed class RepositoryLayoutTests
{
    /// <summary>The package source mapping PRT-ENG-001 gives (P-036).</summary>
    private const string Mapping = """<packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource><packageSource key="vendor"><package pattern="Janus.*" /><package pattern="Hermes.Core" /><package pattern="Themis.Core" /></packageSource></packageSourceMapping>""";

    private static readonly IReadOnlyList<string> RootFiles =
        ["Portunus.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", ".editorconfig", ".gitignore", "nuget.config", "CHANGELOG.md", "RELEASE-NOTES.md"];

    [Fact]
    public void PRT_ENG_001_AC1_ProjectsFollowTheLayout()
    {
        Assert.Empty(LayoutRules.ProjectViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_ENG_001_AC1_ProjectFoldersFollowTheLayout()
    {
        Assert.Empty(LayoutRules.FolderViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_ENG_001_AC1_ProjectOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Extras/Portunus.Extras.csproj", string.Empty);

        Assert.Contains(LayoutRules.ProjectViolations(fixture.Root), violation => violation.StartsWith("src/Portunus.Extras/Portunus.Extras.csproj is not", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tests/Portunus.Extras.Tests/Portunus.Extras.Tests.csproj", "tests/Portunus.Extras.Tests is the test project of no source project.")]
    [InlineData("tools/Portunus.Tools/Portunus.Tools.csproj", "tools/Portunus.Tools/Portunus.Tools.csproj is not a part's project at src/<Part>/ or a test project at tests/<Project>/.")]
    [InlineData("src/Portunus.Core/Nested/Nested.csproj", "src/Portunus.Core/Nested/Nested.csproj is not a part's project at src/<Part>/ or a test project at tests/<Project>/.")]
    [InlineData("deploy/Portunus.Deploy/Portunus.Deploy.csproj", "deploy/Portunus.Deploy/Portunus.Deploy.csproj is not a part's project at src/<Part>/ or a test project at tests/<Project>/.")]
    public void PRT_ENG_001_AC1_ProjectOfNoPlaceIsFound(string path, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(path, string.Empty);

        Assert.Contains(expected, LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_SourceProjectWithoutItsTestProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "tests", "Portunus.Testing.Tests", "Portunus.Testing.Tests.csproj"));

        Assert.Contains($"src/{Packages.Testing} has no test project tests/{Packages.Testing}.Tests.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_RepositoryWithoutItsRepositoryTestsIsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "tests", Packages.RepositoryTests, Packages.RepositoryTests + ".csproj"));

        Assert.Contains($"tests/{Packages.RepositoryTests} is missing.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Core)]
    [InlineData(Packages.Client)]
    [InlineData(Packages.Testing)]
    public void PRT_ENG_001_AC1_PartWithoutItsProjectIsFound(string part)
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "src", part, part + ".csproj"));

        Assert.Contains($"src/{part} is missing, though {part} is a part of 00 section 2.2.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Agent)]
    [InlineData(Packages.Cli)]
    [InlineData(Packages.Janus)]
    [InlineData(Packages.Hermes)]
    [InlineData(Packages.Themis)]
    public void PRT_ENG_001_AC1_PartOfALaterPhaseIsNotYetRequired(string part)
    {
        using RepositoryFixture fixture = new();

        Assert.DoesNotContain(LayoutRules.ProjectViolations(fixture.Root), violation => violation.Contains(part, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(Packages.Agent)]
    [InlineData(Packages.Cli)]
    [InlineData(Packages.Janus)]
    public void PRT_ENG_001_AC1_PartOfALaterPhaseWithItsTestProjectIsAccepted(string part)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject($"src/{part}/{part}.csproj", string.Empty);
        fixture.WriteProject($"tests/{part}.Tests/{part}.Tests.csproj", string.Empty);
        fixture.WriteSolution([.. ProjectFile.ReadAll(fixture.Root).Select(project => project.RelativePath).Where(path => !path.StartsWith("samples/", StringComparison.Ordinal))]);

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Other.sln")]
    [InlineData("Portunus.slnx")]
    [InlineData("tests/Tests.sln")]
    [InlineData("samples/Other.sln")]
    public void PRT_ENG_001_AC1_SolutionOtherThanTheTwoIsFound(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, string.Empty);

        Assert.Contains($"{path} is a solution other than Portunus.sln and samples/Samples.sln.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_SampleSolutionIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Samples.sln", string.Empty);

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_ProjectTheSolutionDoesNotListIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSolution(["src/Portunus.Core/Portunus.Core.csproj"]);

        Assert.Contains("Portunus.sln does not list src/Portunus/Portunus.csproj.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_ProjectTheSolutionListsOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSolution([.. ProjectFile.ReadAll(fixture.Root).Select(project => project.RelativePath), "samples/Sample/Sample.csproj"]);

        Assert.Contains("Portunus.sln lists samples/Sample/Sample.csproj, which is not a part's project or a test project.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData("public sealed class Secret;\n")]
    [InlineData("// namespace Portunus.Core;\n\npublic sealed class Secret;\n")]
    [InlineData("[assembly: System.CLSCompliant(false)]\n")]
    public void PRT_ENG_001_AC1_FileWithoutANamespaceIsFound(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus.Core/Secret.cs", source);

        Assert.Contains("src/Portunus.Core/Secret.cs declares no namespace.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_SampleHostsTopLevelProgramIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Sample/Sample.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        fixture.Write("samples/Sample/Program.cs", "using Sample;\n\nawait Start.RunAsync(args);\n");

        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("samples/Sample/Start.cs", "await Start.RunAsync(args);\n", "samples/Sample/Start.cs declares no namespace.")]
    [InlineData("samples/Sample.Tests/Program.cs", "await Start.RunAsync(args);\n", "samples/Sample.Tests/Program.cs declares no namespace.")]
    [InlineData("samples/Sample/Program.cs", "namespace Sample;\n\ninternal static class Program;\n", "samples/Sample/Program.cs declares a namespace, though it is the sample host's top-level statements.")]
    public void PRT_ENG_001_AC1_SampleFileOutsideTheNamespaceRuleIsFound(string path, string source, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Sample/Sample.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        fixture.Write("samples/Sample.Tests/Sample.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        fixture.Write(path, source);

        Assert.Contains(expected, LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_TwoPublicTypesInOneFileAreFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus.Core/Pair.cs", "namespace Portunus.Core;\n\npublic sealed class One;\n\npublic sealed class Two;\n");

        Assert.Contains("src/Portunus.Core/Pair.cs declares more than one public top-level type.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("namespace Portunus.Core\n{\n    public sealed class One;\n\n    public sealed class Two;\n}\n")]
    [InlineData("namespace Portunus.Core\n{\n    /// <summary>One.</summary>\n    public sealed class One\n    {\n    }\n\n    public enum Two\n    {\n        First,\n    }\n}\n")]
    public void PRT_ENG_001_AC1_TwoPublicTypesInABlockScopedNamespaceAreFound(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus.Core/Pair.cs", source);

        Assert.Contains("src/Portunus.Core/Pair.cs declares more than one public top-level type.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("namespace Portunus.Core;\n\npublic sealed class Secret\n{\n    public sealed class Version;\n\n    public enum Kind\n    {\n        Single,\n    }\n}\n")]
    [InlineData("namespace Portunus.Core\n{\n    public sealed class Secret\n    {\n        public sealed class Version;\n    }\n}\n")]
    [InlineData("namespace Portunus.Core;\n\n// public sealed class Commented;\n\npublic sealed class Secret\n{\n    private const string Text = \"public sealed class Quoted\";\n}\n")]
    public void PRT_ENG_001_AC1_TypeWithItsNestedTypesIsOneTopLevelType(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus.Core/Secret.cs", source);

        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_FolderOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus.Core/Fetch/Fetch.cs", "namespace Portunus.Core.Fetch;\n");

        Assert.Contains("src/Portunus.Core/Portunus.Core.csproj: the folder Fetch is not one the project may hold.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("Fetch")]
    [InlineData("Serving")]
    public void PRT_ENG_001_AC1_AgentsFoldersAreAccepted(string folder)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write($"src/Portunus.Agent/{folder}/Part.cs", $"namespace Portunus.Agent.{folder};\n\ninternal sealed class Part;\n");

        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_FolderOfTheAgentOutsideItsTwoIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write("src/Portunus.Agent/Push/Push.cs", "namespace Portunus.Agent.Push;\n");

        Assert.Contains("src/Portunus.Agent/Portunus.Agent.csproj: the folder Push is not one the project may hold.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_FolderInsideAnAgentFolderIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write("src/Portunus.Agent/Serving/Sockets/Socket.cs", "namespace Portunus.Agent.Serving.Sockets;\n");

        Assert.Contains("src/Portunus.Agent/Portunus.Agent.csproj: the folder Serving holds a folder of its own.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_FolderOfATestProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Portunus.Tests/Reads/ReadTests.cs", "namespace Portunus.Tests.Reads;\n");

        Assert.Contains("tests/Portunus.Tests/Portunus.Tests.csproj: the folder Reads is not one the project may hold.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_NamespaceThatDoesNotMatchItsFolderIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write("src/Portunus.Agent/Fetch/Fetcher.cs", "namespace Portunus.Agent;\n");

        Assert.Contains("src/Portunus.Agent/Fetch/Fetcher.cs declares the namespace Portunus.Agent, not Portunus.Agent.Fetch.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_PublicTypeOutsideTheProjectsRootIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write("src/Portunus.Agent/Fetch/Fetcher.cs", "namespace Portunus.Agent.Fetch;\n\npublic sealed class Fetcher;\n");

        Assert.Contains("src/Portunus.Agent/Fetch/Fetcher.cs declares a public type outside the project's root folder.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_PackageSourcesFollowTheRule()
    {
        Assert.Empty(LayoutRules.PackageSourceViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("<packageSources><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /><add key=\"vendor\" value=\"vendor\" /></packageSources>", "nuget.config does not clear the package sources first.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, and vendor/ as its two sources.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /><add key=\"vendor\" value=\"vendor\" /><add key=\"local\" value=\"artifacts/packages\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, and vendor/ as its two sources.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://example.invalid/v3/index.json\" /><add key=\"vendor\" value=\"vendor\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, and vendor/ as its two sources.")]
    public void PRT_ENG_001_AC1_PackageSourcesOutsideTheRuleAreFound(string sources, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("nuget.config", $"""
            <configuration>
              {sources}
              {Mapping}
            </configuration>
            """);

        Assert.Contains(LayoutRules.PackageSourceViolations(fixture.Root), violation => violation.EndsWith(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"*\" /></packageSource></packageSourceMapping>")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"Microsoft.*\" /></packageSource><packageSource key=\"vendor\"><package pattern=\"Janus.*\" /><package pattern=\"Hermes.Core\" /><package pattern=\"Themis.Core\" /></packageSource></packageSourceMapping>")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"*\" /></packageSource><packageSource key=\"vendor\"><package pattern=\"Janus.*\" /><package pattern=\"Hermes.*\" /><package pattern=\"Themis.Core\" /></packageSource></packageSourceMapping>")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"*\" /></packageSource><packageSource key=\"vendor\"><package pattern=\"Janus.*\" /><package pattern=\"Hermes.Core\" /><package pattern=\"Themis.Core\" /></packageSource><packageSource key=\"other\"><package pattern=\"Portunus.*\" /></packageSource></packageSourceMapping>")]
    public void PRT_ENG_001_AC1_PackageSourceMappingOutsideTheRuleIsFound(string mapping)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("nuget.config", $"""
            <configuration>
              <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /><add key="vendor" value="vendor" /></packageSources>
              {mapping}
            </configuration>
            """);

        Assert.Contains("nuget.config does not map Janus.*, Hermes.Core, Themis.Core to vendor/ alone and every other identifier, \"*\", to nuget.org alone.", LayoutRules.PackageSourceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_VendoredFilesMatchTheirSums()
    {
        Assert.Empty(LayoutRules.VendorViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_ENG_001_AC1_VendoredFileWhoseShaDiffersIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("vendor/Hermes.Core.1.0.0-rc.6.nupkg", "a package");
        fixture.Write("vendor/SHA256SUMS", $"{new string('0', 64)}  Hermes.Core.1.0.0-rc.6.nupkg\n");

        Assert.Contains("vendor/Hermes.Core.1.0.0-rc.6.nupkg's SHA-256 differs from vendor/SHA256SUMS.", LayoutRules.VendorViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_VendoredFileTheSumsDoNotListIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("vendor/Themis.Core.1.0.0-rc.2.nupkg", "a package");
        fixture.Write("vendor/SHA256SUMS", string.Empty);

        Assert.Contains("vendor/Themis.Core.1.0.0-rc.2.nupkg is not listed in vendor/SHA256SUMS.", LayoutRules.VendorViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_VendoredFileOfTheListedShaIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("vendor/Hermes.Core.1.0.0-rc.6.nupkg", "a package");
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("a package"u8));
        fixture.Write("vendor/SHA256SUMS", $"{hash}  Hermes.Core.1.0.0-rc.6.nupkg\n");

        Assert.Empty(LayoutRules.VendorViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_IgnoreFileListsThePackedPackages()
    {
        Assert.Empty(LayoutRules.IgnoreViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void PRT_ENG_001_AC1_IgnoreFileWithoutThePackedPackagesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(".gitignore", "bin/\nobj/\n# /artifacts/packages/\n");

        Assert.Contains(".gitignore does not list artifacts/packages/.", LayoutRules.IgnoreViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_001_AC1_RootOfTheListedFilesIsAccepted()
    {
        Assert.Empty(LayoutRules.RootFileViolations(
            [
                .. RootFiles, "src/Portunus.Core/Portunus.Core.csproj", "docs/spec/00-overview.md", ".github/workflows/gates.yml",
                "build/gates/run-gates.ps1", "deploy/example.json", "vendor/SHA256SUMS", "samples/Samples.sln",
            ]));
    }

    [Theory]
    [InlineData("README.md", "README.md sits at the repository's root, which holds only the files PRT-ENG-001 lists.")]
    [InlineData("AGENTS.md", "AGENTS.md sits at the repository's root, which holds only the files PRT-ENG-001 lists.")]
    [InlineData("tools/run.ps1", "tools/ is a folder at the repository's root that PRT-ENG-001 does not list.")]
    [InlineData("tmp/notes.txt", "tmp/ is a folder at the repository's root that PRT-ENG-001 does not list.")]
    [InlineData(".config/dotnet-tools.json", ".config/ is a folder at the repository's root that PRT-ENG-001 does not list.")]
    public void PRT_ENG_001_AC1_RootFileOutsideTheListIsFound(string path, string expected)
    {
        Assert.Contains(expected, LayoutRules.RootFileViolations([.. RootFiles, path]));
    }

    [Theory]
    [InlineData("CHANGELOG.md")]
    [InlineData("nuget.config")]
    [InlineData("Portunus.sln")]
    [InlineData("RELEASE-NOTES.md")]
    public void PRT_ENG_001_AC1_ListedRootFileMissingIsFound(string missing)
    {
        Assert.Contains($"{missing} is missing from the repository's root.", LayoutRules.RootFileViolations([.. RootFiles.Where(file => !string.Equals(file, missing, StringComparison.Ordinal))]));
    }

    [Fact]
    public void RepositoryFixture_AsMade_KeepsEveryRule()
    {
        using RepositoryFixture fixture = new();

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
        Assert.Empty(SettingRules.SettingViolations(fixture.Root));
        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
        Assert.Empty(PackageRules.MockingViolations(fixture.Root));
        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
        Assert.Empty(ScannerConfigurationRules.Violations(fixture.Root));
    }
}