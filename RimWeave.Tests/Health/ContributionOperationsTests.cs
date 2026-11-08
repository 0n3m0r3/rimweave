using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class ContributionOperationsTests
{
    private static readonly ContributionId Id = new("example.mod", "marker");

    [Fact]
    public void Remove_MissingIdentity_PreservesAnotherOwnersContribution()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var other = ContributionTestData.AttachForLookup<ObservedContribution>(
            pawn, new ContributionId("another.mod", Id.LocalId));

        var result = ContributionOperations.Remove(pawn, Id);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Same(other, Assert.Single(pawn.health.hediffSet.hediffs));
        AssertNoCallbacks(other);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_AmbiguousIdentity_PreservesEveryCandidateWithoutCallbacks(bool reverse)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var first = ContributionTestData.AttachForLookup<ObservedContribution>(pawn, Id, part);
        var second = ContributionTestData.AttachForLookup<ObservedContribution>(pawn, Id, part);
        if (reverse) pawn.health.hediffSet.hediffs.Reverse();
        var before = pawn.health.hediffSet.hediffs.ToArray();

        var result = ContributionOperations.Remove(pawn, Id, part);

        Assert.Equal(ContributionLookupStatus.Ambiguous, result.Lookup.Status);
        Assert.Contains(first, result.Lookup.Matches);
        Assert.Contains(second, result.Lookup.Matches);
        Assert.Null(result.Removal);
        Assert.Equal(before, pawn.health.hediffSet.hediffs);
        AssertNoCallbacks(first, second);
    }

    [Fact]
    public void Remove_AnotherPart_DoesNotRemoveTheMatchingIdentityElsewhere()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var left = new BodyPartRecord();
        var right = new BodyPartRecord();
        var effect = ContributionTestData.AttachForLookup<ObservedContribution>(pawn, Id, right);

        var result = ContributionOperations.Remove(pawn, Id, left);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        AssertNoCallbacks(effect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_KeepsWholeBodyAndLocalEffectsSeparate(bool effectIsWholeBody)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var effect = ContributionTestData.AttachForLookup<ObservedContribution>(
            pawn, Id, effectIsWholeBody ? null : part);

        var result = ContributionOperations.Remove(pawn, Id, effectIsWholeBody ? part : null);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        AssertNoCallbacks(effect);
    }

    [Fact]
    public void Remove_AnotherPawn_DoesNotSearchTheOwner()
    {
        var owner = HealthTestPawns.CreateForGuardChecks();
        var otherPawn = HealthTestPawns.CreateForGuardChecks();
        var effect = ContributionTestData.AttachForLookup<ObservedContribution>(owner, Id);

        var result = ContributionOperations.Remove(otherPawn, Id);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Empty(otherPawn.health.hediffSet.hediffs);
        Assert.Same(effect, Assert.Single(owner.health.hediffSet.hediffs));
        AssertNoCallbacks(effect);
    }

    [Fact]
    public void Remove_DoesNotInventAttributionForAnUnknownEffect()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var effect = new ObservedContribution { pawn = pawn };
        pawn.health.hediffSet.hediffs.Add(effect);

        var result = ContributionOperations.Remove(pawn, Id);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Null(effect.Metadata.Identity);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        AssertNoCallbacks(effect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_UniqueIdentity_ExposesTheNativeGuardResultEvenDuringAClaimConflict(bool conflictingClaim)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var otherPawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var target = ContributionTestData.AttachForLookup<ObservedContribution>(pawn, Id, part);
        var other = ContributionTestData.AttachForLookup<ObservedContribution>(
            pawn, new ContributionId("another.mod", Id.LocalId), part);
        if (conflictingClaim)
        {
            target.Metadata.ClaimExclusiveResponsibility("example/property");
            other.Metadata.ClaimExclusiveResponsibility("example/property");
            Assert.Equal(ContributionLookupStatus.Ambiguous,
                ContributionQueries.ResolveExclusiveClaim(pawn, "example/property", part).Status);
        }

        // Deliberately inconsistent membership exercises delegation while the native guard
        // stops before a health operation requiring a fully initialized game pawn.
        target.pawn = otherPawn;

        var result = ContributionOperations.Remove(pawn, Id, part);

        Assert.Equal(ContributionLookupStatus.Found, result.Lookup.Status);
        Assert.Same(target, Assert.Single(result.Lookup.Matches));
        var removal = Assert.IsType<HediffRemovalResult>(result.Removal);
        Assert.Equal(HediffRemovalStatus.PawnMismatch, removal.Status);
        Assert.Same(pawn, removal.Pawn);
        Assert.Same(target, removal.Hediff);
        Assert.Same(part, removal.Part);
        Assert.Equal(new Hediff[] { target, other }, pawn.health.hediffSet.hediffs);
        Assert.Empty(otherPawn.health.hediffSet.hediffs);
        AssertNoCallbacks(target, other);
    }

    [Fact]
    public void Remove_RejectsNullArgumentsWithoutCallbacks()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var effect = ContributionTestData.AttachForLookup<ObservedContribution>(pawn, Id);

        Assert.Throws<ArgumentNullException>(() => ContributionOperations.Remove(null!, Id));
        Assert.Throws<ArgumentNullException>(() => ContributionOperations.Remove(pawn, null!));
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        AssertNoCallbacks(effect);
    }

    private static void AssertNoCallbacks(params ObservedContribution[] effects)
    {
        foreach (var effect in effects)
        {
            Assert.Equal(0, effect.PreRemovedCalls);
            Assert.Equal(0, effect.PostRemovedCalls);
        }
    }

    private sealed class ObservedContribution : ContributionHediff
    {
        public int PreRemovedCalls { get; private set; }
        public int PostRemovedCalls { get; private set; }

        public override void PreRemoved() => PreRemovedCalls++;
        public override void PostRemoved() => PostRemovedCalls++;
    }
}
