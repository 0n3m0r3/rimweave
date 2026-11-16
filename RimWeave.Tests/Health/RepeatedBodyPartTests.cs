using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class RepeatedBodyPartTests
{
    private static readonly ContributionId Identity = new("example.provider", "limb-state");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Resolve_RepeatedDefinitionsAndLabels_KeepPawnAndOccurrenceSeparate(int selected)
    {
        var (pawn, legs, feet) = CreateBody();
        var otherPawn = HealthTestPawns.CreateForGuardChecks();
        otherPawn.RaceProps.body = pawn.RaceProps.body;
        var effects = legs.Select(part => ContributionTestData.AttachForLookup(pawn, Identity, part)).ToArray();
        var foot = ContributionTestData.AttachForLookup(pawn, Identity, feet[selected]);
        var wholeBody = ContributionTestData.AttachForLookup(pawn, Identity);
        var otherAnimal = ContributionTestData.AttachForLookup(otherPawn, Identity, legs[selected]);
        pawn.health.hediffSet.hediffs.Reverse();
        var before = pawn.health.hediffSet.hediffs.ToArray();

        Assert.Same(effects[selected], Assert.Single(ContributionQueries.Resolve(pawn, Identity, legs[selected]).Matches));
        Assert.Same(foot, Assert.Single(ContributionQueries.Resolve(pawn, Identity, feet[selected]).Matches));
        Assert.Same(wholeBody, Assert.Single(ContributionQueries.Resolve(pawn, Identity).Matches));
        Assert.Same(otherAnimal, Assert.Single(ContributionQueries.Resolve(otherPawn, Identity, legs[selected]).Matches));
        var foreign = new BodyPartRecord { def = legs[selected].def, customLabel = legs[selected].customLabel };
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionQueries.Resolve(pawn, Identity, foreign).Status);
        Assert.Equal(before, pawn.health.hediffSet.hediffs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingBranch_RejectsItsFootWithoutFallingBackToAnotherBranch(int selected)
    {
        var (pawn, legs, feet) = CreateBody();
        var missing = new Hediff_MissingPart { pawn = pawn, Part = legs[selected] };
        pawn.health.hediffSet.hediffs.Add(missing);
        // Native loss records descendants as missing too; a lone ancestor entry is incomplete.
        pawn.health.hediffSet.hediffs.Add(new Hediff_MissingPart { pawn = pawn, Part = feet[selected] });
        var witnesses = feet.Where((_, index) => index != selected)
            .Select(part => ContributionTestData.AttachForLookup(pawn, Identity, part)).ToArray();
        var candidate = new ContributionHediff { pawn = pawn, Part = feet[selected], def = new HediffDef() };
        candidate.Metadata.AssignIdentity(Identity);
        var before = pawn.health.hediffSet.hediffs.ToArray();

        var result = ContributionOperations.Add(pawn, candidate, feet[selected]);

        Assert.Equal(ContributionAdditionStatus.MissingPart, result.Status);
        Assert.Same(feet[selected], candidate.Part);
        Assert.Equal(before, pawn.health.hediffSet.hediffs);
        Assert.Equal(ContributionLookupStatus.NotPresent,
            ContributionQueries.Resolve(pawn, Identity, feet[selected]).Status);
        foreach (var witness in witnesses)
            Assert.Same(witness, Assert.Single(ContributionQueries.Resolve(pawn, Identity, witness.Part).Matches));
    }

    private static (Pawn Pawn, BodyPartRecord[] Legs, BodyPartRecord[] Feet) CreateBody()
    {
        // Synthetic topology for read/guard checks; native animal operations are verified in-game.
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var body = new BodyDef();
        pawn.RaceProps.body = body;
        var root = new BodyPartRecord { def = new BodyPartDef() };
        body.corePart = root;
        body.AllParts.Add(root);
        var legDefinition = new BodyPartDef();
        var footDefinition = new BodyPartDef();
        var legs = new BodyPartRecord[4];
        var feet = new BodyPartRecord[4];
        for (var i = 0; i < legs.Length; i++)
        {
            legs[i] = new BodyPartRecord { def = legDefinition, customLabel = "leg", parent = root };
            feet[i] = new BodyPartRecord { def = footDefinition, customLabel = "foot", parent = legs[i] };
            root.parts.Add(legs[i]);
            legs[i].parts.Add(feet[i]);
            body.AllParts.AddRange([legs[i], feet[i]]);
        }
        return (pawn, legs, feet);
    }
}
