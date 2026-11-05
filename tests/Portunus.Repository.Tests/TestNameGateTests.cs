using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Repository.Tests;

/// <remarks>
/// The probe lists two tests named after criteria; a third name is only in a comment, and a
/// fourth is a method no test framework runs, so neither counts.
/// </remarks>
[Trait("Category", "integration")]
public sealed class TestNameGateTests(TestProjectRepository repository) : IClassFixture<TestProjectRepository>
{
    private const string Tests = """
        using Xunit;

        namespace Probe.Tests;

        public sealed class DeclarationTests
        {
            [Fact]
            [Trait("Category", "unit")]
            public void PRT_AGT_006_AC2_UndeclaredAddressIsDenied()
            {
            }

            [Fact]
            [Trait("Category", "unit")]
            public void PRT_AGT_007_AC12_TwelfthCriterionHolds()
            {
            }

            // PRT_AGT_008_AC1_NamedInAComment
        }

        public static class Declarations
        {
            public static void PRT_AGT_009_AC1_NoTest()
            {
            }
        }

        """;

    [Fact]
    public async Task PRT_ENG_008_AC2_EveryListedCriterionWithItsTestPasses()
    {
        ProcessResult gate = await RunAsync("PRT-AGT-006 AC2\nPRT-AGT-007 AC12\n", TestContext.Current.CancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("Each of the 2 criteria", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PRT-AGT-006 AC1\n", "PRT-AGT-006 AC1 has no test named PRT_AGT_006_AC1_<Outcome>")]
    [InlineData("PRT-AGT-006 AC2\nPRT-AGT-010 AC2\n", "PRT-AGT-010 AC2 has no test named PRT_AGT_010_AC2_<Outcome>")]
    [InlineData("PRT-AGT-007 AC1\n", "PRT-AGT-007 AC1 has no test named PRT_AGT_007_AC1_<Outcome>")]
    [InlineData("PRT-AGT-008 AC1\n", "PRT-AGT-008 AC1 has no test named PRT_AGT_008_AC1_<Outcome>")]
    [InlineData("PRT-AGT-009 AC1\n", "PRT-AGT-009 AC1 has no test named PRT_AGT_009_AC1_<Outcome>")]
    [InlineData("PRT-AGT-006 AC2\nPRT-AGT-006\n", "built-items.txt line 2, \"PRT-AGT-006\", does not name one criterion")]
    [InlineData("PRT-AGT-006 AC2\n\n", "built-items.txt line 2, \"\", does not name one criterion")]
    public async Task PRT_ENG_008_AC2_ListedCriterionWithoutItsTestFails(string criteria, string reason)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(reason);

        ProcessResult gate = await RunAsync(criteria, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(reason, gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestNameGate_NoCriterionListed_Fails()
    {
        ProcessResult gate = await RunAsync(string.Empty, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("names no criterion, so the gate would check nothing", gate.Output, StringComparison.Ordinal);
    }

    private Task<ProcessResult> RunAsync(string criteria, CancellationToken cancellationToken)
    {
        repository.WriteTests(Tests);
        repository.Write("build/gates/built-items.txt", criteria);
        return Processes.GateAsync("gate-test-names.ps1", repository.Root, cancellationToken, "-Repository", repository.Root);
    }
}