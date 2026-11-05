using Xunit;

namespace Portunus.Repository.Tests;

/// <remarks>
/// The grants are read from each project's built assembly under <c>bin/Release/net10.0/</c>,
/// which the build of <c>Portunus.sln</c> the gates run first leaves there.
/// </remarks>
[Trait("Category", "contract")]
public sealed class GrantTests
{
    [Fact]
    public void PRT_ENG_001_AC1_GrantsFollowTheRule()
    {
        Assert.Empty(ReferenceRules.GrantViolations(RepositoryRoot.Path));
    }
}