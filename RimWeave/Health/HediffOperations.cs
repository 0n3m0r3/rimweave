using Verse;

namespace RimWeave.Health;

/// <summary>Guards native health-effect removal against stale instances and mismatched owners.</summary>
public static class HediffOperations
{
    /// <summary>
    /// Removes an attached instance through its pawn's native health tracker.
    /// </summary>
    /// <remarks>
    /// Call on the simulation thread. The result describes attachment to the
    /// requested pawn after a normal return; it does not describe all side effects.
    /// Native exceptions propagate and may follow partial changes.
    /// </remarks>
    /// <param name="pawn">The requested owner with an initialized native health tracker.</param>
    /// <param name="hediff">The exact native effect instance, including an ordinary non-RimWeave Hediff.</param>
    /// <returns>An observed attachment outcome; inspect Status rather than assuming success.</returns>
    /// <exception cref="ArgumentNullException">The pawn or effect is null.</exception>
    public static HediffRemovalResult Remove(Pawn pawn, Hediff hediff)
    {
        ArgumentNullException.ThrowIfNull(pawn);
        ArgumentNullException.ThrowIfNull(hediff);

        BodyPartRecord? part = hediff.Part;

        if (!ReferenceEquals(hediff.pawn, pawn))
        {
            return new HediffRemovalResult(
                HediffRemovalStatus.PawnMismatch, pawn, hediff, part);
        }

        if (!IsAttached(pawn, hediff))
        {
            return new HediffRemovalResult(
                HediffRemovalStatus.NotPresent, pawn, hediff, part);
        }

        pawn.health.RemoveHediff(hediff);

        HediffRemovalStatus status = IsAttached(pawn, hediff)
            ? HediffRemovalStatus.StillPresent
            : HediffRemovalStatus.Removed;

        return new HediffRemovalResult(status, pawn, hediff, part);
    }

    private static bool IsAttached(Pawn pawn, Hediff hediff) =>
        pawn.health.hediffSet.hediffs.Any(current => ReferenceEquals(current, hediff));
}
