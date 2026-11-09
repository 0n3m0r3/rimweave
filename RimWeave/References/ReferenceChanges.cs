namespace RimWeave.References;

/// <summary>Kinds of transitions observed since a consumer last acknowledged them.</summary>
/// <remarks>Repeated kinds are coalesced. These flags preserve neither event counts nor ordering.</remarks>
[Flags]
public enum ReferenceChanges
{
    /// <summary>No transition has been recorded.</summary>
    None = 0,
    /// <summary>The target entered a local context.</summary>
    EnteredContext = 1,
    /// <summary>The target left a local context.</summary>
    LeftContext = 2,
    /// <summary>The target's last observed local context was removed.</summary>
    ContextRemoved = 4,
    /// <summary>The provider reported the target's death.</summary>
    Died = 8,
    /// <summary>The provider reported resurrection.</summary>
    Resurrected = 16,
    /// <summary>The provider discarded the target.</summary>
    Discarded = 32,
    /// <summary>The exact effect was detached from its native owner.</summary>
    EffectDetached = 64,
    /// <summary>Observation stopped; no continuity is promised until tracking starts again.</summary>
    TrackingInterrupted = 128,
    /// <summary>Revalidation observed native destruction; not necessarily an immediate notification.</summary>
    Destroyed = 256
}

/// <summary>Native notifications available to an active reference in the initial integration.</summary>
[Flags]
public enum ReferenceTrackingCoverage
{
    /// <summary>No active subscription.</summary>
    None = 0,
    /// <summary>Native map entry, departure and removal notifications.</summary>
    Maps = 1,
    /// <summary>Callbacks forwarded by the exact cooperating effect; native paths omitting those callbacks still require revalidation.</summary>
    CooperatingHealthEffect = 2
}
