using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

public static class ContributionOperations
{
    /// <summary>Attempts to remove the unique contribution on the requested pawn and body part.</summary>
    /// <remarks>
    /// Null part means whole-body effects only. Missing or ambiguous identities do not trigger
    /// native removal. Exclusive-claim conflicts do not prevent cleanup of a uniquely identified effect.
    /// Call on the simulation thread. Attribution is not an authorization boundary.
    /// Native exceptions propagate and may follow partial changes; no rollback is promised.
    /// </remarks>
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
