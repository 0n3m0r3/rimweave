using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

public static class ContributionQueries
{
    /// <summary>Reads exact matches from native health state. Null part means whole-body only.</summary>
    /// <remarks>
    /// The returned collection is a snapshot of references, not a frozen copy of the effects.
    /// A BodyPartRecord identifies a native body slot, not the lifetime of a biological organ.
    /// Read on the simulation thread. The native health list remains authoritative.
    /// </remarks>
    public static IReadOnlyList<ContributionHediff> Find(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null) =>
        Resolve(pawn, contribution, part).Matches;

    public static ContributionLookupResult<ContributionHediff> Resolve(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null) =>
        ContributionResolver.Resolve(OnPart(pawn, part), contribution);

    /// <summary>Reports an explicit exclusive-ownership conflict without selecting a winner.</summary>
    /// <remarks>Native callback observers are not claimants. Unclaimed effects do not compete here.</remarks>
    public static ContributionLookupResult<ContributionHediff> ResolveExclusiveClaim(
        Pawn pawn, string responsibility, BodyPartRecord? part = null) =>
        ContributionResolver.ResolveExclusiveClaim(OnPart(pawn, part), responsibility);

    private static IEnumerable<ContributionHediff> OnPart(Pawn pawn, BodyPartRecord? part)
    {
        ArgumentNullException.ThrowIfNull(pawn);
        return pawn.health.hediffSet.hediffs
            .OfType<ContributionHediff>()
            .Where(effect => ReferenceEquals(effect.Part, part));
    }
}
