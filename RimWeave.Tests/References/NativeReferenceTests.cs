using System.Reflection;
using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.References;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.References;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeReferenceTests : IDisposable
{
    private readonly NativeGameTestScope _game = new();

    public void Dispose() => _game.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructionWithoutCurrentGame_RejectsTargetsRetainedFromAnEarlierGame(bool targetsEffect)
    {
        var pawn = CreatePawn(8);
        var effect = AddEffect(pawn, 1);
        Current.Game = null;

        Assert.Throws<InvalidOperationException>(() => targetsEffect
            ? new NativeReference(effect)
            : new NativeReference(pawn));

        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void EmptyConstructorWithoutCurrentGame_RemainsAvailableForNativeLoading()
    {
        Current.Game = null;

        using var reference = new NativeReference();

        Assert.Equal(NativeReferenceResolution.Unresolved, reference.Capture().Resolution);
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Fact]
    public void OffMapAndDead_AreResolvedWithoutInventingMapPresenceOrLosingIdentity()
    {
        var pawn = CreatePawn(1);
        var reference = new NativeReference(pawn);
        var alive = reference.Capture();
        pawn.health.SetDead();
        var dead = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Resolved, alive.Resolution);
        Assert.False(alive.Spawned);
        Assert.Null(alive.Map);
        Assert.False(alive.Dead);
        Assert.Equal(NativeReferenceResolution.Resolved, dead.Resolution);
        Assert.Same(pawn, dead.Pawn);
        Assert.Equal(alive.PawnIdentity, dead.PawnIdentity);
        Assert.True(dead.Dead);
        Assert.False(dead.Destroyed);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Fact]
    public void RemovedEffect_DoesNotResolveToAnIdenticalNewContribution()
    {
        var pawn = CreatePawn(2);
        var original = AddEffect(pawn, 1);
        var reference = new NativeReference(original);
        reference.StartTracking(new ReferenceTrackingSession());
        pawn.health.hediffSet.hediffs.Remove(original);
        var replacement = AddEffect(pawn, 2);

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Resolved, snapshot.Resolution);
        Assert.Same(original, snapshot.Effect);
        Assert.NotSame(replacement, snapshot.Effect);
        Assert.False(snapshot.EffectAttached);
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.EffectDetached | ReferenceChanges.TrackingInterrupted, reference.TakeChanges());
        Assert.Equal(ReferenceChanges.None, reference.TakeChanges());
        Assert.Throws<InvalidOperationException>(() => reference.StartTracking(new ReferenceTrackingSession()));
        Assert.Same(replacement, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void DestroyedPawn_RemainsIdentifiableButInspectionEndsItsActiveSubscription()
    {
        var pawn = CreatePawn(3);
        var reference = new NativeReference(pawn);
        var session = new ReferenceTrackingSession();
        reference.StartTracking(session);
        // Construct the native destroyed state without pretending to exercise Pawn.Destroy's game dependencies.
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pawn, (sbyte)-2);

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Resolved, snapshot.Resolution);
        Assert.True(snapshot.Destroyed);
        Assert.False(snapshot.Dead);
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.Destroyed | ReferenceChanges.TrackingInterrupted, reference.TakeChanges());
        Assert.Throws<InvalidOperationException>(() => reference.StartTracking(session));
    }

    [Fact]
    public void ChangedNativeOwner_IsMismatchInsteadOfImplicitRetargeting()
    {
        var first = CreatePawn(4);
        var second = CreatePawn(5);
        var effect = AddEffect(first, 1);
        using var reference = new NativeReference(effect);
        reference.StartTracking(new ReferenceTrackingSession());
        effect.pawn = second;

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Mismatch, snapshot.Resolution);
        Assert.Null(snapshot.Effect);
        Assert.False(reference.IsTracking);
        Assert.Same(effect, Assert.Single(first.health.hediffSet.hediffs));
        Assert.Empty(second.health.hediffSet.hediffs);
    }

    [Fact]
    public void DeadPawn_CanStillBeTrackedForHistoricalUses()
    {
        var pawn = CreatePawn(6);
        pawn.health.SetDead();
        using var reference = new NativeReference(pawn);

        reference.StartTracking(new ReferenceTrackingSession());

        Assert.True(reference.IsTracking);
        Assert.True(reference.Capture().Dead);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Fact]
    public void ReattachingTheSameInstance_DoesNotSilentlyRestartAnInterruptedSubscription()
    {
        var pawn = CreatePawn(7);
        var effect = AddEffect(pawn, 1);
        using var reference = new NativeReference(effect);
        var session = new ReferenceTrackingSession();
        reference.StartTracking(session);
        pawn.health.hediffSet.hediffs.Remove(effect);
        reference.Capture();

        pawn.health.hediffSet.hediffs.Add(effect);

        Assert.True(reference.Capture().EffectAttached);
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.EffectDetached | ReferenceChanges.TrackingInterrupted, reference.TakeChanges());
        reference.StartTracking(session);
        Assert.True(reference.IsTracking);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    internal static Pawn CreatePawn(int id)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        pawn.thingIDNumber = id;
        pawn.def.defName = "ReferenceTestPawn";
        pawn.def.category = ThingCategory.Pawn;
        return pawn;
    }

    internal static ContributionHediff AddEffect(Pawn pawn, int id)
    {
        var effect = new ContributionHediff
        {
            pawn = pawn, loadID = id, def = new HediffDef { defName = "ReferenceTestEffect" }, comps = []
        };
        effect.Metadata.AssignIdentity(new ContributionId("example.mod", "same-effect"));
        pawn.health.hediffSet.hediffs.Add(effect);
        return effect;
    }
}
