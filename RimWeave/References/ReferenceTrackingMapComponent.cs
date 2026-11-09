using Verse;

namespace RimWeave.References;

/// <summary>Relays native map lifecycle facts to explicit subscriptions, without invoking extension behavior.</summary>
public sealed class ReferenceTrackingMapComponent : MapComponent
{
    private readonly ReferenceTrackingSession? _session;
    private bool _subscribed;

    /// <summary>Associates the component with its game; event subscriptions begin after native map initialization.</summary>
    public ReferenceTrackingMapComponent(Map map) : this(map,
        Current.Game?.GetComponent<ReferenceTrackingGameComponent>()?.Session) { }

    internal ReferenceTrackingMapComponent(Map map, ReferenceTrackingSession? session) : base(map)
    {
        _session = session;
    }

    /// <summary>Subscribes only the final component instance retained by native map loading.</summary>
    public override void FinalizeInit()
    {
        base.FinalizeInit();
        // Map loading constructs components before replacing them with deserialized instances.
        // Subscribing in the constructor would retain the discarded instances in native events.
        if (_subscribed) return;
        map.events.ThingSpawned += OnSpawned;
        map.events.ThingDespawned += OnDespawned;
        _subscribed = true;
    }

    private void OnSpawned(Thing thing) => Notify(thing, ReferenceChanges.EnteredContext);
    private void OnDespawned(Thing thing) => Notify(thing, ReferenceChanges.LeftContext);

    private void Notify(Thing thing, ReferenceChanges changes)
    {
        if (Scribe.mode == LoadSaveMode.Inactive && thing is Pawn pawn)
            _session?.Notify(pawn, null, changes, map.uniqueID);
    }

    /// <summary>Releases this map's handlers and records context loss without declaring its former pawns destroyed.</summary>
    public override void MapRemoved()
    {
        if (_subscribed)
        {
            map.events.ThingSpawned -= OnSpawned;
            map.events.ThingDespawned -= OnDespawned;
            _subscribed = false;
            _session?.MapRemoved(map.uniqueID);
        }
        base.MapRemoved();
    }
}
