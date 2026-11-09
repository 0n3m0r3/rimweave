using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.References;
using Verse;

namespace RimWeave.Example.References;

// Persisted in both configurations. Loading only restores observations; it never runs a command.
public sealed class ReferenceScenarioGameComponent(Game game) : GameComponent
{
    internal const string MarkerDefName = "RimWeave_ReferenceMarker";
    private const ReferenceChanges RoundTripChanges = ReferenceChanges.LeftContext | ReferenceChanges.EnteredContext;
    private NativeReference? _pawnA;
    private NativeReference? _pawnB;
    private NativeReference? _effectA;
    private NativeReference? _effectB;
    private NativeReference? _otherEffect;
    private int _commandCount;
    private int _savedCommandCount;
    private int _savedMarkerCount;
    private bool _removed;
    private bool _loadedBaseline;
    private List<ReferenceChanges> _savedPending = [];
    private List<bool> _savedTracking = [];
    private string? _savedPawnIdentity;
    private string? _savedEffectIdentity;
    private string? _savedOtherIdentity;

    internal NativeReference? PawnA => _pawnA;
    internal NativeReference? PawnB => _pawnB;
    internal NativeReference? EffectA => _effectA;
    internal NativeReference? EffectB => _effectB;
    internal NativeReference? OtherEffect => _otherEffect;

    private NativeReference[] Observers => [_pawnA!, _pawnB!, _effectA!, _effectB!];

    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && _pawnA is not null)
        {
            // Revalidation can stop obsolete tracking. Record the resulting save boundary.
            foreach (var reference in Observers) reference.Capture();
            _savedPending = Observers.Select(reference => reference.PendingChanges).ToList();
            _savedTracking = Observers.Select(reference => reference.IsTracking).ToList();
            _savedPawnIdentity = _pawnA.Capture().PawnIdentity;
            _savedEffectIdentity = _effectA!.Capture().EffectIdentity;
            _savedOtherIdentity = _otherEffect!.Capture().EffectIdentity;
            _savedMarkerCount = CountMarkers(_pawnA.Capture().Pawn);
            _savedCommandCount = _commandCount;
        }

        Scribe_Deep.Look(ref _pawnA, "pawnA");
        Scribe_Deep.Look(ref _pawnB, "pawnB");
        Scribe_Deep.Look(ref _effectA, "effectA");
        Scribe_Deep.Look(ref _effectB, "effectB");
        Scribe_Deep.Look(ref _otherEffect, "otherEffect");
        Scribe_Values.Look(ref _commandCount, "commandCount");
        Scribe_Values.Look(ref _savedCommandCount, "savedCommandCount");
        Scribe_Values.Look(ref _savedMarkerCount, "savedMarkerCount");
        Scribe_Values.Look(ref _removed, "removed");
        Scribe_Collections.Look(ref _savedPending, "savedPending", LookMode.Value);
        Scribe_Collections.Look(ref _savedTracking, "savedTracking", LookMode.Value);
        Scribe_Values.Look(ref _savedPawnIdentity, "savedPawnIdentity");
        Scribe_Values.Look(ref _savedEffectIdentity, "savedEffectIdentity");
        Scribe_Values.Look(ref _savedOtherIdentity, "savedOtherIdentity");
        if (Scribe.mode == LoadSaveMode.LoadingVars) _loadedBaseline = true;
    }

    internal string Start(Pawn pawn)
    {
        RequireCurrentGame();
        if (_pawnA is not null)
            throw new InvalidOperationException("A scenario already exists in this save. Inspect or resume it.");
        RequireAvailablePawn(pawn);
        if (CountMarkers(pawn) != 0)
            throw new InvalidOperationException("The pawn already has reference scenario markers.");

        var definition = DefDatabase<HediffDef>.GetNamed(MarkerDefName);
        var target = (ContributionHediff)HediffMaker.MakeHediff(definition, pawn);
        var other = (ContributionHediff)HediffMaker.MakeHediff(definition, pawn);
        // Deliberately identical descriptions: only native identity distinguishes these instances.
        var identity = new ContributionId("rimweave.example", "reference-marker");
        target.Metadata.AssignIdentity(identity);
        other.Metadata.AssignIdentity(identity);
        pawn.health.AddHediff(target);
        pawn.health.AddHediff(other);
        Track(pawn, target, other);
        BeginAction();
        return Result("START", ExactTargets() && Observers.All(reference => reference.IsTracking), Details());
    }

    internal void Track(Pawn pawn, Hediff target, Hediff other)
    {
        RequireCurrentGame();
        if (_pawnA is not null || ReferenceEquals(target, other)
            || !ReferenceEquals(target.pawn, pawn) || !ReferenceEquals(other.pawn, pawn))
            throw new InvalidOperationException("Tracking requires a fresh scenario and two distinct effects on the same pawn.");
        _pawnA = new NativeReference(pawn);
        _pawnB = new NativeReference(pawn);
        _effectA = new NativeReference(target);
        _effectB = new NativeReference(target);
        _otherEffect = new NativeReference(other);
        foreach (var reference in Observers) reference.StartTracking();
    }

    internal string RoundTrip()
    {
        RequireScenario();
        if (!Observers.All(reference => reference.IsTracking))
            throw new InvalidOperationException("All four observers must be active. Resume explicitly after loading.");
        var pawn = _pawnA!.Capture().Pawn!;
        RequireAvailablePawn(pawn);
        var map = pawn.Map;
        var cell = pawn.Position;
        var rotation = pawn.Rotation;
        BeginAction();
        pawn.DeSpawn();
        // Same synchronous command, same map and cell; use real native notifications.
        GenSpawn.Spawn(pawn, cell, map, rotation);
        bool passed = pawn.Spawned && ReferenceEquals(pawn.Map, map) && pawn.Position == cell
            && Observers.All(reference => (reference.PendingChanges & RoundTripChanges) == RoundTripChanges)
            && ExactTargets();
        return Result("DEPARTURE/RETURN", passed, Details());
    }

    internal string AcknowledgeA()
    {
        RequireScenario();
        var pawnBefore = _pawnB!.PendingChanges;
        var effectBefore = _effectB!.PendingChanges;
        if ((pawnBefore & RoundTripChanges) != RoundTripChanges
            || (effectBefore & RoundTripChanges) != RoundTripChanges)
            throw new InvalidOperationException("Run departure and return before acknowledging consumer A.");
        BeginAction();
        var pawnTaken = _pawnA!.TakeChanges();
        var effectTaken = _effectA!.TakeChanges();
        bool passed = (pawnTaken & RoundTripChanges) == RoundTripChanges
            && (effectTaken & RoundTripChanges) == RoundTripChanges
            && _pawnA.PendingChanges == ReferenceChanges.None && _effectA.PendingChanges == ReferenceChanges.None
            && _pawnB.PendingChanges == pawnBefore && _effectB.PendingChanges == effectBefore;
        return Result("ACKNOWLEDGE A", passed, $"taken pawn={pawnTaken}; effect={effectTaken}\n{Details()}");
    }

    internal string Resume()
    {
        RequireScenario();
        if (_removed || !ExactTargets())
            throw new InvalidOperationException("The exact target is no longer attached; no replacement will be selected.");
        var pending = Observers.Select(reference => reference.PendingChanges).ToArray();
        BeginAction();
        foreach (var reference in Observers) reference.StartTracking();
        bool passed = Observers.All(reference => reference.IsTracking)
            && Observers.Select(reference => reference.PendingChanges).SequenceEqual(pending);
        return Result("RESUME", passed, Details());
    }

    internal string RemoveExactEffect()
    {
        RequireScenario();
        if (_removed || !ExactTargets())
            throw new InvalidOperationException("The exact target is unavailable or already removed.");
        if (!_effectA!.IsTracking || !_effectB!.IsTracking)
            throw new InvalidOperationException("Resume tracking explicitly before testing removal notifications.");
        var target = _effectA.Capture();
        var other = _otherEffect!.Capture();
        RequireAvailablePawn(target.Pawn!);
        BeginAction();
        var first = HediffOperations.Remove(target.Pawn!, target.Effect!);
        var repeated = HediffOperations.Remove(target.Pawn!, target.Effect!);
        _removed = first.Status == HediffRemovalStatus.Removed;
        var a = _effectA.Capture();
        var b = _effectB.Capture();
        bool passed = _removed && repeated.Status == HediffRemovalStatus.NotPresent
            && a.EffectAttached == false && b.EffectAttached == false
            && ReferenceEquals(a.Effect, target.Effect) && ReferenceEquals(b.Effect, target.Effect)
            && ReferenceEquals(_otherEffect.Capture().Effect, other.Effect)
            && _otherEffect.Capture().EffectAttached == true
            && !_effectA.IsTracking && !_effectB.IsTracking
            && (_effectA.PendingChanges & ReferenceChanges.EffectDetached) != 0
            && (_effectB.PendingChanges & ReferenceChanges.EffectDetached) != 0;
        return Result("EXACT REMOVAL", passed,
            $"first={first.Status}; repeated={repeated.Status}; similar effect survives\n{Details()}");
    }

    internal string Inspect()
    {
        RequireCurrentGame();
        if (_pawnA is null) return "Reference scenario: not started; no effects or observers created.";
        RequireScenario();
        if (!_loadedBaseline) return $"Reference scenario INSPECT (no unchecked load boundary)\n{Details()}";

        bool expectedAvailable = _savedPending?.Count == 4 && _savedTracking?.Count == 4;
        bool observations = expectedAvailable && Observers.Select((reference, index) =>
            reference.PendingChanges == (_savedPending![index]
                | (_savedTracking![index] ? ReferenceChanges.TrackingInterrupted : ReferenceChanges.None))).All(value => value);
        var pawn = _pawnA.Capture();
        var target = _effectA!.Capture();
        var other = _otherEffect!.Capture();
        bool identities = pawn.PawnIdentity == _savedPawnIdentity
            && target.EffectIdentity == _savedEffectIdentity && other.EffectIdentity == _savedOtherIdentity;
        bool exact = _removed
            ? target.EffectAttached != true && !ReferenceEquals(target.Effect, other.Effect)
                && _effectB!.Capture().EffectIdentity == target.EffectIdentity && other.EffectAttached == true
            : ExactTargets();
        bool noReplay = _commandCount == _savedCommandCount && CountMarkers(pawn.Pawn) == _savedMarkerCount;
        bool noResume = Observers.All(reference => !reference.IsTracking);
        return Result("LOAD", observations && identities && exact && noReplay && noResume,
            $"pendingPreserved={observations}; identitiesPreserved={identities}; exactTargets={exact}; "
            + $"noReplay={noReplay}; noAutoResume={noResume}; savedCommands={_savedCommandCount}; "
            + $"savedMarkers={_savedMarkerCount}\n{Details()}");
    }

    private bool ExactTargets()
    {
        var pawnA = _pawnA!.Capture();
        var pawnB = _pawnB!.Capture();
        var effectA = _effectA!.Capture();
        var effectB = _effectB!.Capture();
        var other = _otherEffect!.Capture();
        return pawnA.Resolution == NativeReferenceResolution.Resolved
            && ReferenceEquals(pawnA.Pawn, pawnB.Pawn)
            && ReferenceEquals(pawnA.Pawn, effectA.Pawn) && ReferenceEquals(pawnA.Pawn, effectB.Pawn)
            && ReferenceEquals(pawnA.Pawn, other.Pawn)
            && effectA.Effect is not null && ReferenceEquals(effectA.Effect, effectB.Effect)
            && !ReferenceEquals(effectA.Effect, other.Effect)
            && effectA.EffectAttached == true && effectB.EffectAttached == true && other.EffectAttached == true;
    }

    private string Details()
    {
        var names = new[] { "pawn A", "pawn B", "effect A", "effect B", "similar effect" };
        var references = Observers.Append(_otherEffect!).ToArray();
        return $"commands={_commandCount}; markers={CountMarkers(_pawnA!.Capture().Pawn)}; removed={_removed}\n"
            + string.Join("\n", references.Select((reference, index) =>
            {
                var state = reference.Capture();
                return $"{names[index]}: pawn={state.PawnIdentity}; effect={state.EffectIdentity}; "
                    + $"resolution={state.Resolution}; map={state.Map?.uniqueID}; spawned={state.Spawned}; "
                    + $"dead={state.Dead}; destroyed={state.Destroyed}; attached={state.EffectAttached}; "
                    + $"tracking={reference.IsTracking}; coverage={reference.TrackingCoverage}; pending={reference.PendingChanges}";
            }));
    }

    private static int CountMarkers(Pawn? pawn) =>
        pawn?.health?.hediffSet.hediffs.Count(effect => effect.def?.defName == MarkerDefName) ?? 0;

    private void BeginAction()
    {
        _loadedBaseline = false;
        _commandCount++;
    }

    private void RequireCurrentGame()
    {
        if (!ReferenceEquals(Current.Game, game) || Scribe.mode != LoadSaveMode.Inactive)
            throw new InvalidOperationException("The scenario requires its owning, fully loaded game.");
    }

    private void RequireScenario()
    {
        RequireCurrentGame();
        if (_pawnA is null || _pawnB is null || _effectA is null || _effectB is null || _otherEffect is null)
            throw new InvalidOperationException("Start a complete reference scenario first.");
    }

    private static void RequireAvailablePawn(Pawn pawn)
    {
        if (pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.Map != Find.CurrentMap || pawn.health is null)
            throw new InvalidOperationException("The target must be a living pawn directly on the current map.");
    }

    private static string Result(string step, bool passed, string details) =>
        $"Reference scenario {step} {(passed ? "PASS" : "FAIL")}:\n{details}";
}
