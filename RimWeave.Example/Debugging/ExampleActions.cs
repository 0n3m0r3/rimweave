#if DEBUG

using LudeonTK;
using RimWeave.Logging;
using RimWorld;
using Verse;

namespace RimWeave.Example.Debugging;

internal static class ExampleActions
{
    [DebugAction("RimWeave Example", "Add marker to hand",
        actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void AddMarker(Pawn pawn) => ChooseHand(pawn, remove: false);

    [DebugAction("RimWeave Example", "Remove marker from hand",
        actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void RemoveMarker(Pawn pawn) => ChooseHand(pawn, remove: true);

    private static void ChooseHand(Pawn pawn, bool remove)
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
            options.Add(new FloatMenuOption(label, () => Apply(pawn, part, remove)));
        }

        if (options.Count == 0)
        {
            Reject("This pawn has no hand slots.");
            return;
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void Apply(Pawn pawn, BodyPartRecord part, bool remove)
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

        if (remove)
        {
            if (existing is null)
            {
                Reject("No marker on this hand.");
                return;
            }

            pawn.health.RemoveHediff(existing);
            RimWeaveLog.Message($"Example remove result: attached={health.hediffs.Contains(existing)}");
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
