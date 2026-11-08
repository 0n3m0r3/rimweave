using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class ContributionResolutionTests
{
    [Fact]
    public void Resolve_DistinguishesMissingUniqueAndDuplicateInstances()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var id = new ContributionId("example.mod", "marker");
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionQueries.Resolve(pawn, id).Status);
        var first = ContributionTestData.AttachForLookup(pawn, id);
        Assert.Equal(ContributionLookupStatus.Found, ContributionQueries.Resolve(pawn, id).Status);
        var second = ContributionTestData.AttachForLookup(pawn, id);
        var result = ContributionQueries.Resolve(pawn, id);
        Assert.Equal(ContributionLookupStatus.Ambiguous, result.Status);
        Assert.Contains(first, result.Matches);
        Assert.Contains(second, result.Matches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExclusiveClaim_ReportsBothProvidersRegardlessOfInsertionOrder(bool reverse)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var first = ContributionTestData.AttachForLookup(pawn, new ContributionId("example.a", "marker"));
        var second = ContributionTestData.AttachForLookup(pawn, new ContributionId("example.b", "marker"));
        first.Metadata.ClaimExclusiveResponsibility("example/shared-property");
        second.Metadata.ClaimExclusiveResponsibility("example/shared-property");
        if (reverse) pawn.health.hediffSet.hediffs.Reverse();

        var result = ContributionQueries.ResolveExclusiveClaim(pawn, "example/shared-property");
        Assert.Equal(ContributionLookupStatus.Ambiguous, result.Status);
        Assert.Contains(first, result.Matches);
        Assert.Contains(second, result.Matches);
        Assert.Equal(2, pawn.health.hediffSet.hediffs.Count);
    }

    [Fact]
    public void ExclusiveClaim_DoesNotBlockAnotherPartOrResponsibility()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var left = new BodyPartRecord();
        var right = new BodyPartRecord();
        var a = ContributionTestData.AttachForLookup(pawn, new ContributionId("example.a", "marker"), left);
        var b = ContributionTestData.AttachForLookup(pawn, new ContributionId("example.b", "marker"), right);
        a.Metadata.ClaimExclusiveResponsibility("example/shared-property");
        b.Metadata.ClaimExclusiveResponsibility("example/shared-property");
        Assert.Equal(ContributionLookupStatus.Found,
            ContributionQueries.ResolveExclusiveClaim(pawn, "example/shared-property", left).Status);
        Assert.Equal(ContributionLookupStatus.NotPresent,
            ContributionQueries.ResolveExclusiveClaim(pawn, "example/other-property", left).Status);
    }

    [Fact]
    public void ExclusiveClaim_DoesNotTreatUnclaimedContributionsAsCompetitors()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var claimed = ContributionTestData.AttachForLookup(pawn, new ContributionId("example.a", "marker"));
        var unclaimed = ContributionTestData.AttachForLookup(pawn, new ContributionId("example.b", "marker"));
        claimed.Metadata.ClaimExclusiveResponsibility("example/shared-property");
        unclaimed.comps = [new HediffComp(), new HediffComp()];
        Assert.Same(claimed, Assert.Single(
            ContributionQueries.ResolveExclusiveClaim(pawn, "example/shared-property").Matches));
    }
}
