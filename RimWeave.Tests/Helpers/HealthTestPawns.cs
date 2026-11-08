using Verse;

namespace RimWeave.Tests.Helpers;

internal static class HealthTestPawns
{
    /// <summary>Creates a fresh pawn for health checks that must stop before native mutation.</summary>
    /// <remarks>
    /// Contains only race metadata and a health tracker. This is not a fully initialized
    /// game pawn and must not be used for successful health operations or integration tests.
    /// </remarks>
    internal static Pawn CreateForGuardChecks()
    {
        var pawn = new Pawn
        {
            def = new ThingDef { race = new RaceProperties() }
        };
        pawn.health = new Pawn_HealthTracker(pawn);
        return pawn;
    }
}