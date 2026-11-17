using System.Reflection;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class NativePartHealthTests
{
    [Fact]
    public void MaximumHealthUsesNativeHealthScaleAndRounding()
    {
        var pawn = NativeHealthReadPawns.Create(health: 1.25f);
        var definition = new BodyPartDef { hitPoints = 7 };
        Assert.Equal(9f, definition.GetMaxHealth(pawn));
    }

    [Fact]
    public void CurrentHealthCountsOnlyInjuriesOnTheExactOccurrence()
    {
        var pawn = NativeHealthReadPawns.Create();
        var definition = new BodyPartDef { hitPoints = 20, destroyableByDamage = true };
        var left = new BodyPartRecord { def = definition };
        var right = new BodyPartRecord { def = definition };
        var leftInjury = Injury(pawn, left, 3f);
        var rightInjury = Injury(pawn, right, 8f);
        var other = new Hediff { pawn = pawn, Part = left, def = new HediffDef { maxSeverity = 100f }, Severity = 12f };
        pawn.health.hediffSet.hediffs.AddRange([leftInjury, rightInjury, other]);
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(17f, pawn.health.hediffSet.GetPartHealth(left));
            Assert.Equal(12f, pawn.health.hediffSet.GetPartHealth(right));
        }
        Assert.Equal(before, random.GetValue(null));
        Assert.Equal(new Hediff[] { leftInjury, rightInjury, other }, pawn.health.hediffSet.hediffs);
        Assert.Equal(3f, leftInjury.Severity);
        Assert.Equal(12f, other.Severity);
    }

    [Theory]
    [InlineData(true, true, 0f)]
    [InlineData(false, true, 1f)]
    [InlineData(true, false, 1f)]
    public void NativeDamageFloorsRemainAuthoritative(bool injuryDestroys, bool partDestroyable, float expected)
    {
        var pawn = NativeHealthReadPawns.Create();
        var part = new BodyPartRecord { def = new BodyPartDef { hitPoints = 10, destroyableByDamage = partDestroyable } };
        var injury = Injury(pawn, part, 20f);
        injury.destroysBodyParts = injuryDestroys;
        pawn.health.hediffSet.hediffs.Add(injury);
        Assert.Equal(expected, pawn.health.hediffSet.GetPartHealth(part));
    }

    [Fact]
    public void AnExplicitlyMissingOccurrenceHasNoCurrentHealth()
    {
        var pawn = NativeHealthReadPawns.Create();
        var part = new BodyPartRecord { def = new BodyPartDef { hitPoints = 10 } };
        pawn.health.hediffSet.hediffs.Add(new Hediff_MissingPart { pawn = pawn, Part = part });
        Assert.Equal(0f, pawn.health.hediffSet.GetPartHealth(part));
        Assert.Equal(10f, part.def.GetMaxHealth(pawn));
    }

    private static Hediff_Injury Injury(Pawn pawn, BodyPartRecord part, float severity) => new()
    {
        pawn = pawn, Part = part, def = new HediffDef { maxSeverity = 100f },
        Severity = severity, destroysBodyParts = true
    };
}
