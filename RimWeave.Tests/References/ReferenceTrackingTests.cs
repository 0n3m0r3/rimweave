using System.Reflection;
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
