#if DEBUG
using LudeonTK;
using RimWeave.Diagnostics;
using RimWeave.Logging;
using Verse;

namespace RimWeave.Debugging;

internal static class BodyInspectionActions
{
    [DebugAction("RimWeave", "Log selected pawn body",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void LogSelectedPawnBody()
    {
        if (Find.Selector.SingleSelectedThing is Pawn pawn)
            Show(pawn);
        else
            InspectionPresentation.PreserveRandomState(() => RimWeaveLog.Message("Select one pawn on the map."));
    }

    internal static void Show(Pawn pawn) =>
        InspectionPresentation.PreserveRandomState(() => RimWeaveLog.Message(BodyReport.Create(pawn)));
}
#endif
