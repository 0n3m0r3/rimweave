using Verse;

namespace RimWeave.References;

/// <summary>Whether the exact native objects named by a reference are available.</summary>
public enum NativeReferenceResolution
{
    /// <summary>A target is unavailable; this does not establish permanent disappearance.</summary>
    Unresolved,
    /// <summary>Native loading has not completed reference resolution.</summary>
    Loading,
    /// <summary>The exact native objects are available, independently of their current usability.</summary>
    Resolved,
    /// <summary>The resolved objects do not match the saved identity or owner association.</summary>
    Mismatch,
    /// <summary>The handle belongs to a different game instance and cannot be reused in this game.</summary>
    DifferentGame
}

/// <summary>A passive observation of native identity, location and attachment at one instant.</summary>
/// <remarks>
/// A resolved dead, destroyed or detached object is not necessarily usable by an operation.
/// Map presence does not establish reachability, reservation, willingness or business eligibility.
/// The referenced native objects remain mutable; inspect again before a later operation.
/// </remarks>
public sealed record NativeReferenceSnapshot
{
    /// <summary>The resolution outcome; unresolved does not mean destroyed.</summary>
    public NativeReferenceResolution Resolution { get; init; }
    /// <summary>The native pawn identity retained even if resolution fails.</summary>
    public string? PawnIdentity { get; init; }
    /// <summary>The exact native effect identity, or null for a pawn reference.</summary>
    public string? EffectIdentity { get; init; }
    /// <summary>The resolved pawn, or null when resolution is incomplete or inconsistent.</summary>
    public Pawn? Pawn { get; init; }
    /// <summary>The resolved exact effect; no substitution by definition or contribution identity occurs.</summary>
    public Hediff? Effect { get; init; }
    /// <summary>The pawn's directly occupied map when observed, if any.</summary>
    public Map? Map { get; init; }
    /// <summary>Whether the resolved pawn was spawned directly on a map.</summary>
    public bool? Spawned { get; init; }
    /// <summary>The observed native death state, independent of resolution.</summary>
    public bool? Dead { get; init; }
    /// <summary>The observed native destruction state, independent of death and map presence.</summary>
    public bool? Destroyed { get; init; }
    /// <summary>Whether the native pawn has been discarded.</summary>
    public bool? Discarded { get; init; }
    /// <summary>Exact membership in the native health list; null for a pawn-only or unresolved reference.</summary>
    public bool? EffectAttached { get; init; }
}
