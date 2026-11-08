using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

/// <summary>A native health effect whose contribution must retain its own lifetime.</summary>
/// <remarks>
/// Opt in through an extension's HediffDef. This does not alter ordinary Hediffs or
/// prevent unrelated patches from changing native behavior. Call on the simulation thread.
/// </remarks>
public class ContributionHediff : HediffWithComps, IContribution
{
    public ContributionMetadata Metadata { get; } = new();

    // Native AddDirect asks existing effects whether they accept a merge.
    public sealed override bool TryMergeWith(Hediff other) => false;
}
