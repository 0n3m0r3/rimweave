using System.Reflection;
using RimWeave.Tests.Helpers;
using RimWorld;
using Verse;
using Xunit;

namespace RimWeave.Tests.Equipment;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeApparelFilterTests
{
    [Theory]
    [InlineData(Gender.Male, Gender.Male, DevelopmentalStage.Adult, true)]
    [InlineData(Gender.Male, Gender.Female, DevelopmentalStage.Adult, false)]
    [InlineData(Gender.None, Gender.Female, DevelopmentalStage.Adult, true)]
    [InlineData(Gender.Male, Gender.None, DevelopmentalStage.Adult, true)]
    [InlineData(Gender.None, Gender.None, DevelopmentalStage.Child, false)]
    public void DefinitionFilterPreservesNativeGenderAndStageSemantics(
        Gender required, Gender candidate, DevelopmentalStage stage, bool expected)
    {
        var properties = new ApparelProperties { gender = required, developmentalStageFilter = DevelopmentalStage.Adult };
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);
        for (int i = 0; i < 3; i++) Assert.Equal(expected, properties.PawnCanWear(candidate, stage));
        Assert.Equal(required, properties.gender);
        Assert.Equal(DevelopmentalStage.Adult, properties.developmentalStageFilter);
        Assert.Equal(before, random.GetValue(null));
    }

    [Fact]
    public void MultipleDevelopmentalStagesRemainAnExplicitDefinitionFilter()
    {
        var properties = new ApparelProperties
        {
            developmentalStageFilter = DevelopmentalStage.Child | DevelopmentalStage.Adult
        };
        Assert.True(properties.PawnCanWear(Gender.Female, DevelopmentalStage.Child));
        Assert.True(properties.PawnCanWear(Gender.Female, DevelopmentalStage.Adult));
        Assert.False(properties.PawnCanWear(Gender.Female, DevelopmentalStage.Baby));
    }

    [Fact]
    public void NoCurrentApparelConflictDoesNotImplyTheDefinitionFilterPasses()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var tracker = new Pawn_ApparelTracker(pawn);
        var definition = new ThingDef
        {
            apparel = new ApparelProperties { gender = Gender.Male, developmentalStageFilter = DevelopmentalStage.Adult }
        };
        Assert.True(tracker.CanWearWithoutDroppingAnything(definition));
        Assert.False(definition.apparel.PawnCanWear(Gender.Female, DevelopmentalStage.Adult));
        Assert.Empty(tracker.WornApparel);
    }
}
