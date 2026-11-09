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
            ShowReport("Select one pawn on the map.");
    }

    internal static void Show(Pawn pawn) =>
        ShowReport(HealthContributionReport.Create(pawn));

    private static void ShowReport(string report)
    {
        // Native dialog sounds randomize their samples. Keep presentation out of simulation randomness.
        Rand.PushState();
        try
        {
            Find.WindowStack.Add(new InspectionDialog(report));
        }
        finally
        {
            Rand.PopState();
        }
    }

    private sealed class InspectionDialog(string report) : Dialog_MessageBox(report, title: "Health contributions")
    {
        public override void WindowOnGUI()
        {
            // Rendering and button sounds happen after ShowReport has returned.
            Rand.PushState();
            try
            {
                base.WindowOnGUI();
            }
            finally
            {
                Rand.PopState();
            }
        }

        public override void Close(bool doCloseSound = true)
        {
            // Keyboard dismissal can reach Close outside this window's render callback.
            Rand.PushState();
            try
            {
                base.Close(doCloseSound);
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
#endif
