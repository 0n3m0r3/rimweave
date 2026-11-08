using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

/// <summary>Performs native health operations on an unambiguously resolved contribution.</summary>
public static class ContributionOperations
{
    /// <summary>Attempts to remove the unique contribution on the requested pawn and body part.</summary>
    /// <remarks>
    /// Null part means whole-body effects only. Missing, ambiguous or unreadable metadata does not
    /// trigger native removal. Exclusive-claim conflicts do not prevent cleanup of a uniquely identified effect.
    /// Call on the simulation thread. Attribution is not an authorization boundary.
    /// Native exceptions propagate and may follow partial changes; no rollback is promised.
    /// </remarks>
    /// <param name="pawn">The native owner whose health list is queried.</param>
    /// <param name="contribution">The contribution identity to resolve.</param>
    /// <param name="part">The exact body-part reference, or null for whole-body effects only.</param>
    /// <returns>The lookup snapshot and an optional guarded-removal outcome.</returns>
    /// <exception cref="ArgumentNullException">The pawn or contribution identity is null.</exception>
    public static ContributionRemovalResult Remove(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null)
    {
        var lookup = ContributionQueries.Resolve(pawn, contribution, part);
        if (lookup.Status != ContributionLookupStatus.Found)
            return new ContributionRemovalResult(lookup, null);

        return new ContributionRemovalResult(lookup,
            HediffOperations.Remove(pawn, lookup.Matches[0]));
    }
}
