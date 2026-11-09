using System.Xml.Linq;
using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.References;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.References;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeReferencePersistenceTests : IDisposable
{
    private readonly NativeGameTestScope _game = new();

    public void Dispose() => _game.Dispose();

    [Fact]
    public void RoundTrip_ResolvesTheExactPawnAndEffectWithoutSubstitutingAnIdenticalContribution()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var original = definitions.CreateGraph();
        var originalEffect = original.Pawn!.health.hediffSet.hediffs[1];

        var loaded = scribe.RoundTrip(original);
        loaded = scribe.RoundTrip(loaded);

        Assert.Equal(NativeReferenceResolution.Loading, loaded.DuringLoading);
        var pawn = loaded.References[0].Capture();
        var effect = loaded.References[1].Capture();
        Assert.Equal(NativeReferenceResolution.Resolved, pawn.Resolution);
        Assert.Same(loaded.Pawn, pawn.Pawn);
        Assert.Null(pawn.Effect);
        Assert.Null(pawn.EffectAttached);
        Assert.Equal(NativeReferenceResolution.Resolved, effect.Resolution);
        Assert.Same(loaded.Pawn, effect.Pawn);
        Assert.Equal(2, loaded.Pawn!.health.hediffSet.hediffs.Count);
        Assert.Same(loaded.Pawn.health.hediffSet.hediffs[1], effect.Effect);
        Assert.NotSame(originalEffect, effect.Effect);
        Assert.Equal(originalEffect.GetUniqueLoadID(), effect.EffectIdentity);
        Assert.True(effect.EffectAttached);
        Assert.All(loaded.References, reference => Assert.False(reference.IsTracking));
    }

    [Fact]
    public void MissingEffect_KeepsItsIdentityAcrossSavesWithoutSelectingTheOtherMatchingEffect()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var original = definitions.CreateGraph();
        var intended = original.References[1].Capture();
        original.Pawn!.health.hediffSet.hediffs.RemoveAt(1);
        Assert.False(original.References[1].Capture().EffectAttached);

        var loaded = scribe.RoundTrip(original);
        loaded = scribe.RoundTrip(loaded);

        var result = loaded.References[1].Capture();
        Assert.Equal(NativeReferenceResolution.Unresolved, result.Resolution);
        Assert.Equal(intended.PawnIdentity, result.PawnIdentity);
        Assert.Equal(intended.EffectIdentity, result.EffectIdentity);
        Assert.Null(result.Effect);
        Assert.Null(result.Destroyed);
        Assert.Single(loaded.Pawn!.health.hediffSet.hediffs);
        Assert.Equal(NativeReferenceResolution.Resolved, loaded.References[0].Capture().Resolution);
        Assert.Throws<InvalidOperationException>(() =>
            loaded.References[1].StartTracking(new ReferenceTrackingSession()));
    }

    [Fact]
    public void MissingPawn_PreservesUnresolvedIntentInsteadOfClaimingPermanentDisappearance()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var original = definitions.CreateGraph();
        var identity = original.References[0].Capture().PawnIdentity;
        original.Pawn = null;

        var loaded = scribe.RoundTrip(original);
        loaded = scribe.RoundTrip(loaded);

        Assert.Null(loaded.Pawn);
        Assert.All(loaded.References, reference =>
        {
            var result = reference.Capture();
            Assert.Equal(NativeReferenceResolution.Unresolved, result.Resolution);
            Assert.Equal(identity, result.PawnIdentity);
            Assert.Null(result.Pawn);
            Assert.Null(result.Dead);
            Assert.Null(result.Destroyed);
            Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        });
    }

    [Fact]
    public void ActiveRoundTrip_PreservesPendingChangesAndReportsOneInterruptionWithoutResuming()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var original = definitions.CreateGraph();
        var reference = original.References[1];
        var session = new ReferenceTrackingSession();
        reference.StartTracking(session);
        session.Notify(original.Pawn!, null, ReferenceChanges.LeftContext, 17);
        session.Notify(original.Pawn!, null, ReferenceChanges.EnteredContext, 17);
        var observed = ReferenceChanges.LeftContext | ReferenceChanges.EnteredContext;

        var loaded = scribe.RoundTrip(original);

        Assert.True(reference.IsTracking);
        Assert.Equal(observed, reference.PendingChanges);
        Assert.False(loaded.References[1].IsTracking);
        Assert.Equal(ReferenceTrackingCoverage.None, loaded.References[1].TrackingCoverage);
        Assert.Equal(observed | ReferenceChanges.TrackingInterrupted, loaded.References[1].TakeChanges());
        Assert.Equal(ReferenceChanges.None, loaded.References[1].TakeChanges());
        var reloaded = scribe.RoundTrip(loaded);
        Assert.Equal(ReferenceChanges.None, reloaded.References[1].PendingChanges);
        Assert.False(reloaded.References[1].IsTracking);
        Assert.Equal(2, reloaded.Pawn!.health.hediffSet.hediffs.Count);
        reference.Dispose();
    }

    [Fact]
    public void InactiveRoundTrip_DoesNotInventTransitionsOrStartTracking()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var loaded = scribe.RoundTrip(definitions.CreateGraph());

        Assert.All(loaded.References, reference =>
        {
            Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
            Assert.False(reference.IsTracking);
            Assert.Equal(NativeReferenceResolution.Resolved, reference.Capture().Resolution);
        });
    }

    [Fact]
    public void SavedIdentityMismatch_IsReportedWithoutRebindingOrChangingNativeEffects()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var xml = XDocument.Parse(scribe.Save(definitions.CreateGraph()));
        Assert.Single(xml.Descendants("effectIdentity")).Value = "another-native-effect";

        var loaded = scribe.Load<ReferenceGraph>(xml.ToString());

        var result = loaded.References[1].Capture();
        Assert.Equal(NativeReferenceResolution.Mismatch, result.Resolution);
        Assert.Equal("another-native-effect", result.EffectIdentity);
        Assert.Null(result.Effect);
        Assert.Equal(2, loaded.Pawn!.health.hediffSet.hediffs.Count);
        Assert.Throws<InvalidOperationException>(() =>
            loaded.References[1].StartTracking(new ReferenceTrackingSession()));
    }

    [Fact]
    public void LoadingBeforeTrackingComponentExists_BindsTheGameInsteadOfAProvisionalComponent()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        using var firstGame = new NativeGameTestScope();
        var xml = scribe.Save(definitions.CreateGraph());
        using var loadedGame = new NativeGameTestScope();
        Assert.Empty(loadedGame.Game.components);

        var loaded = scribe.Load<ReferenceGraph>(xml);
        var tracking = new ReferenceTrackingGameComponent(loadedGame.Game);
        loadedGame.Game.components.Add(tracking);
        tracking.FinalizeInit();
        loaded.References[1].StartTracking();

        Assert.True(loaded.References[1].IsTracking);
        Assert.Same(loaded.Pawn, loaded.References[1].Capture().Pawn);
        loaded.References[1].Dispose();
    }

    [Fact]
    public void SavingForeignGameReference_PreservesUnavailableIntentWithoutRebindingCollidingNativeIdentities()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        using var firstGame = new NativeGameTestScope();
        var original = definitions.CreateGraph();
        var pawnIdentity = original.References[0].Capture().PawnIdentity;
        var effectIdentity = original.References[1].Capture().EffectIdentity;
        using var secondGame = new NativeGameTestScope();
        var current = definitions.CreateGraph();
        Assert.Equal(original.Pawn!.GetUniqueLoadID(), current.Pawn!.GetUniqueLoadID());
        current.References = original.References;
        Assert.Equal(NativeReferenceResolution.DifferentGame, current.References[0].Capture().Resolution);

        var saved = scribe.Save(current);
        var foreignMarkers = XDocument.Parse(saved).Descendants("foreignGame").ToArray();
        Assert.Equal(2, foreignMarkers.Length);
        Assert.All(foreignMarkers, node => Assert.True(bool.Parse(node.Value)));
        var loaded = scribe.Load<ReferenceGraph>(saved);
        loaded = scribe.RoundTrip(loaded);

        Assert.Same(original.Pawn, original.Pawn.health.hediffSet.hediffs[1].pawn);
        Assert.Equal(pawnIdentity, loaded.Pawn!.GetUniqueLoadID());
        Assert.Equal(2, loaded.Pawn.health.hediffSet.hediffs.Count);
        Assert.Equal(effectIdentity, loaded.Pawn.health.hediffSet.hediffs[1].GetUniqueLoadID());
        Assert.All(loaded.References, reference =>
        {
            var result = reference.Capture();
            Assert.Equal(NativeReferenceResolution.DifferentGame, result.Resolution);
            Assert.Equal(pawnIdentity, result.PawnIdentity);
            Assert.Null(result.Pawn);
            Assert.Null(result.Effect);
            Assert.False(reference.IsTracking);
            Assert.Throws<InvalidOperationException>(() => reference.StartTracking(new ReferenceTrackingSession()));
        });
        Assert.Equal(effectIdentity, loaded.References[1].Capture().EffectIdentity);
    }

    [Fact]
    public void ContradictorySavedTargetKind_IsMismatchInsteadOfBypassingEffectAttachmentChecks()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var original = definitions.CreateGraph();
        var expectedIdentity = original.References[1].Capture().EffectIdentity;
        var xml = XDocument.Parse(scribe.Save(original));
        Assert.Single(xml.Descendants("targetsEffect")).Value = "False";

        var loaded = scribe.Load<ReferenceGraph>(xml.ToString());

        var result = loaded.References[1].Capture();
        Assert.Equal(NativeReferenceResolution.Mismatch, result.Resolution);
        Assert.Equal(expectedIdentity, result.EffectIdentity);
        Assert.Null(result.Effect);
        Assert.False(loaded.References[1].IsTracking);
        Assert.Throws<InvalidOperationException>(() =>
            loaded.References[1].StartTracking(new ReferenceTrackingSession()));
    }

    [Fact]
    public void LoadingWithoutCurrentGame_CannotRebindToCollidingTargetsInALaterGame()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ReferenceDefinitions();
        var original = definitions.CreateGraph();
        var pawnIdentity = original.References[0].Capture().PawnIdentity;
        var effectIdentity = original.References[1].Capture().EffectIdentity;
        var saved = scribe.Save(original);
        Current.Game = null;

        var unbound = scribe.Load<ReferenceGraph>(saved);

        Assert.All(unbound.References, reference =>
        {
            var snapshot = reference.Capture();
            Assert.Equal(NativeReferenceResolution.DifferentGame, snapshot.Resolution);
            Assert.Null(snapshot.Pawn);
            Assert.Null(snapshot.Effect);
        });
        using var laterGame = new NativeGameTestScope();
        var current = definitions.CreateGraph();
        Assert.Equal(unbound.Pawn!.GetUniqueLoadID(), current.Pawn!.GetUniqueLoadID());
        Assert.Equal(unbound.Pawn.health.hediffSet.hediffs[1].GetUniqueLoadID(),
            current.Pawn.health.hediffSet.hediffs[1].GetUniqueLoadID());
        current.References = unbound.References;

        var loaded = scribe.RoundTrip(current);
        loaded = scribe.RoundTrip(loaded);

        Assert.All(loaded.References, reference =>
        {
            var snapshot = reference.Capture();
            Assert.Equal(NativeReferenceResolution.DifferentGame, snapshot.Resolution);
            Assert.Equal(pawnIdentity, snapshot.PawnIdentity);
            Assert.Null(snapshot.Pawn);
            Assert.Null(snapshot.Effect);
            Assert.False(reference.IsTracking);
            Assert.Throws<InvalidOperationException>(() => reference.StartTracking(new ReferenceTrackingSession()));
        });
        Assert.Equal(effectIdentity, loaded.References[1].Capture().EffectIdentity);
        Assert.Equal(2, loaded.Pawn!.health.hediffSet.hediffs.Count);
    }

    public sealed class ReferenceGraph : IExposable
    {
        public ReferenceTestPawn? Pawn;
        public List<NativeReference> References = [];
        public NativeReferenceResolution? DuringLoading;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref Pawn, "pawn");
            Scribe_Collections.Look(ref References, "references", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                DuringLoading = References[0].Capture().Resolution;
        }
    }

    // Exercises native load-reference registration and resolution, not the full colony/Pawn load path.
    public sealed class ReferenceTestPawn : Pawn
    {
        public ReferenceTestPawn()
        {
            def = new ThingDef
            {
                defName = "RimWeaveReferenceTestPawn", category = ThingCategory.Pawn,
                race = new RaceProperties()
            };
            health = new Pawn_HealthTracker(this);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref thingIDNumber, "id");
            Scribe_Deep.Look(ref health.hediffSet, "health", this);
        }
    }

    private sealed class ReferenceDefinitions : IDisposable
    {
        private readonly HediffDef[] _previous = DefDatabase<HediffDef>.AllDefs.ToArray();
        private readonly HediffDef _effect = new()
        {
            defName = "RimWeaveReferenceTestEffect", hediffClass = typeof(ContributionHediff), comps = []
        };

        internal ReferenceDefinitions() => DefDatabase<HediffDef>.Add(_effect);

        internal ReferenceGraph CreateGraph()
        {
            var pawn = new ReferenceTestPawn { thingIDNumber = 71 };
            foreach (int id in new[] { 101, 102 })
            {
                var effect = new ContributionHediff { pawn = pawn, def = _effect, loadID = id, comps = [] };
                effect.Metadata.AssignIdentity(new ContributionId("example.owner", "same-contribution"));
                pawn.health.hediffSet.hediffs.Add(effect);
            }
            return new ReferenceGraph
            {
                Pawn = pawn,
                References = [new NativeReference(pawn), new NativeReference(pawn.health.hediffSet.hediffs[1])]
            };
        }

        public void Dispose()
        {
            DefDatabase<HediffDef>.Clear();
            DefDatabase<HediffDef>.Add(_previous);
        }
    }
}
