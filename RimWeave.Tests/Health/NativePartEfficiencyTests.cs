using System.Reflection;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class NativePartEfficiencyTests
{
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1.25f)]
    public void HealthyReplacementCanHaveDifferentEfficiency(float efficiency)
    {
        var pawn = NativeHealthReadPawns.Create();
        var part = Part();
        pawn.health.hediffSet.hediffs.Add(Replacement(pawn, part, efficiency));
        Assert.Equal(20f, pawn.health.hediffSet.GetPartHealth(part));
        Assert.Equal(efficiency, PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, part));
    }

    [Fact]
    public void ReplacementAncestorSuppliesItsNativeEfficiency()
    {
        var pawn = NativeHealthReadPawns.Create();
        var parent = Part();
        var child = Part();
        child.parent = parent;
        pawn.health.hediffSet.hediffs.Add(Replacement(pawn, parent, 1.4f));
        Assert.Equal(1.4f, PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, child));
    }

    [Fact]
    public void MissingParentMakesChildInefficientDespiteItsOwnHealth()
    {
        var pawn = NativeHealthReadPawns.Create();
        var parent = Part();
        var child = Part();
        child.parent = parent;
        pawn.health.hediffSet.hediffs.Add(new Hediff_MissingPart { pawn = pawn, Part = parent });
        Assert.Equal(20f, pawn.health.hediffSet.GetPartHealth(child));
        Assert.Equal(0f, PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, child));
    }

    [Fact]
    public void NativeStageOffsetIsSeparateFromSeverityAndReadsArePassive()
    {
        var pawn = NativeHealthReadPawns.Create();
        var part = Part();
        var effect = new Hediff
        {
            pawn = pawn, Part = part,
            def = new HediffDef { stages = [new HediffStage { partEfficiencyOffset = 0.25f }] },
            Severity = 0.75f
        };
        pawn.health.hediffSet.hediffs.Add(effect);
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);
        for (int i = 0; i < 3; i++)
            Assert.Equal(1.25f, PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, part));
        Assert.Equal(20f, pawn.health.hediffSet.GetPartHealth(part));
        Assert.Equal(0.75f, effect.Severity);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        Assert.Equal(before, random.GetValue(null));
    }

    private static BodyPartRecord Part() => new() { def = new BodyPartDef { hitPoints = 20 } };

    private static Hediff_AddedPart Replacement(Pawn pawn, BodyPartRecord part, float efficiency) => new()
    {
        pawn = pawn, Part = part,
        def = new HediffDef { addedPartProps = new AddedBodyPartProps { partEfficiency = efficiency } }
    };
}
