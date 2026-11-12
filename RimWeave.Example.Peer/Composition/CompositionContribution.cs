using RimWeave.Contributions;
using RimWeave.Health;
using Verse;

namespace RimWeave.Example.Peer.Composition;

// This consumer owns its identity and admission policy; the framework owns resolution and native guards.
public static class CompositionContribution
{
    public const string DefinitionName = "RimWeave_CompositionTemporary";
    public static string Responsibility => Declaration.ExclusiveResponsibility
        ?? throw new InvalidOperationException("The peer example must declare an exclusive responsibility.");
    public static ContributionId Identity => Declaration.Identity!;

    private static ContributionMetadata Declaration => ContributionDefExtension.Read(
        DefDatabase<HediffDef>.GetNamed(DefinitionName))
        ?? throw new InvalidOperationException("The peer example must declare contribution metadata in XML.");

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
