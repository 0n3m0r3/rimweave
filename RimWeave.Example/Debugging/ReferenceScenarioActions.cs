#if DEBUG
using LudeonTK;
using RimWeave.Debugging;
using RimWeave.Example.References;
using RimWeave.Logging;
using RimWorld;
using Verse;

namespace RimWeave.Example.Debugging;

internal enum ReferenceScenarioAction { Start, RoundTrip, AcknowledgeA, Inspect, Resume, RemoveExactEffect }

internal static class ReferenceScenarioActions
{
    [DebugAction("RimWeave Example", "Reference scenario: start", actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Start(Pawn pawn)
    {
        if (DebugTools.curTool is null) return;
        DebugTools.curTool = null;
        var game = Current.Game;
        // The deferred selection is an explicit action; recheck mode, game and target when it executes.
        Find.WindowStack.Add(new FloatMenu([
            new FloatMenuOption($"Start reference scenario on {pawn.LabelShort}",
                () => Show(Execute(game, ReferenceScenarioAction.Start, pawn)))
        ]));
    }

    [DebugAction("RimWeave Example", "Reference scenario: departure and return",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void RoundTrip() => Run(ReferenceScenarioAction.RoundTrip);

    [DebugAction("RimWeave Example", "Reference scenario: acknowledge A",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void AcknowledgeA() => Run(ReferenceScenarioAction.AcknowledgeA);

    [DebugAction("RimWeave Example", "Reference scenario: inspect",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Inspect() => Run(ReferenceScenarioAction.Inspect);

    [DebugAction("RimWeave Example", "Reference scenario: resume",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Resume() => Run(ReferenceScenarioAction.Resume);

    [DebugAction("RimWeave Example", "Reference scenario: remove exact effect",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void RemoveExactEffect() => Run(ReferenceScenarioAction.RemoveExactEffect);

    private static void Run(ReferenceScenarioAction action) => Show(Execute(Current.Game, action));

    internal static string Execute(Game expectedGame, ReferenceScenarioAction action, Pawn? pawn = null)
    {
        if (!Prefs.DevMode) return "Reference scenario REFUSED: developer mode is disabled.";
        if (expectedGame is null || !ReferenceEquals(Current.Game, expectedGame)
            || Scribe.mode != LoadSaveMode.Inactive)
            return "Reference scenario REFUSED: the selected game is no longer available or is loading.";
        var scenario = expectedGame.GetComponent<ReferenceScenarioGameComponent>();
        if (scenario is null) return "Reference scenario REFUSED: the example component is unavailable.";
        try
        {
            return action switch
            {
                ReferenceScenarioAction.Start when pawn is not null => scenario.Start(pawn),
                ReferenceScenarioAction.RoundTrip => scenario.RoundTrip(),
                ReferenceScenarioAction.AcknowledgeA => scenario.AcknowledgeA(),
                ReferenceScenarioAction.Inspect => scenario.Inspect(),
                ReferenceScenarioAction.Resume => scenario.Resume(),
                ReferenceScenarioAction.RemoveExactEffect => scenario.RemoveExactEffect(),
                _ => "Reference scenario REFUSED: select a pawn for the start action."
            };
        }
        catch (Exception error)
        {
            // Native operations may have partially completed. Never report rollback or retry them here.
            return $"Reference scenario FAILED: {error.GetType().Name}: {error.Message}. Inspect native state before continuing.";
        }
    }

    private static void Show(string report)
    {
        RimWeaveLog.Message(report);
        bool failed = report.Contains("FAIL", StringComparison.Ordinal) || report.Contains("REFUSED", StringComparison.Ordinal);
        InspectionPresentation.Show(report,
            notificationType: failed ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.TaskCompletion);
    }
}
#endif
