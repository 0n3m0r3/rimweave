using Verse;

namespace RimWeave.Health;

/// <summary>The observed outcome of an explicitly targeted contribution addition.</summary>
public enum ContributionAdditionStatus
{
    /// <summary>The native call returned with the exact instance attached to the requested pawn and part.</summary>
    Added,
    /// <summary>The instance is absent from the requested pawn after the native call; its cause is not inferred.</summary>
    NotAttached,
    /// <summary>The instance is listed on the requested pawn, but its owner or part field changed during the native call.</summary>
    AttachmentChanged,
    /// <summary>The instance was already attached to the requested pawn and part; no native addition was called.</summary>
    AlreadyPresent,
    /// <summary>An addition of this exact instance is in progress; no native addition was called again.</summary>
    AlreadyAdding,
    /// <summary>The prepared instance refers to a different or null pawn; no native addition was called.</summary>
    PawnMismatch,
    /// <summary>The prepared instance's part differs from the explicit target; no native addition was called.</summary>
    PartMismatch,
    /// <summary>The part is not an occurrence in the requested pawn's BodyDef; no native addition was called.</summary>
    InvalidPart,
    /// <summary>The native health tracker excludes the requested part as missing; no native addition was called.</summary>
    MissingPart,
    /// <summary>A whole-body request would select the definition's default part; no native addition was called.</summary>
    IncompatibleTarget,
    /// <summary>The prepared instance has no definition; no native addition was called.</summary>
    InvalidDefinition
}

/// <summary>An addition outcome captured at return, without inferring the cause of an absent instance.</summary>
/// <param name="Status">The request outcome; only Added confirms a new native call with matching final attachment.</param>
/// <param name="Pawn">The requested native owner.</param>
/// <param name="Hediff">The exact contribution requested for addition.</param>
/// <param name="Part">The requested part reference, or null for the whole body.</param>
/// <param name="ObservedPawn">The instance's owner field at return; this alone does not prove list membership.</param>
/// <param name="ObservedPart">The instance's part field at return, which may differ after callbacks.</param>
public sealed record ContributionAdditionResult(
    ContributionAdditionStatus Status,
    Pawn Pawn,
    ContributionHediff Hediff,
    BodyPartRecord? Part,
    Pawn? ObservedPawn,
    BodyPartRecord? ObservedPart);
