using RimWeave.Contributions;
using RimWeave.Health;
using Verse;

namespace RimWeave.Tests.Helpers;

internal static class ContributionTestData
{
    // Sets up native storage for read/guard tests without invoking a health operation.
    internal static ContributionHediff AttachForLookup(
        Pawn pawn, ContributionId contribution, BodyPartRecord? part = null)
    {
        var effect = new ContributionHediff { pawn = pawn, Part = part };
        effect.Metadata.AssignIdentity(contribution);
        pawn.health.hediffSet.hediffs.Add(effect);
        return effect;
    }
}
