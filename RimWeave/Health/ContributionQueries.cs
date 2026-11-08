using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

/// <summary>Queries cooperating ContributionHediff instances in authoritative native health storage.</summary>
public static class ContributionQueries
{
    /// <summary>Reads exact matches from native health state. Null part means whole-body only.</summary>
    /// <remarks>
    /// The returned collection is a snapshot of references, not a frozen copy of the effects.
    /// A BodyPartRecord identifies a native body slot, not the lifetime of a biological organ.
    /// Read on the simulation thread. The native health list remains authoritative.
    /// </remarks>
    /// <param name="pawn">The native owner with an initialized health tracker.</param>
    /// <param name="contribution">The identity to match.</param>
    /// <param name="part">The exact body-part reference, or null for whole-body effects only.</param>
    /// <returns>A read-only membership snapshot of all matching contribution effects.</returns>
    /// <exception cref="ArgumentNullException">The pawn or identity is null.</exception>
    /// <exception cref="InvalidOperationException">Saved metadata in the scope is unreadable; use Resolve to inspect the incomplete result.</exception>
    public static IReadOnlyList<ContributionHediff> Find(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null)
    {
        var result = Resolve(pawn, contribution, part);
        if (result.Status == ContributionLookupStatus.InvalidMetadata)
            throw new InvalidOperationException("The scope contains unreadable contribution metadata. Use Resolve for diagnostics.");
        return result.Matches;
    }

    /// <summary>Resolves an identity on one pawn and one exact native body-part scope.</summary>
    /// <param name="pawn">The native owner with an initialized health tracker.</param>
    /// <param name="contribution">The identity to match.</param>
    /// <param name="part">The exact body-part reference, or null for whole-body effects only.</param>
    /// <returns>Unreadable metadata, no match, one match or ambiguity; no native mutation is performed.</returns>
    /// <exception cref="ArgumentNullException">The pawn or identity is null.</exception>
    /// <remarks>Call on the simulation thread. Only ContributionHediff instances are selected.</remarks>
    public static ContributionLookupResult<ContributionHediff> Resolve(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null) =>
        ContributionResolver.Resolve(OnPart(pawn, part), contribution);

    /// <summary>Reports an explicit exclusive-ownership conflict without selecting a winner.</summary>
    /// <remarks>Native callback observers are not claimants. Unclaimed effects do not compete here.</remarks>
    /// <param name="pawn">The native owner with an initialized health tracker.</param>
    /// <param name="responsibility">The exact exclusive responsibility key to match.</param>
    /// <param name="part">The exact body-part reference, or null for whole-body effects only.</param>
    /// <returns>The explicit claimants and unreadable candidates; InvalidMetadata takes precedence over claim counts.</returns>
    /// <exception cref="ArgumentNullException">The pawn is null.</exception>
    /// <exception cref="ArgumentException">The responsibility key is null, blank or has surrounding whitespace.</exception>
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
