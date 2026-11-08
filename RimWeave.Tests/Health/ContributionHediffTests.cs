using RimWeave.Contributions;
using RimWeave.Health;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class ContributionHediffTests
{
    [Fact]
    public void Metadata_IsExposedThroughTheSharedContract()
    {
        var effect = new ContributionHediff();
        IContribution contribution = effect;
        Assert.Same(effect.Metadata, contribution.Metadata);
        Assert.Null(contribution.Metadata.Identity);
    }

    [Fact]
    public void Metadata_BelongsToEachNativeEffectIndependently()
    {
        var first = new ContributionHediff();
        var second = new ContributionHediff();
        first.Metadata.AssignIdentity(new ContributionId("example.mod", "marker"));
        first.Metadata.ClaimExclusiveResponsibility("example/property");

        Assert.NotSame(first.Metadata, second.Metadata);
        Assert.Null(second.Metadata.Identity);
        Assert.Null(second.Metadata.ExclusiveResponsibility);
    }

    [Fact]
    public void Merge_IsRefusedEvenForTheSameDefinitionAndPart()
    {
        var definition = new HediffDef();
        var part = new BodyPartRecord();
        var first = new ContributionHediff { def = definition, Part = part, ageTicks = 12 };
        var second = new ContributionHediff { def = definition, Part = part, ageTicks = 25 };

        Assert.False(first.TryMergeWith(second));
        Assert.Equal(12, first.ageTicks);
        Assert.Equal(25, second.ageTicks);
    }
}
