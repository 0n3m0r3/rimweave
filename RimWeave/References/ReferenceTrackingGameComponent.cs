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
    private readonly ConditionalWeakTable<NativeReference, object> _references = new();
    private readonly System.WeakReference<Game>? _game;
    private static readonly object Registration = new();

    internal ReferenceTrackingSession(Game? game = null)
    {
        _game = game is null ? null : new System.WeakReference<Game>(game);
        Ready = game is null; // A standalone scope is used by adapter tests only.
    }

    internal bool Ready { get; set; }
    internal bool IsCurrent => _game is null || (_game.TryGetTarget(out var game) && ReferenceEquals(Current.Game, game));

    internal void Add(NativeReference reference) => _references.Add(reference, Registration);
    internal void Remove(NativeReference reference) => _references.Remove(reference);

    internal void Notify(Pawn pawn, Hediff? effect, ReferenceChanges changes, int? mapId = null)
    {
        if (!IsCurrent || !Ready) return;
        // A detached effect releases its subscription during delivery. No consumer callback runs here.
        foreach (var reference in _references.Select(pair => pair.Key).ToArray())
            reference.Observe(pawn, effect, changes, mapId);
    }

    internal void MapRemoved(int mapId)
    {
        if (!IsCurrent || !Ready) return;
        foreach (var pair in _references)
            pair.Key.ObserveMapRemoved(mapId);
    }
}
