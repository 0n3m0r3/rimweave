using System.Text;
using Verse;
using static RimWeave.Diagnostics.DiagnosticText;

namespace RimWeave.Diagnostics;

// A bounded observation of native slots; no anatomical state or presentation getters.
internal static class BodyReport
{
    internal const int MaxParts = 100;
    internal const int MaxEffects = 40;
    private const string CompleteDiagnostic = "Diagnostic: complete for the inspected native body.";

    internal static string Create(Pawn pawn)
    {
        ArgumentNullException.ThrowIfNull(pawn);
        var body = pawn.def?.race?.body;
        var health = pawn.health?.hediffSet;
        if (body is null || health?.hediffs is null)
            return "Body snapshot unavailable: native body or health storage is not initialized.";

        var parts = body.AllParts;
        var effects = health.hediffs;
        bool limited = parts.Count > MaxParts || effects.Count > MaxEffects;
        var report = new StringBuilder();
        Append(report, $"Body snapshot: pawn={Field(pawn.GetUniqueLoadID())}, body={Field(body.defName)}\n"
            + $"Parts inspected: {Math.Min(parts.Count, MaxParts)}/{parts.Count} (limit {MaxParts}).\n"
            + $"Effects inspected: {Math.Min(effects.Count, MaxEffects)}/{effects.Count} (limit {MaxEffects}).\n"
            + (limited ? "Diagnostic: INCOMPLETE (entry limit reached).\n" : CompleteDiagnostic + "\n")
            + "Slots are positions in this body's native list, not persistent organ identities.\n"
            + "Labels come from native definitions or custom labels; no health-effect label getter is invoked.\n");
        for (int index = 0; index < Math.Min(parts.Count, MaxParts); index++)
        {
            var part = parts[index];
            int parent = parts.FindIndex(candidate => ReferenceEquals(candidate, part.parent));
            if (!Append(report, $"slot={index}, parent={parent}, def={Field(part.def?.defName)}, "
                + $"label={Field(part.customLabel ?? part.def?.label)}, directMissing={health.PartIsMissing(part)}\n"))
                return report.ToString();
        }
        foreach (var effect in effects.Take(MaxEffects))
        {
            int slot = parts.FindIndex(part => ReferenceEquals(part, effect.Part));
            string target = effect.Part is null ? "whole body"
                : slot < 0 ? "unresolved part" : $"slot={slot}";
            if (!Append(report, $"hediff={Field(effect.def?.defName)}, target={target}\n"))
                break;
        }
        return report.ToString();
    }

    private static bool Append(StringBuilder report, string text) =>
        DiagnosticText.Append(report, text, CompleteDiagnostic);
}
