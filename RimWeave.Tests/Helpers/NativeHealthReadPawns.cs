using System.Reflection;
using RimWorld;
using Verse;

namespace RimWeave.Tests.Helpers;

internal static class NativeHealthReadPawns
{
    // A getter fixture, not a loaded pawn. Never use it for native mutation or colony checks.
    internal static Pawn Create(float size = 1f, float sizeFactor = 1f, float health = 1f, float healthFactor = 1f)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        pawn.RaceProps.baseBodySize = size;
        pawn.RaceProps.baseHealthScale = health;
        pawn.RaceProps.lifeStageAges = [new LifeStageAge
        {
            def = new LifeStageDef { bodySizeFactor = sizeFactor, healthScaleFactor = healthFactor }
        }];
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        // Model completed native initialization without invoking unrelated lifecycle hooks.
        typeof(Pawn_AgeTracker).GetField("cachedLifeStageIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pawn.ageTracker, 0);
        return pawn;
    }
}
