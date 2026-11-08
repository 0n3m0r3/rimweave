using RimWeave.Contributions;
using RimWeave.Health;
using Verse;

namespace RimWeave.Tests.Helpers;

internal static class ContributionTestData
{
    // Sets up native storage for read/guard tests without invoking a health operation.
    internal static ContributionHediff AttachForLookup(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null) =>
        AttachForLookup<ContributionHediff>(pawn, contribution, part);

    internal static T AttachForLookup<T>(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null)
        where T : ContributionHediff, new()
    {
        var effect = new T { pawn = pawn, Part = part };
        effect.Metadata.AssignIdentity(contribution);
        pawn.health.hediffSet.hediffs.Add(effect);
        return effect;
    }
}
