using RimWeave.Health;
using Verse;

namespace RimWeave.References;

/// <summary>A consumer-owned reference to a native pawn or one exact health effect on that pawn.</summary>
/// <remarks>
/// Save this object from its owner's ExposeData using native deep serialization. Targets are saved
/// as native references, never copied or recovered through contribution identities or source strings.
/// Tracking retains transition kinds, not their count or order. It requires explicit activation and
/// disposal by its owner, and must be restarted after loading. No operation or reservation is created.
/// Create targeted references within their owning game. A target loaded without a current game
/// remains unavailable instead of being adopted by a later game.
/// All access belongs on the simulation thread.
/// </remarks>
public sealed class NativeReference : IExposable, IDisposable
{
    private Pawn? _pawn;
    private Hediff? _effect;
    private bool _targetsEffect;
    private string? _pawnIdentity;
    private string? _effectIdentity;
    private bool _loading;
    private bool _foreignGame;
    private int? _lastMapId;
    private ReferenceTrackingSession? _session;
    private System.WeakReference<Game>? _gameScope = CaptureGameScope();
    private ReferenceChangeTracker _changes = new();

    /// <summary>Creates an unresolved reference for native loading.</summary>
    public NativeReference() { }

    /// <summary>Names the exact pawn without starting observation or retaining its world availability.</summary>
    /// <exception cref="InvalidOperationException">There is no current game to own the reference.</exception>
    public NativeReference(Pawn pawn)
    {
        ArgumentNullException.ThrowIfNull(pawn);
        if (_gameScope is null)
            throw new InvalidOperationException("Create a native reference within its owning game.");
        _pawn = pawn;
        _pawnIdentity = pawn.GetUniqueLoadID();
    }

    /// <summary>Names the exact effect and its native owner without substituting other matching effects.</summary>
    /// <exception cref="InvalidOperationException">There is no current game to own the reference.</exception>
    public NativeReference(Hediff effect) : this(effect?.pawn
        ?? throw new ArgumentException("The effect must have a native pawn owner.", nameof(effect)))
    {
        _effect = effect;
        _targetsEffect = true;
        _effectIdentity = effect.GetUniqueLoadID();
    }

    /// <summary>Unacknowledged recorded transitions; reading does not clear them.</summary>
    public ReferenceChanges PendingChanges => _changes.Pending;

    /// <summary>Whether this reference currently receives notifications in its owning game.</summary>
    public bool IsTracking
    {
        get
        {
            Capture();
            return _session is not null;
        }
    }

    /// <summary>The notifications this subscription can promise; arbitrary off-map pawn destruction is excluded.</summary>
    public ReferenceTrackingCoverage TrackingCoverage => !IsTracking
        ? ReferenceTrackingCoverage.None
        : ReferenceTrackingCoverage.Maps | (_effect is ContributionHediff
            ? ReferenceTrackingCoverage.CooperatingHealthEffect : ReferenceTrackingCoverage.None);

    /// <summary>Reads current native state and releases an obsolete subscription without changing the game.</summary>
    public NativeReferenceSnapshot Capture()
    {
        var resolution = Resolve();
        var snapshot = new NativeReferenceSnapshot
        {
            Resolution = resolution,
            PawnIdentity = _pawnIdentity,
            EffectIdentity = _effectIdentity
        };
        if (resolution != NativeReferenceResolution.Resolved)
        {
            StopTracking();
            return snapshot;
        }

        snapshot = snapshot with
        {
            Pawn = _pawn,
            Effect = _effect,
            Map = _pawn!.Map,
            Spawned = _pawn.Spawned,
            Dead = _pawn.Dead,
            Destroyed = _pawn.Destroyed,
            Discarded = _pawn.Discarded,
            EffectAttached = _targetsEffect ? IsAttached() : null
        };
        if (_session is not null)
        {
            var unavailable = ReferenceChanges.None;
            if (snapshot.Destroyed == true) unavailable |= ReferenceChanges.Destroyed;
            if (snapshot.Discarded == true) unavailable |= ReferenceChanges.Discarded;
            if (snapshot.EffectAttached == false) unavailable |= ReferenceChanges.EffectDetached;
            if (unavailable != ReferenceChanges.None)
            {
                _changes.Record(unavailable);
                StopTracking();
            }
        }
        return snapshot;
    }

    /// <summary>Starts receiving native notifications in the current game without changing the target.</summary>
    /// <remarks>
    /// Call from LoadedGame/StartedNewGame or later, after native map initialization. Pawn-only and ordinary Hediff references receive map events;
    /// health lifecycle notifications require the exact effect to be a cooperating ContributionHediff.
    /// Native and external paths that bypass these callbacks are outside notification coverage.
    /// For example, native resurrection can remove effects before notifying survivors. Capture and
    /// TakeChanges reconcile a detached or destroyed target; they do not reconstruct missing history.
    /// Map events concern direct spawning, not every movement of a pawn inside another holder.
    /// </remarks>
    public void StartTracking()
    {
        if (Current.Game is null || Scribe.mode != LoadSaveMode.Inactive)
            throw new InvalidOperationException("Start reference tracking after the game has finished loading.");
        var component = Current.Game.GetComponent<ReferenceTrackingGameComponent>()
            ?? throw new InvalidOperationException("The current game has no reference tracking component.");
        StartTracking(component.Session);
    }

    internal void StartTracking(ReferenceTrackingSession session)
    {
        if (!session.IsCurrent || !session.Ready || IsDifferentGame())
            throw new InvalidOperationException("The reference and tracking scope must belong to the current, fully initialized game.");
        var current = Capture();
        if (current.Resolution != NativeReferenceResolution.Resolved || current.Destroyed == true
            || current.Discarded == true || current.EffectAttached == false)
            throw new InvalidOperationException("Tracking requires a resolved native owner and an attached effect, when targeted.");
        if (_session == session)
            return;
        StopTracking();
        _lastMapId = current.Map?.uniqueID;
        _session = session;
        session.Add(this);
    }

    /// <summary>Stops this subscription, retaining its reference and pending observations.</summary>
    public void StopTracking()
    {
        if (_session is null)
            return;
        _session.Remove(this);
        _session = null;
        _changes.Record(ReferenceChanges.TrackingInterrupted);
    }

    /// <summary>Acknowledges this consumer's observed change kinds without invoking any gameplay reaction.</summary>
    public ReferenceChanges TakeChanges()
    {
        Capture();
        return _changes.TakeChanges();
    }

    /// <summary>Releases the active subscription. Historical identity and observations remain readable.</summary>
    public void Dispose() => StopTracking();

    /// <summary>Persists exact native references and pending transitions without resuming observation or replaying effects.</summary>
    /// <remarks>
    /// Saving revalidates the subscription and can stop an obsolete one without changing native game state.
    /// A reference saved in another game retains its diagnostic identities but writes no resolvable target links.
    /// </remarks>
    public void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            StopTracking();
            _loading = true;
            _lastMapId = null;
            // GameComponent owners may load before the tracking component exists.
            _gameScope = CaptureGameScope();
            _changes = new ReferenceChangeTracker();
        }

        Scribe_Values.Look(ref _targetsEffect, "targetsEffect");
        Scribe_Values.Look(ref _pawnIdentity, "pawnIdentity");
        Scribe_Values.Look(ref _effectIdentity, "effectIdentity");
        var foreignGame = _foreignGame || (Scribe.mode == LoadSaveMode.Saving && IsDifferentGame());
        Scribe_Values.Look(ref foreignGame, "foreignGame");
        if (Scribe.mode == LoadSaveMode.LoadingVars) _foreignGame = foreignGame;
        // Native deep serialization can catch exceptions and continue saving. Write an explicitly
        // unavailable reference instead of relying on an exception to prevent cross-game rebinding.
        Pawn? pawn = foreignGame ? null : _pawn;
        Hediff? effect = foreignGame ? null : _effect;
        Scribe_References.Look(ref pawn, "pawn", saveDestroyedThings: true);
        Scribe_References.Look(ref effect, "effect", saveDestroyedThings: true);
        if (Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.ResolvingCrossRefs)
        {
            _pawn = foreignGame ? null : pawn;
            _effect = foreignGame ? null : effect;
        }
        var tracking = IsTracking;
        var pending = _changes.Pending;
        Scribe_Values.Look(ref pending, "pendingChanges");
        Scribe_Values.Look(ref tracking, "wasTracking");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _changes.Record(pending);
            if (tracking) _changes.Record(ReferenceChanges.TrackingInterrupted);
        }
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _loading = false;
    }

    internal void Observe(Pawn pawn, Hediff? effect, ReferenceChanges changes, int? mapId)
    {
        if (!ReferenceEquals(_pawn, pawn) || (effect is not null && !ReferenceEquals(_effect, effect)))
            return;
        // Health notifications belong to the exact cooperating effect; pawn-only subscriptions
        // must not accidentally claim coverage merely because some unrelated effect was present.
        if (effect is null && (changes & (ReferenceChanges.Died | ReferenceChanges.Resurrected
            | ReferenceChanges.Discarded)) != 0)
            return;
        if (mapId.HasValue) _lastMapId = mapId;
        _changes.Record(changes);
        if ((changes & (ReferenceChanges.EffectDetached | ReferenceChanges.Discarded)) != 0)
            StopTracking();
    }

    internal void ObserveMapRemoved(int mapId)
    {
        if (_lastMapId == mapId)
        {
            _changes.Record(ReferenceChanges.ContextRemoved);
            _lastMapId = null;
        }
    }

    private NativeReferenceResolution Resolve()
    {
        if (_foreignGame || IsDifferentGame())
            return NativeReferenceResolution.DifferentGame;
        if (_loading || Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.ResolvingCrossRefs)
            return NativeReferenceResolution.Loading;
        if (!_targetsEffect && (_effect is not null || _effectIdentity is not null))
            return NativeReferenceResolution.Mismatch;
        if (_pawn?.health is null || (_targetsEffect && _effect is null))
            return NativeReferenceResolution.Unresolved;
        if (_pawn.GetUniqueLoadID() != _pawnIdentity || (_targetsEffect
            && (_effect!.GetUniqueLoadID() != _effectIdentity || !ReferenceEquals(_effect.pawn, _pawn))))
            return NativeReferenceResolution.Mismatch;
        return NativeReferenceResolution.Resolved;
    }

    private bool IsAttached() => _pawn!.health.hediffSet.hediffs.Any(effect => ReferenceEquals(effect, _effect));

    private bool IsDifferentGame() => _gameScope is null
        ? _pawn is not null || _effect is not null
        : !_gameScope.TryGetTarget(out var game) || !ReferenceEquals(Current.Game, game);

    private static System.WeakReference<Game>? CaptureGameScope() =>
        Current.Game is { } game ? new System.WeakReference<Game>(game) : null;
}
