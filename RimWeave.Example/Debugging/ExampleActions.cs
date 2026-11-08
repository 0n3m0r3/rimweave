#if DEBUG

using LudeonTK;
using RimWeave.Example.Health;
using RimWeave.Health;
using RimWeave.Logging;
using RimWorld;
using Verse;

namespace RimWeave.Example.Debugging;

internal static class ExampleActions
{
    private enum MarkerAction
    {
        Add,
        Remove,
        VerifyRepeatedRemoval
    }

    [DebugAction("RimWeave Example", "Add marker to hand",
        actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void AddMarker(Pawn pawn) => ChooseHand(pawn, MarkerAction.Add);

    [DebugAction("RimWeave Example", "Remove marker from hand",
        actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void RemoveMarker(Pawn pawn) => ChooseHand(pawn, MarkerAction.Remove);

    [DebugAction("RimWeave Example", "Verify repeated marker removal",
        actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void VerifyRepeatedMarkerRemoval(Pawn pawn) =>
        ChooseHand(pawn, MarkerAction.VerifyRepeatedRemoval);

    private static void ChooseHand(Pawn pawn, MarkerAction action)
    {
        if (DebugTools.curTool is null)
        {
            return;
        }

        DebugTools.curTool = null;

        if (!Prefs.DevMode)
        {
            Reject("Developer mode is disabled.");
            return;
        }

        if (!IsAvailable(pawn))
        {
            Reject("The target is no longer available.");
            return;
        }

        var options = new List<FloatMenuOption>();
        var parts = pawn.RaceProps.body.AllParts;

        foreach (var part in parts.Where(part => part.def == BodyPartDefOf.Hand))
        {
            string label = $"{pawn.LabelShort}: {part.Label} (slot {parts.IndexOf(part)})";
            options.Add(new FloatMenuOption(label, () => Apply(pawn, part, action)));
        }

        if (options.Count == 0)
        {
            Reject("This pawn has no hand slots.");
            return;
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void Apply(Pawn pawn, BodyPartRecord part, MarkerAction action)
    {
        if (!Prefs.DevMode)
        {
            Reject("Developer mode is disabled.");
            return;
        }

        if (!IsAvailable(pawn) || !pawn.RaceProps.body.AllParts.Contains(part))
        {
            Reject("The target is no longer available.");
            return;
        }

        var health = pawn.health.hediffSet;
        var definition = DefDatabase<HediffDef>.GetNamed("RimWeave_ExampleMarker");
        var matches = health.hediffs
            .Where(hediff => hediff.def == definition && hediff.Part == part)
            .ToList();

        if (matches.Count > 1)
        {
            Reject("Multiple markers found; refusing an ambiguous operation.");
            return;
        }

        var existing = matches.SingleOrDefault();

        if (action != MarkerAction.Add)
        {
            if (existing is null)
            {
                Reject("No marker on this hand.");
                return;
            }

            if (action == MarkerAction.VerifyRepeatedRemoval)
            {
                VerifyRepeatedRemoval(pawn, existing);
                return;
            }

            HediffRemovalResult result = HediffOperations.Remove(pawn, existing);
            RimWeaveLog.Message($"Example remove result: status={result.Status}");

            if (result.Status != HediffRemovalStatus.Removed)
            {
                Reject($"Marker removal returned {result.Status}.");
            }
            return;
        }

        for (BodyPartRecord? current = part; current is not null; current = current.parent)
        {
            if (health.PartIsMissing(current))
            {
                Reject("The hand or an ancestor is missing.");
                return;
            }
        }

        if (existing is not null)
        {
            Reject("A marker already exists on this hand.");
            return;
        }

        var marker = HediffMaker.MakeHediff(definition, pawn, part);
        pawn.health.AddHediff(marker, part);
        RimWeaveLog.Message($"Example add result: attached={health.hediffs.Contains(marker)}");
    }

    private static void VerifyRepeatedRemoval(Pawn pawn, Hediff marker)
    {
        if (marker is not HediffWithComps withComps)
        {
            Reject("Repeated removal check requires a marker with components.");
            return;
        }

        var firstObserver = withComps.comps.OfType<HediffComp_FirstObserver>().SingleOrDefault();
        var secondObserver = withComps.comps.OfType<HediffComp_SecondObserver>().SingleOrDefault();

        if (firstObserver is null || secondObserver is null)
        {
            Reject("Repeated removal check requires both example observers.");
            return;
        }

        int firstBefore = firstObserver.RemovalCallbackCount;
        int secondBefore = secondObserver.RemovalCallbackCount;

        HediffRemovalResult first = HediffOperations.Remove(pawn, marker);
        int firstAfter = firstObserver.RemovalCallbackCount;
        int secondAfter = secondObserver.RemovalCallbackCount;

        if (first.Status != HediffRemovalStatus.Removed)
        {
            Reject($"Repeated marker removal FAIL: first={first.Status}; second call skipped.");
            return;
        }

        // Keep the removed instance: looking it up again would bypass the API's absence guard.
        HediffRemovalResult second = HediffOperations.Remove(pawn, marker);
        int firstDelta = firstAfter - firstBefore;
        int secondDelta = secondAfter - secondBefore;
        int firstRepeatDelta = firstObserver.RemovalCallbackCount - firstAfter;
        int secondRepeatDelta = secondObserver.RemovalCallbackCount - secondAfter;

        bool passed = second.Status == HediffRemovalStatus.NotPresent
            && firstDelta == 1 && secondDelta == 1
            && firstRepeatDelta == 0 && secondRepeatDelta == 0
            && !pawn.health.hediffSet.hediffs.Any(current => ReferenceEquals(current, marker));

        string message = $"Repeated marker removal {(passed ? "PASS" : "FAIL")}: "
            + $"first={first.Status}, second={second.Status}; "
            + $"callbacks first=[{firstDelta},{secondDelta}], "
            + $"second=[{firstRepeatDelta},{secondRepeatDelta}].";

        if (!passed)
        {
            Reject(message);
            return;
        }

        RimWeaveLog.Message(message);
        Messages.Message(message, MessageTypeDefOf.TaskCompletion, historical: false);
    }

    private static bool IsAvailable(Pawn pawn) =>
        !pawn.Destroyed && !pawn.Dead && pawn.Spawned && pawn.Map == Find.CurrentMap;

    private static void Reject(string reason)
    {
        string message = $"Example: {reason}";
        RimWeaveLog.Message(message);
        Messages.Message(message, MessageTypeDefOf.RejectInput, historical: false);
    }
}

#endif
