using RimWeave.Contributions;
using RimWeave.Health;
using Verse;

namespace RimWeave.Example.Peer.Composition;

// This consumer owns its identity and admission policy; the framework owns resolution and native guards.
public static class CompositionContribution
{
    public const string DefinitionName = "RimWeave_CompositionTemporary";
    public const string Responsibility = "rimweave.examples/hand-state";
    public static ContributionId Identity { get; } = new("rimweave.example.peer", "hand-state");

    public static ContributionAdditionResult Add(Pawn pawn, BodyPartRecord part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var existing = ContributionQueries.Resolve(pawn, Identity, part);
        if (existing.Status == ContributionLookupStatus.Found)
            return ContributionOperations.Add(pawn, existing.Matches[0], part);
        if (existing.Status != ContributionLookupStatus.NotPresent)
            throw new InvalidOperationException($"Cannot add composition B: identity lookup is {existing.Status}.");

        var definition = DefDatabase<HediffDef>.GetNamed(DefinitionName);
        if (HediffMaker.MakeHediff(definition, pawn, part) is not ContributionHediff effect)
            throw new InvalidOperationException("The composition definition must use ContributionHediff.");
        effect.Metadata.AssignIdentity(Identity);
        effect.Metadata.ClaimExclusiveResponsibility(Responsibility);
        effect.Metadata.DescribeOrigin(new ContributionOrigin(
            new ContributionOriginType("rimweave.example.peer", "temporary-test")));
        return ContributionOperations.Add(pawn, effect, part);
    }

    public static ContributionRemovalResult Remove(Pawn pawn, BodyPartRecord part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return ContributionOperations.Remove(pawn, Identity, part);
    }
}
