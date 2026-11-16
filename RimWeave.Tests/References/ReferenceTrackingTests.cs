using System.Reflection;
using System.Runtime.CompilerServices;
using RimWeave.Health;
using RimWeave.References;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.References;

[Collection(ScribeTestCollection.Name)]
public sealed class ReferenceTrackingTests : IDisposable
{
    private readonly NativeGameTestScope _game = new();

    public void Dispose() => _game.Dispose();

    [Fact]
    public void NativeMapDepartureAndReturn_AreRetainedIndependentlyByTwoConsumers()
    {
        var pawn = NativeReferenceTests.CreatePawn(1);
        var unrelatedPawn = NativeReferenceTests.CreatePawn(2);
        var session = new ReferenceTrackingSession();
        var map = CreateMap(17);
        var bridge = new ReferenceTrackingMapComponent(map, session);
        bridge.FinalizeInit();
        using var first = new NativeReference(pawn);
        using var second = new NativeReference(pawn);
        using var unrelated = new NativeReference(unrelatedPawn);
        first.StartTracking(session);
        second.StartTracking(session);
        unrelated.StartTracking(session);
        var before = first.Capture();

        // Exercise the actual native event raisers; this is not a complete Pawn.DeSpawn/SpawnSetup test.
        map.events.Notify_ThingDespawned(pawn);
        map.events.Notify_ThingSpawned(pawn);

        var expected = ReferenceChanges.LeftContext | ReferenceChanges.EnteredContext;
        Assert.Equal(before, first.Capture());
        Assert.Equal(expected, first.TakeChanges());
        Assert.Equal(expected, second.PendingChanges);
        Assert.Equal(ReferenceChanges.None, first.TakeChanges());
        Assert.Equal(ReferenceChanges.None, unrelated.PendingChanges);
        bridge.MapRemoved();
    }

    [Fact]
    public void MapRemoval_EndsEventDeliveryWithoutDeclaringFormerPawnDestroyed()
    {
        var pawn = NativeReferenceTests.CreatePawn(3);
        var session = new ReferenceTrackingSession();
        var map = CreateMap(21);
        var bridge = new ReferenceTrackingMapComponent(map, session);
        bridge.FinalizeInit();
        using var reference = new NativeReference(pawn);
        reference.StartTracking(session);
        map.events.Notify_ThingDespawned(pawn);

        bridge.MapRemoved();

        Assert.Equal(ReferenceChanges.LeftContext | ReferenceChanges.ContextRemoved, reference.TakeChanges());
        Assert.Equal(NativeReferenceResolution.Resolved, reference.Capture().Resolution);
        Assert.False(reference.Capture().Destroyed);
        map.events.Notify_ThingSpawned(pawn);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Fact]
    public void UnretainedComponents_DoNotSubscribeInTheirConstructors()
    {
        var pawn = NativeReferenceTests.CreatePawn(4);
        var session = new ReferenceTrackingSession();
        var map = CreateMap(22);
        _ = new ReferenceTrackingMapComponent(map, session); // Native loading can discard this instance.
        var retained = new ReferenceTrackingMapComponent(map, session);
        using var reference = new NativeReference(pawn);
        reference.StartTracking(session);
        map.events.Notify_ThingSpawned(pawn);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        retained.FinalizeInit();
        retained.FinalizeInit();
        map.events.Notify_ThingSpawned(pawn);
        Assert.Equal(ReferenceChanges.EnteredContext, reference.TakeChanges());

        retained.MapRemoved();
        reference.TakeChanges();
        map.events.Notify_ThingSpawned(pawn);

        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Fact]
    public void MovingToAnotherMap_DoesNotLoseCurrentContextWhenPreviousMapIsRemoved()
    {
        var pawn = NativeReferenceTests.CreatePawn(5);
        var session = new ReferenceTrackingSession();
        using var reference = new NativeReference(pawn);
        reference.StartTracking(session);
        session.Notify(pawn, null, ReferenceChanges.LeftContext, 31);
        session.Notify(pawn, null, ReferenceChanges.EnteredContext, 32);
        reference.TakeChanges();

        session.MapRemoved(31);

        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        session.MapRemoved(32);
        Assert.Equal(ReferenceChanges.ContextRemoved, reference.TakeChanges());
    }

    [Fact]
    public void HealthCallbacks_RecordOnlyTheExactCooperatingEffectAndStopAfterRemoval()
    {
        using var game = new GameScope();
        var pawn = NativeReferenceTests.CreatePawn(6);
        var effect = NativeReferenceTests.AddEffect(pawn, 1);
        var independentEffect = NativeReferenceTests.AddEffect(pawn, 2);
        using var reference = new NativeReference(effect);
        using var independent = new NativeReference(independentEffect);
        using var pawnReference = new NativeReference(pawn);
        reference.StartTracking();
        independent.StartTracking();
        pawnReference.StartTracking();

        effect.Notify_PawnDied(null);
        effect.Notify_Resurrected();

        Assert.Equal(ReferenceChanges.Died | ReferenceChanges.Resurrected, reference.TakeChanges());
        Assert.Equal(ReferenceChanges.None, independent.PendingChanges);
        Assert.Equal(ReferenceChanges.None, pawnReference.PendingChanges);
        Assert.Equal(ReferenceTrackingCoverage.Maps, pawnReference.TrackingCoverage);
        Assert.Equal(ReferenceTrackingCoverage.Maps | ReferenceTrackingCoverage.CooperatingHealthEffect,
            reference.TrackingCoverage);
        pawn.health.hediffSet.hediffs.Remove(effect);
        effect.PostRemoved();
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.EffectDetached | ReferenceChanges.TrackingInterrupted, reference.TakeChanges());
        effect.PostRemoved();
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        Assert.True(independent.IsTracking);
        Assert.Same(independentEffect, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void OrdinaryHealthEffect_DoesNotPromiseHealthNotificationCoverage()
    {
        var pawn = NativeReferenceTests.CreatePawn(7);
        var effect = new Hediff { pawn = pawn, loadID = 7 };
        pawn.health.hediffSet.hediffs.Add(effect);
        using var reference = new NativeReference(effect);
        reference.StartTracking(new ReferenceTrackingSession());

        Assert.Equal(ReferenceTrackingCoverage.Maps, reference.TrackingCoverage);
        Assert.True(reference.Capture().EffectAttached);
    }

    [Fact]
    public void SilentResurrectionRemoval_InterruptsEachObserverWithoutBorrowingTheSurvivorsCallback()
    {
        using var game = new GameScope();
        var pawn = NativeReferenceTests.CreatePawn(12);
        var survivor = NativeReferenceTests.AddEffect(pawn, 10);
        var removed = NativeReferenceTests.AddEffect(pawn, 11);
        using var first = new NativeReference(removed);
        using var second = new NativeReference(removed);
        using var independent = new NativeReference(survivor);
        using var pawnReference = new NativeReference(pawn);
        foreach (var reference in new[] { first, second, independent, pawnReference })
            reference.StartTracking();
        removed.Notify_PawnDied(null);
        survivor.Notify_PawnDied(null);

        // Model the inspected native path: remove without PostRemoved, then notify surviving effects.
        // Complete death, corpse handling and resurrection are verified in a separate game scenario.
        pawn.health.hediffSet.hediffs.Remove(removed);
        survivor.Notify_Resurrected();
        var replacement = NativeReferenceTests.AddEffect(pawn, 12);

        Assert.Equal(removed.Metadata.Identity, replacement.Metadata.Identity);
        Assert.Same(removed, first.Capture().Effect);
        Assert.False(first.Capture().EffectAttached);
        Assert.False(first.IsTracking);
        var expected = ReferenceChanges.Died | ReferenceChanges.EffectDetached | ReferenceChanges.TrackingInterrupted;
        Assert.Equal(expected, first.TakeChanges());
        Assert.Equal(ReferenceChanges.None, first.TakeChanges());
        Assert.Equal(expected, second.TakeChanges());
        Assert.False(second.IsTracking);
        Assert.Same(removed, second.Capture().Effect);
        Assert.Equal(ReferenceChanges.Died | ReferenceChanges.Resurrected, independent.TakeChanges());
        Assert.True(independent.IsTracking);
        Assert.Equal(ReferenceChanges.None, pawnReference.PendingChanges);
        Assert.True(pawnReference.IsTracking);
        Assert.Collection(pawn.health.hediffSet.hediffs,
            effect => Assert.Same(survivor, effect), effect => Assert.Same(replacement, effect));
    }

    [Fact]
    public void CorpseNotification_OnlySignalsDiscardWhenTheNativePawnWasActuallyDiscarded()
    {
        using var game = new GameScope();
        var pawn = NativeReferenceTests.CreatePawn(11);
        var effect = NativeReferenceTests.AddEffect(pawn, 1);
        using var reference = new NativeReference(effect);
        reference.StartTracking();

        effect.Notify_PawnCorpseDestroyed();

        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        Assert.True(reference.IsTracking);
        // Set the native state for this callback check; this is not a complete Pawn.Discard test.
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pawn, (sbyte)-3);
        effect.Notify_PawnCorpseDestroyed();
        Assert.True(reference.Capture().Discarded);
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.Discarded | ReferenceChanges.TrackingInterrupted, reference.TakeChanges());
    }

    [Fact]
    public void DisposedReference_StopsObservingWithoutConsumingIndependentChanges()
    {
        var pawn = NativeReferenceTests.CreatePawn(8);
        var session = new ReferenceTrackingSession();
        var reference = new NativeReference(pawn);
        reference.StartTracking(session);
        reference.Dispose();
        reference.TakeChanges();

        session.Notify(pawn, null, ReferenceChanges.EnteredContext, 1);

        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        Assert.Equal(NativeReferenceResolution.Resolved, reference.Capture().Resolution);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedObserver_DoesNotKeepItsTargetAliveAndOtherObserversRemainActive(bool targetsEffect)
    {
        var session = new ReferenceTrackingSession();
        var livePawn = NativeReferenceTests.CreatePawn(20);
        using var live = new NativeReference(livePawn);
        live.StartTracking(session);
        var abandoned = CreateAbandonedObserver(session, targetsEffect);

        CollectAbandonedObjects();

        Assert.False(abandoned.Reference.IsAlive);
        Assert.False(abandoned.Pawn.IsAlive);
        if (abandoned.Effect is not null) Assert.False(abandoned.Effect.IsAlive);
        session.Notify(livePawn, null, ReferenceChanges.EnteredContext, 71);
        Assert.Equal(ReferenceChanges.EnteredContext, live.TakeChanges());
        Assert.True(live.IsTracking);
        GC.KeepAlive(session);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EmptySession_ReleasesNotificationAllocationsAndCanTrackAgain(bool collect, bool mapRemoval)
    {
        var session = new ReferenceTrackingSession();
        var pawn = NativeReferenceTests.CreatePawn(23);
        if (collect)
        {
            var abandoned = CreateAbandonedObserver(session, targetsEffect: true);
            CollectAbandonedObjects();
            Assert.False(abandoned.Reference.IsAlive);
        }
        else
        {
            using var previous = new NativeReference(pawn);
            previous.StartTracking(session);
        }

        if (mapRemoval) session.MapRemoved(72);
        else session.Notify(pawn, null, ReferenceChanges.LeftContext, 72);
        // Warm both notification paths before measuring this empty, reconciled session.
        for (var i = 0; i < 32; i++)
        {
            session.Notify(pawn, null, ReferenceChanges.LeftContext, 72);
            session.MapRemoved(72);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++)
        {
            session.Notify(pawn, null, ReferenceChanges.LeftContext, 72);
            session.MapRemoved(72);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);

        using var current = new NativeReference(pawn);
        current.StartTracking(session);
        session.Notify(pawn, null, ReferenceChanges.EnteredContext, 73);
        session.MapRemoved(73);

        Assert.True(current.IsTracking);
        Assert.Equal(ReferenceChanges.EnteredContext | ReferenceChanges.ContextRemoved, current.TakeChanges());
        Assert.Equal(NativeReferenceResolution.Resolved, current.Capture().Resolution);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveDelivery_DoesNotAllocatePerObserver(bool mapRemoval)
    {
        var session = new ReferenceTrackingSession();
        var pawn = NativeReferenceTests.CreatePawn(24);
        var references = new List<NativeReference>();
        try
        {
            AddObservers(1);
            var singleObserverBytes = Measure();
            AddObservers(511);
            var manyObserverBytes = Measure();

            Assert.True(manyObserverBytes <= singleObserverBytes,
                $"Notification allocations grew from {singleObserverBytes} to {manyObserverBytes} bytes.");
            var expected = mapRemoval ? ReferenceChanges.ContextRemoved : ReferenceChanges.EnteredContext;
            Assert.All(references, reference => Assert.Equal(expected, reference.TakeChanges()));
        }
        finally
        {
            foreach (var reference in references) reference.Dispose();
        }

        void AddObservers(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var reference = new NativeReference(pawn);
                reference.StartTracking(session);
                references.Add(reference);
            }
            session.Notify(pawn, null, ReferenceChanges.EnteredContext, 74);
            foreach (var reference in references) reference.TakeChanges();
        }

        long Measure()
        {
            for (var i = 0; i < 32; i++) Deliver();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 128; i++) Deliver();
            return GC.GetAllocatedBytesForCurrentThread() - allocated;
        }

        void Deliver()
        {
            if (mapRemoval) session.MapRemoved(74);
            else session.Notify(pawn, null, ReferenceChanges.EnteredContext, 74);
        }
    }

    [Fact]
    public void DetachmentDuringDelivery_ReachesEveryObserverAndPreservesOtherTargets()
    {
        using var game = new GameScope();
        var pawn = NativeReferenceTests.CreatePawn(25);
        var otherPawn = NativeReferenceTests.CreatePawn(26);
        var removed = NativeReferenceTests.AddEffect(pawn, 25);
        var survivor = NativeReferenceTests.AddEffect(pawn, 26);
        var removedObservers = new List<NativeReference>();
        var survivingObservers = new List<NativeReference>();
        using var pawnObserver = new NativeReference(pawn);
        using var unrelated = new NativeReference(otherPawn);
        pawnObserver.StartTracking();
        unrelated.StartTracking();
        try
        {
            for (var i = 0; i < 128; i++)
            {
                // Interleave registrations; delivery must not depend on enumeration order.
                var departing = new NativeReference(removed);
                departing.StartTracking();
                removedObservers.Add(departing);
                var staying = new NativeReference(survivor);
                staying.StartTracking();
                survivingObservers.Add(staying);
            }
            pawn.health.hediffSet.hediffs.Remove(removed);
            removed.PostRemoved();

            Assert.All(removedObservers, reference =>
            {
                // Read pending facts before snapshot reconciliation can detect a missed detachment.
                Assert.Equal(ReferenceChanges.EffectDetached | ReferenceChanges.TrackingInterrupted,
                    reference.PendingChanges);
                Assert.False(reference.IsTracking);
                Assert.Same(removed, reference.Capture().Effect);
            });
            Assert.All(survivingObservers, reference => Assert.Equal(ReferenceChanges.None, reference.PendingChanges));
            survivor.Notify_Resurrected();
            Assert.All(survivingObservers, reference =>
            {
                Assert.True(reference.IsTracking);
                Assert.Equal(ReferenceChanges.Resurrected, reference.TakeChanges());
            });
            Assert.Equal(ReferenceChanges.None, pawnObserver.PendingChanges);
            Assert.Equal(ReferenceChanges.None, unrelated.PendingChanges);
            Assert.True(pawnObserver.IsTracking);
            Assert.True(unrelated.IsTracking);
            Assert.Same(survivor, Assert.Single(pawn.health.hediffSet.hediffs));
        }
        finally
        {
            foreach (var reference in removedObservers) reference.Dispose();
            foreach (var reference in survivingObservers) reference.Dispose();
        }
    }

    [Fact]
    public void DisposedObserver_ReleasesItsSessionWhileKeepingHistoricalIdentity()
    {
        using var reference = new NativeReference(NativeReferenceTests.CreatePawn(22));
        var session = StartWithTemporarySession(reference);
        var identity = reference.Capture().PawnIdentity;

        reference.Dispose();
        CollectAbandonedObjects();

        Assert.False(session.IsAlive);
        Assert.False(reference.IsTracking);
        Assert.Equal(identity, reference.Capture().PawnIdentity);
        Assert.Equal(NativeReferenceResolution.Resolved, reference.Capture().Resolution);
        Assert.Equal(ReferenceChanges.TrackingInterrupted, reference.TakeChanges());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Reference, WeakReference Pawn, WeakReference? Effect)
        CreateAbandonedObserver(ReferenceTrackingSession session, bool targetsEffect)
    {
        // No native world owns these minimal objects; only the reference and health list connect them.
        var pawn = NativeReferenceTests.CreatePawn(21);
        var effect = targetsEffect ? NativeReferenceTests.AddEffect(pawn, 21) : null;
        var reference = effect is null ? new NativeReference(pawn) : new NativeReference(effect);
        reference.StartTracking(session);
        session.Notify(pawn, null, ReferenceChanges.EnteredContext, 75);
        return (new WeakReference(reference), new WeakReference(pawn), effect is null ? null : new WeakReference(effect));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference StartWithTemporarySession(NativeReference reference)
    {
        var session = new ReferenceTrackingSession();
        reference.StartTracking(session);
        return new WeakReference(session);
    }

    private static void CollectAbandonedObjects()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void HandlesCannotCrossGames_EvenWhenNativeIdentitiesAreEqual()
    {
        using var firstGame = new GameScope();
        var pawn = NativeReferenceTests.CreatePawn(9);
        using var active = new NativeReference(pawn);
        using var neverStarted = new NativeReference(pawn);
        active.StartTracking();
        using var secondGame = new GameScope();
        var differentPawn = NativeReferenceTests.CreatePawn(9);
        using var current = new NativeReference(differentPawn);
        current.StartTracking();

        Assert.Equal(active.Capture().PawnIdentity, current.Capture().PawnIdentity);
        Assert.Equal(NativeReferenceResolution.DifferentGame, active.Capture().Resolution);
        Assert.Equal(NativeReferenceResolution.DifferentGame, neverStarted.Capture().Resolution);
        Assert.False(active.IsTracking);
        Assert.Throws<InvalidOperationException>(active.StartTracking);
        Assert.Throws<InvalidOperationException>(neverStarted.StartTracking);
        Assert.Equal(NativeReferenceResolution.Resolved, current.Capture().Resolution);
    }

    [Fact]
    public void InactiveScribeAlone_DoesNotPermitTrackingBeforeNativeGameInitialization()
    {
        using var game = new GameScope(ready: false);
        using var reference = new NativeReference(NativeReferenceTests.CreatePawn(10));
        Assert.Equal(LoadSaveMode.Inactive, Scribe.mode);

        Assert.Throws<InvalidOperationException>(reference.StartTracking);

        Current.Game.GetComponent<ReferenceTrackingGameComponent>().FinalizeInit();
        reference.StartTracking();
        Assert.True(reference.IsTracking);
    }

    private static Map CreateMap(int id)
    {
        var map = new Map { uniqueID = id };
        map.events = new MapEvents(map);
        return map;
    }

    private sealed class GameScope : IDisposable
    {
        private readonly NativeGameTestScope _game = new();

        internal GameScope(bool ready = true)
        {
            var game = _game.Game;
            var component = new ReferenceTrackingGameComponent(game);
            game.components.Add(component);
            if (ready) component.FinalizeInit();
        }

        public void Dispose() => _game.Dispose();
    }
}
