using System.IO;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary><c>08</c> PRT-THR-001 criterion 1 over this repository: its threat model and its tests (P-041).</summary>
[Trait("Category", "unit")]
public sealed class ThreatModelTests
{
    [Fact]
    public void PRT_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement()
    {
        string model = File.ReadAllText(RepositoryRoot.Combine("docs", "spec", "08-threat-model.md"));

        Assert.Empty(ThreatModelRules.Untested(ThreatModelRules.Named(model), ThreatModelRules.Tested(RepositoryRoot.Combine("tests"))));
    }
}