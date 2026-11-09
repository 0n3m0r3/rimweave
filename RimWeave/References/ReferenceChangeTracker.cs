namespace RimWeave.References;

/// <summary>Retains unacknowledged transition kinds independently of a provider's entities or storage.</summary>
/// <remarks>
/// Use on the simulation thread. Recording never invokes a consumer or decides a gameplay outcome.
/// Each consumer owns its tracker; acknowledging one tracker cannot consume another's observations.
/// </remarks>
public sealed class ReferenceChangeTracker
{
    /// <summary>Transition kinds recorded since the last acknowledgement.</summary>
    public ReferenceChanges Pending { get; private set; }

    /// <summary>Records provider-observed transitions without deducing them from a final state.</summary>
    public void Record(ReferenceChanges changes) => Pending |= changes;

    /// <summary>Returns and clears this consumer's pending transition kinds.</summary>
    public ReferenceChanges TakeChanges()
    {
        var changes = Pending;
        Pending = ReferenceChanges.None;
        return changes;
    }
}
