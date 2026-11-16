using System.Runtime.CompilerServices;
using Verse;

namespace RimWeave.References;

/// <summary>Owns explicit subscriptions for one game, without owning or mirroring native world state.</summary>
public sealed class ReferenceTrackingGameComponent : GameComponent
{
    internal ReferenceTrackingSession Session { get; }

    /// <summary>Creates an empty subscription scope; no targets are tracked automatically.</summary>
    public ReferenceTrackingGameComponent(Game game) => Session = new ReferenceTrackingSession(game);

    /// <summary>Makes tracking available before consumers receive LoadedGame or StartedNewGame.</summary>
    public override void FinalizeInit() => Session.Ready = true;

    internal static void NotifyHealth(Hediff effect, ReferenceChanges changes)
    {
        if (Scribe.mode == LoadSaveMode.Inactive)
            Current.Game?.GetComponent<ReferenceTrackingGameComponent>()
                ?.Session.Notify(effect.pawn, effect, changes);
    }
}

internal sealed class ReferenceTrackingSession
{
    // Consumers own their references. A forgotten subscription must not keep an abandoned target alive.
    private ConditionalWeakTable<NativeReference, object>? _references;
    private readonly System.WeakReference<Game>? _game;
    private static readonly object Registration = new();

    internal ReferenceTrackingSession(Game? game = null)
    {
        _game = game is null ? null : new System.WeakReference<Game>(game);
        Ready = game is null; // A standalone scope is used by adapter tests only.
    }

    internal bool Ready { get; set; }
    internal bool IsCurrent => _game is null || (_game.TryGetTarget(out var game) && ReferenceEquals(Current.Game, game));

    internal void Add(NativeReference reference) =>
        (_references ??= new ConditionalWeakTable<NativeReference, object>()).Add(reference, Registration);
    internal void Remove(NativeReference reference) => _references?.Remove(reference);

    internal void Notify(Pawn pawn, Hediff? effect, ReferenceChanges changes, int? mapId = null)
    {
        using var references = BeginDelivery();
        if (references is null) return;
        // Only the current subscription can remove itself; no consumer callback runs here.
        do references.Current.Key.Observe(pawn, effect, changes, mapId);
        while (references.MoveNext());
    }

    internal void MapRemoved(int mapId)
    {
        using var references = BeginDelivery();
        if (references is null) return;
        do references.Current.Key.ObserveMapRemoved(mapId);
        while (references.MoveNext());
    }

    // Returns an enumerator positioned on its first live entry, or null when delivery is unnecessary.
    // Weak-table enumeration permits removal and retains only the current entry, not every observer.
    private IEnumerator<KeyValuePair<NativeReference, object>>? BeginDelivery()
    {
        if (!IsCurrent || !Ready || _references is null) return null;
        var references = ((IEnumerable<KeyValuePair<NativeReference, object>>)_references).GetEnumerator();
        if (references.MoveNext()) return references;
        references.Dispose();
        // Release historical enumeration cost when an existing notification finds no live entries.
        _references = null;
        return null;
    }
}
