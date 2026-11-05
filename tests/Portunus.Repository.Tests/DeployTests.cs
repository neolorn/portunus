using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "unit")]
public sealed class DeployTests
{
    private static readonly string Reference = RepositoryRoot.Combine("docs", "spec", "07-reference.md");

    [Fact]
    public void PRT_OPS_003_AC2_UnitAndDropInHoldEveryLineOfTheirSettings()
    {
        Assert.Empty(DeployRules.Violations(RepositoryRoot.Path, Reference));
    }

    [Fact]
    public void PRT_OPS_003_AC2_UnitLackingALineIsFound()
    {
        using ScratchFolder folder = new();
        string unit = File.ReadAllText(RepositoryRoot.Combine("deploy", DeployRules.Unit));
        folder.Write(Path.Combine("deploy", DeployRules.Unit), unit.Replace("LimitCORE=0\n", string.Empty, System.StringComparison.Ordinal));
        folder.Write(Path.Combine("deploy", DeployRules.DropIn), File.ReadAllText(RepositoryRoot.Combine("deploy", DeployRules.DropIn)));

        Assert.Equal(["deploy/portunus-agent@.service lacks the line LimitCORE=0, which 07 section 7 gives it."], DeployRules.Violations(folder.Path, Reference));
    }

    [Fact]
    public void Settings_Reference_GiveTheUnitAndTheDropInTheirLines()
    {
        Dictionary<string, IReadOnlyList<string>> settings = DeployRules.Settings(Reference).ToDictionary(entry => entry.File, entry => entry.Settings, System.StringComparer.Ordinal);

        Assert.Contains("ExecStart=/usr/local/bin/portunus-agent run %i", settings[DeployRules.Unit]);
        Assert.Equal(["LoadCredential=token:/etc/portunus/%i.token"], settings[DeployRules.DropIn]);
    }
}