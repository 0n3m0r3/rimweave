#if DEBUG

using System.Text;
using LudeonTK;
using Verse;
using RimWeave.Logging;

namespace RimWeave.Debugging;

internal static class BodyInspectionActions
{
    [DebugAction("RimWeave", "Log selected pawn body",
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void LogSelectedPawnBody()
    {
        if (Find.Selector.SingleSelectedThing is not Pawn pawn)
        {
            RimWeaveLog.Message("Select one pawn on the map.");
            return;
        }

        var body = pawn.RaceProps.body;
        var parts = body.AllParts;
        var health = pawn.health.hediffSet;
        var report = new StringBuilder();

        report.AppendLine($"Body snapshot: pawn={pawn.ThingID}, body={body.defName}");

        for (int index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            int parentIndex = parts.IndexOf(part.parent);

            report.AppendLine(
                $"slot={index}, parent={parentIndex}, " +
                $"def={part.def.defName}, label={part.Label}, " +
                $"directMissing={health.PartIsMissing(part)}"
            );
        }

        foreach (var hediff in health.hediffs)
        {
            string target = "whole body";
            if (hediff.Part is not null)
            {
                int partIndex = parts.IndexOf(hediff.Part);
                target = partIndex >= 0 ? $"slot={partIndex}" : "unresolved part";
            }

            report.AppendLine($"hediff={hediff.def.defName}, target={target}");
        }

        RimWeaveLog.Message(report.ToString());
    }
}

#endif