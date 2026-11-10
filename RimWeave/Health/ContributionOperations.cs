using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

/// <summary>Performs guarded native health operations on contributions.</summary>
public static class ContributionOperations
{
    [ThreadStatic]
    private static HashSet<ContributionHediff>? _additionsInProgress;

    /// <summary>Adds a prepared contribution to its explicit target and observes its final attachment.</summary>
    /// <remarks>
    /// Prepare the instance with HediffMaker.MakeHediff using the same pawn and part.
    /// Null explicitly means whole body, and is refused when the definition would select a default part.
    /// The part must belong to the pawn's BodyDef and be present according to the native health tracker.
    /// Body-part records are shared by pawns using the same BodyDef; they are not individual organ identities.
    /// Call on the simulation thread. Requests for an already attached instance, and reentrant requests
    /// for an addition in progress, do not invoke native addition again. Later attempts after detachment
    /// remain possible. Other instances remain independent; attribution conflicts are not admission rules.
    /// The result is a snapshot after the complete native call, not a promise of continued attachment.
    /// NotAttached does not identify a cause: existing ordinary effects can absorb an incoming contribution,
    /// and callbacks can remove it. Native exceptions and legitimate random draws are preserved.
    /// Exceptions may follow partial changes; the temporary guard is released, but no rollback is performed.
    /// </remarks>
    /// <param name="pawn">The prepared instance's owner, with an initialized native health tracker.</param>
    /// <param name="hediff">The exact contribution instance prepared by the extension.</param>
    /// <param name="part">The prepared instance's exact part, or explicitly null for the whole body.</param>
    /// <returns>The request outcome and the owner and part fields observed when returning.</returns>
    /// <exception cref="ArgumentNullException">The pawn or contribution is null.</exception>
    public static ContributionAdditionResult Add(Pawn pawn, ContributionHediff hediff, BodyPartRecord? part)
    {
        ArgumentNullException.ThrowIfNull(pawn);
        ArgumentNullException.ThrowIfNull(hediff);

        ContributionAdditionResult Result(ContributionAdditionStatus status) =>
            new(status, pawn, hediff, part, hediff.pawn, hediff.Part);

        if (_additionsInProgress?.Contains(hediff) == true)
            return Result(ContributionAdditionStatus.AlreadyAdding);
        if (!ReferenceEquals(hediff.pawn, pawn))
            return Result(ContributionAdditionStatus.PawnMismatch);
        if (!ReferenceEquals(hediff.Part, part))
            return Result(ContributionAdditionStatus.PartMismatch);
        if (HediffOperations.IsAttached(pawn, hediff))
            return Result(ContributionAdditionStatus.AlreadyPresent);
        if (hediff.def is null)
            return Result(ContributionAdditionStatus.InvalidDefinition);

        if (part is null)
        {
            // Native null targeting can select a random default part, even on a prepared instance.
            if (hediff.def.defaultInstallPart is not null)
                return Result(ContributionAdditionStatus.IncompatibleTarget);
        }
        else
        {
            if (!pawn.RaceProps.body.AllParts.Any(candidate => ReferenceEquals(candidate, part)))
                return Result(ContributionAdditionStatus.InvalidPart);
            if (!pawn.health.hediffSet.GetNotMissingParts().Any(candidate => ReferenceEquals(candidate, part)))
                return Result(ContributionAdditionStatus.MissingPart);
        }

        var inProgress = _additionsInProgress ??=
            new HashSet<ContributionHediff>(ReferenceEqualityComparer.Instance);
        inProgress.Add(hediff);
        try
        {
            pawn.health.AddHediff(hediff, part);

            if (!HediffOperations.IsAttached(pawn, hediff))
                return Result(ContributionAdditionStatus.NotAttached);
            if (!ReferenceEquals(hediff.pawn, pawn) || !ReferenceEquals(hediff.Part, part))
                return Result(ContributionAdditionStatus.AttachmentChanged);
            return Result(ContributionAdditionStatus.Added);
        }
        finally
        {
            inProgress.Remove(hediff);
        }
    }

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
