using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary>
/// The reading of <c>08</c> PRT-THR-001 criterion 1, over a fixture table (P-041); the
/// criterion's own test over this repository is <see cref="ThreatModelTests"/>.
/// </summary>
[Trait("Category", "unit")]
public sealed class ThreatModelRuleTests
{
    private const string Model = """
        **PRT-THR-001** Each threat below SHALL be met by the control it names.

        | Threat | Control |
        |---|---|
        | A disk is copied | Nothing is written to it (`01` PRT-AGT-001); the checklist (PRT-THR-003) |
        | A log leaks a value | Checked in the gate (`05` PRT-OPS-005; `06` PRT-ENG-012) |
        | A process reads another's secret | Its declaration (`01` PRT-AGT-007, PRT-AGT-001) |

        **Acceptance criteria**
        1. Every control above has a test (`02` PRT-PSH-003).
        """;

    [Fact]
    public void Named_FixtureTable_ReadsEachRequirementItsTableNamesOnceInOrder()
    {
        Assert.Equal(["PRT-AGT-001", "PRT-AGT-007"], ThreatModelRules.Named(Model));
    }

    [Fact]
    public void Named_RequirementOnTheList_IsLeftOut()
    {
        IReadOnlyList<string> named = ThreatModelRules.Named(Model);

        Assert.DoesNotContain("PRT-THR-003", named);
        Assert.DoesNotContain("PRT-OPS-005", named);
        Assert.DoesNotContain("PRT-ENG-012", named);
    }

    [Fact]
    public void Named_RequirementAddedToTheTable_IsRead()
    {
        string model = Model.Replace("PRT-AGT-007, PRT-AGT-001", "PRT-AGT-007, PRT-AGT-001, PRT-AGT-009", StringComparison.Ordinal);

        Assert.Contains("PRT-AGT-009", ThreatModelRules.Named(model));
    }

    [Fact]
    public void Named_ModelWithoutTheTable_Throws()
    {
        Assert.Throws<InvalidDataException>(() => ThreatModelRules.Named("**PRT-THR-002** Another requirement."));
    }

    [Fact]
    public void Excluded_List_HoldsTheSixRequirementsOfP041EachWithItsReason()
    {
        Assert.Equal(["PRT-ENG-005", "PRT-ENG-012", "PRT-OPS-002", "PRT-OPS-005", "PRT-PSH-001", "PRT-THR-003"], [.. ThreatModelRules.Excluded.Keys.Order(StringComparer.Ordinal)]);
        Assert.All(ThreatModelRules.Excluded.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    [Fact]
    public void Tested_FixtureTests_ReadsTheRequirementOfEachTestNameAndNoneOfALiteralOrAComment()
    {
        using ScratchFolder folder = new();
        folder.Write("tests/Probe.Tests/ProbeTests.cs", """
            public sealed class ProbeTests
            {
                [Fact]
                public void PRT_AGT_001_AC1_SwapIsRefused()
                {
                }

                [Theory]
                [InlineData(1)]
                public async Task PRT_AGT_007_AC2_UndeclaredAddressIsDenied(int value)
                {
                }

                // [Fact] public void PRT_PSH_003_AC1_InAComment() { }
                private const string Held = "[Fact] public void PRT_PSH_002_AC1_InALiteral() { }";
            }
            """);
        folder.Write("tests/Probe.Tests/obj/Generated.cs", "[Fact] public void PRT_AGT_004_AC1_InBuildOutput() { }");

        IReadOnlySet<string> tested = ThreatModelRules.Tested(Path.Combine(folder.Path, "tests"));

        Assert.Equal(["PRT-AGT-001", "PRT-AGT-007"], [.. tested.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Untested_RequirementWithoutATest_IsFoundAndOneWithATestIsAccepted()
    {
        IReadOnlyList<string> untested = ThreatModelRules.Untested(ThreatModelRules.Named(Model), new HashSet<string>(["PRT-AGT-001"]));

        Assert.Equal(["PRT-AGT-007 names a control of PRT-THR-001, and no test carries its identifier."], untested);
    }
}