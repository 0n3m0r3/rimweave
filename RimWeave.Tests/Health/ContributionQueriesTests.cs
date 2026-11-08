using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class ContributionQueriesTests
{
    private static readonly ContributionId Id = new("example.mod", "marker");

    [Fact]
    public void Find_SelectsOnlyTheRequestedPartOccurrence()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var left = new BodyPartRecord();
        var right = new BodyPartRecord();
        var expected = ContributionTestData.AttachForLookup(pawn, Id, left);
        ContributionTestData.AttachForLookup(pawn, Id, right);
        Assert.Same(expected, Assert.Single(ContributionQueries.Find(pawn, Id, left)));
        Assert.Equal(2, pawn.health.hediffSet.hediffs.Count);
    }

    [Fact]
    public void Find_NullPartMeansWholeBodyRatherThanEveryPart()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var expected = ContributionTestData.AttachForLookup(pawn, Id);
        ContributionTestData.AttachForLookup(pawn, Id, new BodyPartRecord());
        Assert.Same(expected, Assert.Single(ContributionQueries.Find(pawn, Id)));
    }

    [Fact]
    public void Find_DistinguishesOwners()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var expected = ContributionTestData.AttachForLookup(pawn, Id);
        ContributionTestData.AttachForLookup(pawn, new ContributionId("another.mod", Id.LocalId));
        Assert.Same(expected, Assert.Single(ContributionQueries.Find(pawn, Id)));
    }

    [Fact]
    public void Find_DoesNotSearchAnotherPawn()
    {
        var first = HealthTestPawns.CreateForGuardChecks();
        var second = HealthTestPawns.CreateForGuardChecks();
        ContributionTestData.AttachForLookup(first, Id);
        Assert.Empty(ContributionQueries.Find(second, Id));
    }

    [Fact]
    public void Find_ReturnsAReadOnlySnapshotOfMembership()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var first = ContributionTestData.AttachForLookup(pawn, Id);
        var matches = ContributionQueries.Find(pawn, Id);
        ContributionTestData.AttachForLookup(pawn, Id);
        Assert.Same(first, Assert.Single(matches));
        Assert.Throws<NotSupportedException>(() => ((IList<ContributionHediff>)matches).Clear());
        Assert.Equal(2, pawn.health.hediffSet.hediffs.Count);
    }
}
