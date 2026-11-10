#if DEBUG
using Verse;

namespace RimWeave.Debugging;

// Shared by developer reports. Compute reports and execute gameplay commands before presenting them.
internal static class InspectionPresentation
{
    internal static void Show(string report, string? title = null, MessageTypeDef? notificationType = null)
    {
        PreserveRandomState(() =>
        {
            if (notificationType is not null)
                Messages.Message(report.Split('\n')[0], notificationType, historical: false);
            Find.WindowStack.Add(new InspectionDialog(report, title));
        });
    }

    internal static void PreserveRandomState(Action presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        // Native UI sounds randomize their samples. Never wrap the gameplay action producing a report.
        Rand.PushState();
        try
        {
            presentation();
        }
        finally
        {
            Rand.PopState();
        }
    }

    private sealed class InspectionDialog(string report, string? title) : Dialog_MessageBox(report, title: title)
    {
        // Rendering and button sounds happen after Show has returned.
        public override void WindowOnGUI() => PreserveRandomState(base.WindowOnGUI);

        // Keyboard dismissal can reach Close outside this window's render callback.
        public override void Close(bool doCloseSound = true) =>
            PreserveRandomState(() => base.Close(doCloseSound));
    }
}
#endif
