#if DEBUG
using LudeonTK;
using RimWeave.Health;
using RimWeave.Logging;
using RimWorld;
using Verse;

namespace RimWeave.Example.Composition;

internal static class CompositionActions
{
    [DebugAction("RimWeave Example", "Composition A: add on first hand",
        actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    // ReSharper disable once UnusedMember.Local -- Discovered through the native DebugAction attribute.
    private static void Add(Pawn pawn) => Run(pawn, remove: false);

    [DebugAction("RimWeave Example", "Composition A: remove on first hand",
        actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    // ReSharper disable once UnusedMember.Local -- Discovered through the native DebugAction attribute.
    private static void Remove(Pawn pawn) => Run(pawn, remove: true);

    private static void Run(Pawn pawn, bool remove)
    {
        if (DebugTools.curTool is null) return;
        DebugTools.curTool = null;
        RimWeaveLog.Message(Execute(Current.Game, pawn, remove));
    }

    internal static string Execute(Game? expectedGame, Pawn? pawn, bool remove)
    {
        // Validate at execution time, before the native factory can allocate an ID or run PostMake.
        if (!Prefs.DevMode) return "Composition A REFUSED: developer mode is disabled.";
        if (expectedGame is null || !ReferenceEquals(Current.Game, expectedGame)
            || Scribe.mode != LoadSaveMode.Inactive)
            return "Composition A REFUSED: the selected game is unavailable or is loading.";
        if (pawn is null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.Map != Find.CurrentMap)
            return "Composition A REFUSED: select an available pawn on the current map.";
        var part = pawn.RaceProps.body.AllParts.FirstOrDefault(candidate => candidate.def == BodyPartDefOf.Hand);
        if (part is null) return "Composition A REFUSED: this body has no native hand slot.";

        try
        {
            // Use a fixed body slot even when it is missing; never silently switch to the other hand.
            string outcome;
            if (remove)
            {
                var result = CompositionContribution.Remove(pawn, part);
                outcome = $"REMOVE: lookup={result.Lookup.Status}; removal={result.Removal?.Status.ToString() ?? "not requested"}";
            }
            else
            {
                var result = CompositionContribution.Add(pawn, part);
                outcome = $"ADD: {result.Status}";
            }
            var claim = ContributionQueries.ResolveExclusiveClaim(pawn, CompositionContribution.Responsibility, part);
            return $"Composition A {outcome}; claim={claim.Status}; claimants={claim.Matches.Count}.";
        }
        catch (Exception error)
        {
            return $"Composition A FAILED: {error.GetType().Name}: {error.Message}. Inspect native state before retrying.";
        }
    }
}
#endif
