using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class HediffOperationsTests
{
    [Fact]
    public void Remove_NullPawn_ThrowsWithParameterName()
    {
        var hediff = new ObservedHediff();

        var exception = Assert.Throws<ArgumentNullException>(() => HediffOperations.Remove(null!, hediff));

        Assert.Equal("pawn", exception.ParamName);
        Assert.Equal(0, hediff.PreRemovedCalls);
        Assert.Equal(0, hediff.PostRemovedCalls);
    }

    [Fact]
    public void Remove_NullHediff_ThrowsWithParameterName()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();

        var exception = Assert.Throws<ArgumentNullException>(() => HediffOperations.Remove(pawn, null!));

        Assert.Equal("hediff", exception.ParamName);
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    [Fact]
    public void Remove_AbsentInstance_CanBeRetriedWithoutCallbacks()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var hediff = new ObservedHediff { pawn = pawn, Part = part };

        HediffRemovalResult first = HediffOperations.Remove(pawn, hediff);
        HediffRemovalResult second = HediffOperations.Remove(pawn, hediff);

        Assert.Equal(HediffRemovalStatus.NotPresent, first.Status);
        Assert.Equal(HediffRemovalStatus.NotPresent, second.Status);
        Assert.Same(pawn, first.Pawn);
        Assert.Same(hediff, first.Hediff);
        Assert.Same(part, first.Part);
        Assert.Equal(0, hediff.PreRemovedCalls);
        Assert.Equal(0, hediff.PostRemovedCalls);
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    [Fact]
    public void Remove_AbsentInstance_PreservesAnotherWithSameDefinitionAndPart()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var definition = new HediffDef();
        var part = new BodyPartRecord();
        var absent = new ObservedHediff { pawn = pawn, def = definition, Part = part };
        var attached = new ObservedHediff { pawn = pawn, def = definition, Part = part };
        pawn.health.hediffSet.hediffs.Add(attached);

        HediffRemovalResult result = HediffOperations.Remove(pawn, absent);

        Assert.Equal(HediffRemovalStatus.NotPresent, result.Status);
        Assert.Same(attached, Assert.Single(pawn.health.hediffSet.hediffs));
        Assert.Equal(0, absent.PreRemovedCalls);
        Assert.Equal(0, absent.PostRemovedCalls);
        Assert.Equal(0, attached.PreRemovedCalls);
        Assert.Equal(0, attached.PostRemovedCalls);
    }

    [Fact]
    public void Remove_WrongPawn_PreservesBothHealthTrackers()
    {
        var owner = HealthTestPawns.CreateForGuardChecks();
        var otherPawn = HealthTestPawns.CreateForGuardChecks();
        var hediff = new ObservedHediff { pawn = owner };
        var otherHediff = new ObservedHediff { pawn = otherPawn };
        owner.health.hediffSet.hediffs.Add(hediff);
        otherPawn.health.hediffSet.hediffs.Add(otherHediff);

        HediffRemovalResult result = HediffOperations.Remove(otherPawn, hediff);

        Assert.Equal(HediffRemovalStatus.PawnMismatch, result.Status);
        Assert.Same(otherPawn, result.Pawn);
        Assert.Same(hediff, result.Hediff);
        Assert.Same(owner, hediff.pawn);
        Assert.Null(result.Part);
        Assert.Same(hediff, Assert.Single(owner.health.hediffSet.hediffs));
        Assert.Same(otherHediff, Assert.Single(otherPawn.health.hediffSet.hediffs));
        Assert.Equal(0, hediff.PreRemovedCalls);
        Assert.Equal(0, hediff.PostRemovedCalls);
        Assert.Equal(0, otherHediff.PreRemovedCalls);
        Assert.Equal(0, otherHediff.PostRemovedCalls);
    }

    private sealed class ObservedHediff : Hediff
    {
        public int PreRemovedCalls { get; private set; }
        public int PostRemovedCalls { get; private set; }

        public override void PreRemoved() => PreRemovedCalls++;
        public override void PostRemoved() => PostRemovedCalls++;
    }
}