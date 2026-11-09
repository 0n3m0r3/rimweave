using System.Reflection;
using System.Runtime.CompilerServices;
using RimWeave.Contributions;
using RimWeave.Example.References;
using RimWeave.Health;
using RimWeave.References;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;
#if DEBUG
using RimWeave.Example.Debugging;
#endif

namespace RimWeave.Tests.References;

[Collection(ScribeTestCollection.Name)]
public sealed class ReferenceScenarioTests : IDisposable
{
    private readonly NativeGameTestScope _game = new();

    public ReferenceScenarioTests()
    {
        var tracking = new ReferenceTrackingGameComponent(_game.Game);
        _game.Game.components.Add(tracking);
        tracking.FinalizeInit();
    }

    public void Dispose() => _game.Dispose();

    [Fact]
    public void EmptyScenario_RemainsInactiveThroughInspectionAndNativeLoad()
    {
        using var scribe = new ScribeTestScope();
        var scenario = new ReferenceScenarioGameComponent(_game.Game);

        scenario.FinalizeInit();
        scenario.StartedNewGame();
        scenario.Inspect();
        var loaded = scribe.RoundTrip(scenario, _game.Game);
        loaded.FinalizeInit();
        loaded.LoadedGame();
        loaded.Inspect();

        Assert.Null(scenario.PawnA);
        Assert.Null(scenario.PawnB);
        Assert.Null(scenario.EffectA);
        Assert.Null(scenario.EffectB);
        Assert.Null(scenario.OtherEffect);
        Assert.Null(loaded.PawnA);
        Assert.Null(loaded.PawnB);
        Assert.Null(loaded.EffectA);
        Assert.Null(loaded.EffectB);
        Assert.Null(loaded.OtherEffect);
    }

    [Fact]
    public void AcknowledgeA_ConsumesOnlyItsOwnPawnAndEffectObservations()
    {
        using var definitions = new ScenarioDefinitions();
        var graph = definitions.CreateGraph(_game.Game);
        var scenario = graph.Scenario!;
        NotifyDepartureAndReturn(graph.Pawn!);
        var expected = ReferenceChanges.LeftContext | ReferenceChanges.EnteredContext;

        scenario.Inspect();

        Assert.All(Observers(scenario), reference => Assert.Equal(expected, reference.PendingChanges));
        Assert.Contains("ACKNOWLEDGE A PASS", scenario.AcknowledgeA());
        Assert.Equal(ReferenceChanges.None, scenario.PawnA!.PendingChanges);
        Assert.Equal(ReferenceChanges.None, scenario.EffectA!.PendingChanges);
        Assert.Equal(expected, scenario.PawnB!.PendingChanges);
        Assert.Equal(expected, scenario.EffectB!.PendingChanges);
        Assert.Same(scenario.EffectA.Capture().Effect, scenario.EffectB.Capture().Effect);
        Assert.NotSame(scenario.EffectA.Capture().Effect, scenario.OtherEffect!.Capture().Effect);
        Assert.Equal(2, graph.Pawn!.health.hediffSet.hediffs.Count);
    }

    [Fact]
    public void SavedScenario_PreservesIndependentObservationsWithoutReplayingOrResuming()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ScenarioDefinitions();
        var original = definitions.CreateGraph(_game.Game);
        NotifyDepartureAndReturn(original.Pawn!);
        original.Scenario!.AcknowledgeA();
        var targetIdentity = original.Scenario.EffectA!.Capture().EffectIdentity;

        var loaded = scribe.RoundTrip(original);
        loaded.Scenario!.FinalizeInit();
        loaded.Scenario.LoadedGame();
        loaded = scribe.RoundTrip(loaded);
        var scenario = loaded.Scenario!;

        Assert.Contains("LOAD PASS", scenario.Inspect());
        Assert.Contains("LOAD PASS", scenario.Inspect());
        Assert.Equal(2, loaded.Pawn!.health.hediffSet.hediffs.Count);
        Assert.Same(loaded.Pawn.health.hediffSet.hediffs[1], scenario.EffectA!.Capture().Effect);
        Assert.Equal(targetIdentity, scenario.EffectA.Capture().EffectIdentity);
        Assert.Equal(ReferenceChanges.TrackingInterrupted, scenario.PawnA!.PendingChanges);
        Assert.Equal(ReferenceChanges.TrackingInterrupted, scenario.EffectA.PendingChanges);
        var retained = ReferenceChanges.LeftContext | ReferenceChanges.EnteredContext
            | ReferenceChanges.TrackingInterrupted;
        Assert.Equal(retained, scenario.PawnB!.PendingChanges);
        Assert.Equal(retained, scenario.EffectB!.PendingChanges);
        Assert.All(Observers(scenario), reference => Assert.False(reference.IsTracking));

        Assert.Contains("RESUME PASS", scenario.Resume());

        Assert.All(Observers(scenario), reference => Assert.True(reference.IsTracking));
        Assert.Equal(ReferenceChanges.TrackingInterrupted, scenario.PawnA.PendingChanges);
        Assert.Equal(ReferenceChanges.TrackingInterrupted, scenario.EffectA.PendingChanges);
        Assert.Equal(retained, scenario.PawnB.PendingChanges);
        Assert.Equal(retained, scenario.EffectB.PendingChanges);
        Assert.Equal(2, loaded.Pawn.health.hediffSet.hediffs.Count);
    }

    [Fact]
    public void MissingSavedTarget_IsReportedAndNeverReplacedByTheMatchingSurvivor()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new ScenarioDefinitions();
        var original = definitions.CreateGraph(_game.Game);
        var target = Assert.IsType<ContributionHediff>(original.Scenario!.EffectA!.Capture().Effect);
        var targetIdentity = target.GetUniqueLoadID();
        // Model an external disappearance in the controlled save graph, not a complete health operation.
        original.Pawn!.health.hediffSet.hediffs.Remove(target);
        target.PostRemoved();

        var loaded = scribe.RoundTrip(original);
        var scenario = loaded.Scenario!;
        var survivor = Assert.Single(loaded.Pawn!.health.hediffSet.hediffs);

        Assert.Contains("LOAD FAIL", scenario.Inspect());
        Assert.Equal(NativeReferenceResolution.Unresolved, scenario.EffectA!.Capture().Resolution);
        Assert.Equal(targetIdentity, scenario.EffectA.Capture().EffectIdentity);
        Assert.Null(scenario.EffectA.Capture().Effect);
        Assert.Null(scenario.EffectB!.Capture().Effect);
        Assert.Same(survivor, scenario.OtherEffect!.Capture().Effect);
        Assert.Throws<InvalidOperationException>(() => scenario.Resume());
        Assert.Throws<InvalidOperationException>(() => scenario.RemoveExactEffect());
        Assert.Same(survivor, Assert.Single(loaded.Pawn.health.hediffSet.hediffs));
        Assert.All(Observers(scenario), reference => Assert.False(reference.IsTracking));
    }

    private void NotifyDepartureAndReturn(Pawn pawn)
    {
        // Feed supported observations; the real DeSpawn/SpawnSetup route is checked separately in game.
        var session = _game.Game.GetComponent<ReferenceTrackingGameComponent>().Session;
        session.Notify(pawn, null, ReferenceChanges.LeftContext, 17);
        session.Notify(pawn, null, ReferenceChanges.EnteredContext, 17);
    }

    private static NativeReference[] Observers(ReferenceScenarioGameComponent scenario) =>
        [scenario.PawnA!, scenario.PawnB!, scenario.EffectA!, scenario.EffectB!];

    public sealed class ScenarioGraph : IExposable
    {
        public NativeReferencePersistenceTests.ReferenceTestPawn? Pawn;
        public ReferenceScenarioGameComponent? Scenario;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref Pawn, "pawn");
            Scribe_Deep.Look(ref Scenario, "scenario", Current.Game);
        }
    }

    private sealed class ScenarioDefinitions : IDisposable
    {
        private readonly HediffDef[] _previous = DefDatabase<HediffDef>.AllDefs.ToArray();
        private readonly HediffDef _marker = new()
        {
            defName = ReferenceScenarioGameComponent.MarkerDefName,
            hediffClass = typeof(ContributionHediff), comps = []
        };

        internal ScenarioDefinitions() => DefDatabase<HediffDef>.Add(_marker);

        internal ScenarioGraph CreateGraph(Game game)
        {
            var pawn = new NativeReferencePersistenceTests.ReferenceTestPawn { thingIDNumber = 91 };
            foreach (int id in new[] { 101, 102 })
            {
                var effect = new ContributionHediff { pawn = pawn, def = _marker, loadID = id, comps = [] };
                effect.Metadata.AssignIdentity(new ContributionId("rimweave.example", "reference-marker"));
                pawn.health.hediffSet.hediffs.Add(effect);
            }
            var scenario = new ReferenceScenarioGameComponent(game);
            scenario.Track(pawn, pawn.health.hediffSet.hediffs[1], pawn.health.hediffSet.hediffs[0]);
            return new ScenarioGraph { Pawn = pawn, Scenario = scenario };
        }

        public void Dispose()
        {
            DefDatabase<HediffDef>.Clear();
            DefDatabase<HediffDef>.Add(_previous);
        }
    }

#if DEBUG
    [Fact]
    public void PreparedCommands_RejectExecutionAfterDeveloperModeIsDisabled()
    {
        using var preferences = new DeveloperPreferencesScope();
        var scenario = new ReferenceScenarioGameComponent(_game.Game);
        _game.Game.components.Add(scenario);
        var pawn = NativeReferenceTests.CreatePawn(91);

        foreach (var action in Enum.GetValues<ReferenceScenarioAction>())
        {
            preferences.Data.devMode = true;
            Func<string> prepared = () => ReferenceScenarioActions.Execute(_game.Game, action, pawn);
            preferences.Data.devMode = false;

            string result = prepared();

            Assert.Contains("developer mode", result, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(pawn.health.hediffSet.hediffs);
            Assert.Null(scenario.PawnA);
            Assert.Null(scenario.EffectA);
        }
    }

    [Fact]
    public void PreparedCommand_RejectsAnotherGameBeforeCreatingAScenario()
    {
        using var preferences = new DeveloperPreferencesScope();
        preferences.Data.devMode = true;
        var pawn = NativeReferenceTests.CreatePawn(92);
        Func<string> prepared = () => ReferenceScenarioActions.Execute(
            _game.Game, ReferenceScenarioAction.Start, pawn);
        using var otherGame = new NativeGameTestScope();

        string result = prepared();

        Assert.Contains("game", result, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Empty(otherGame.Game.components);
    }

    private sealed class DeveloperPreferencesScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(Prefs)
            .GetField("data", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = Field.GetValue(null);
        internal PrefsData Data { get; } = (PrefsData)RuntimeHelpers.GetUninitializedObject(typeof(PrefsData));

        internal DeveloperPreferencesScope() => Field.SetValue(null, Data);

        public void Dispose() => Field.SetValue(null, _previous);
    }
#endif
}
