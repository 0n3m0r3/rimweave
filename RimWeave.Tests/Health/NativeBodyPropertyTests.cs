using System.Reflection;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeBodyPropertyTests
{
    [Theory]
    [InlineData(2f, 0.25f, 3f, 0.5f, 0.5f, 1.5f)]
    [InlineData(0.5f, 1f, 2f, 1.25f, 0.5f, 2.5f)]
    [InlineData(1f, 1f, 1f, 1f, 1f, 1f)]
    public void NativeSizeAndHealthScale_UseIndependentRaceAndLifeStageFactors(
        float size, float sizeFactor, float health, float healthFactor, float expectedSize, float expectedHealth)
    {
        var pawn = NativeHealthReadPawns.Create(size, sizeFactor, health, healthFactor);
        Assert.Equal(expectedSize, pawn.BodySize);
        Assert.Equal(expectedHealth, pawn.HealthScale);
    }

    [Fact]
    public void NativeRead_DoesNotReinterpretHealthEffectSeverityOrChangeRandomState()
    {
        var pawn = NativeHealthReadPawns.Create(2f, 0.25f, 3f, 0.5f);
        var effect = new Hediff { pawn = pawn, def = new HediffDef { maxSeverity = 100f }, Severity = 9f };
        pawn.health.hediffSet.hediffs.Add(effect);
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);
        float severity = effect.Severity;
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0.5f, pawn.BodySize);
            Assert.Equal(1.5f, pawn.HealthScale);
        }
        Assert.Equal(before, random.GetValue(null));
        Assert.Equal(severity, effect.Severity);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void RetainedValuesRemainObservationsWhenTheNativeDefinitionChanges()
    {
        var pawn = NativeHealthReadPawns.Create(2f, 0.25f, 3f, 0.5f);
        var observed = (Size: pawn.BodySize, HealthScale: pawn.HealthScale);
        pawn.ageTracker.CurLifeStage.bodySizeFactor = 0.75f;
        Assert.Equal(0.5f, observed.Size);
        Assert.Equal(1.5f, pawn.BodySize);
        Assert.Equal(observed.HealthScale, pawn.HealthScale);
    }
}
