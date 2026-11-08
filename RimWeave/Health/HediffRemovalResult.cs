using Verse;

namespace RimWeave.Health;

public enum HediffRemovalStatus
{
    /// <summary>The instance is absent from the requested pawn after the native call.</summary>
    Removed,

    /// <summary>The instance was absent; no native removal was called.</summary>
    NotPresent,

    /// <summary>The instance refers to another pawn; no native removal was called.</summary>
    PawnMismatch,

    /// <summary>The instance is still attached after the native call; side effects may have occurred.</summary>
    StillPresent
}

/// <summary>An observed removal outcome for the requested pawn and instance.</summary>
/// <param name="Part">The part reference captured before callbacks, not a persistent organ identity.</param>
public sealed record HediffRemovalResult(
    HediffRemovalStatus Status,
    Pawn Pawn,
    Hediff Hediff,
    BodyPartRecord? Part);