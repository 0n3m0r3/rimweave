#if DEBUG
using LudeonTK;
using RimWeave.Diagnostics;
using Verse;

namespace RimWeave.Debugging;

internal static class HealthContributionInspectionActions
{
    [DebugAction("RimWeave", "Inspect selected pawn health contributions",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void InspectSelectedPawn()
    {
        if (Find.Selector.SingleSelectedThing is Pawn pawn)
            Show(pawn);
        else
            InspectionPresentation.Show("Select one pawn on the map.", title: "Health contributions");
    }

    internal static void Show(Pawn pawn) =>
        InspectionPresentation.Show(HealthContributionReport.Create(pawn), title: "Health contributions");
}
#endif
