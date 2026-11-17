using System.Text;
using RimWeave.Contributions;
using RimWeave.Health;
using Verse;
using static RimWeave.Diagnostics.DiagnosticText;

namespace RimWeave.Diagnostics;

// A one-shot view of native health storage, not a registry or a saved gameplay object.
internal static class HealthContributionReport
{
    internal const int MaxEffects = 40;
    internal const int MaxFieldLength = DiagnosticText.MaxFieldLength;
    internal const int MaxReportLength = DiagnosticText.MaxReportLength;
    private const string CompleteDiagnostic = "Diagnostic: complete for available RimWeave declarations.";

    internal static string Create(Pawn pawn)
    {
        ArgumentNullException.ThrowIfNull(pawn);
        var nativeEffects = pawn.health?.hediffSet?.hediffs;
        if (nativeEffects is null)
            return "Health contributions unavailable: this pawn has no initialized native health list.";

        var effects = nativeEffects.Take(MaxEffects).ToArray();
        bool limited = nativeEffects.Count > effects.Length;
        var parts = new List<BodyPartRecord?>();
        foreach (var effect in effects)
            if (!parts.Any(part => ReferenceEquals(part, effect.Part))) parts.Add(effect.Part);
        var scopes = parts.Select(part => new Scope(part,
            effects.Where(effect => ReferenceEquals(effect.Part, part)).ToArray())).ToArray();

        int unreadable = scopes.Sum(scope => scope.Contributions.Count(entry => entry.Metadata.LoadError is not null));
        int ambiguous = 0;
        int conflicts = 0;
        foreach (var scope in scopes)
        {
            foreach (var identity in scope.Contributions.Select(entry => entry.Metadata.Identity).OfType<ContributionId>().Distinct())
                if (ContributionResolver.Resolve(scope.Contributions, identity, static effect => effect.Metadata).Matches.Count > 1) ambiguous++;
            foreach (var claim in scope.Contributions.Select(entry => entry.Metadata.ExclusiveResponsibility).OfType<string>().Distinct(StringComparer.Ordinal))
                if (ContributionResolver.ResolveExclusiveClaim(scope.Contributions, claim, static effect => effect.Metadata).Matches.Count > 1) conflicts++;
        }

        var report = new StringBuilder();
        Append(report, $"Pawn native ID: {Field(pawn.GetUniqueLoadID())}\n"
            + $"Effects inspected: {effects.Length}/{nativeEffects.Count} (limit {MaxEffects}).\n"
            + (limited || unreadable > 0 ? "Diagnostic: INCOMPLETE.\n" : CompleteDiagnostic + "\n")
            + $"Review: {ambiguous} known identity ambiguities; {conflicts} known exclusive-claim conflicts; "
            + $"{unreadable} unreadable metadata entries.\n"
            + (limited ? "Effect limit reached: all lookups are incomplete; omitted effects may add matches or claims.\n" : "")
            + "Declared metadata is read from ContributionHediff; other health effects are listed without attribution.\n"
            + "Owners and origins are declarations, not proof of the creating mod or gameplay compatibility.\n"
            + "Part numbers distinguish exact occurrences within this report, not persistent organs.\n"
            + "Opaque source values are hidden. Close and reopen to refresh.\n");

        if (effects.Length == 0) Append(report, "\nNo native health effects.\n");
        for (int index = 0; index < scopes.Length; index++)
        {
            var scope = scopes[index];
            if (!Append(report, "\nScope: " + ScopeName(scope.Part, index + 1) + "\n")) break;
            foreach (var effect in scope.Effects)
            {
                var block = new StringBuilder();
                block.AppendLine($"\n[{effect.GetUniqueLoadID()}] {Field(effect.def?.defName)}");
                block.AppendLine("Definition package: " + (effect.def?.modContentPack?.PackageId is { } package
                    ? Field(package) + " (definition source, not the creating mod)" : "unavailable"));
                if (effect is not ContributionHediff contribution)
                {
                    block.AppendLine("Metadata: not declared (non-cooperating effect).");
                    block.AppendLine("Owner / contribution / origin / exclusive claim: not declared.");
                }
                else
                {
                    // Bind the nonvirtual base property; a subclass can reimplement IContribution.
                    var metadata = contribution.Metadata;
                    if (metadata.LoadError is not null)
                    {
                        block.AppendLine("Metadata: unreadable saved metadata.");
                        block.AppendLine("Declared fields unavailable; this scope's lookups are incomplete.");
                    }
                    else
                    {
                        block.AppendLine("Metadata: available.");
                        block.AppendLine($"Owner: {Field(metadata.Identity?.OwnerId)}");
                        block.AppendLine($"Contribution: {Field(metadata.Identity?.LocalId)}");
                        block.AppendLine("Origin: " + (metadata.Origin is null ? "not declared"
                            : metadata.Origin.Type == ContributionOriginType.Unknown ? "explicitly unknown"
                            : $"{Field(metadata.Origin.Type.NamespaceId)}/{Field(metadata.Origin.Type.LocalId)}"));
                        block.AppendLine("Source: " + (metadata.Origin?.SourceReference is null ? "not declared"
                            : "declared (opaque; value hidden, not resolved)"));
                        block.AppendLine($"Exclusive claim: {Field(metadata.ExclusiveResponsibility)}");
                        if (metadata.Identity is { } identity)
                            block.AppendLine("Identity lookup: " + Lookup(
                                ContributionResolver.Resolve(scope.Contributions, identity, static effect => effect.Metadata), limited, exclusive: false));
                        if (metadata.ExclusiveResponsibility is { } claim)
                            block.AppendLine("Exclusive lookup: " + Lookup(
                                ContributionResolver.ResolveExclusiveClaim(scope.Contributions, claim, static effect => effect.Metadata), limited, exclusive: true));
                    }
                }
                if (!Append(report, block.ToString())) return report.ToString();
            }
        }
        return report.ToString();
    }

    private static string Lookup(ContributionLookupResult<ContributionHediff> result, bool limited, bool exclusive)
    {
        int count = result.Matches.Count;
        string noun = exclusive ? "claimants" : "matches";
        string multiple = exclusive ? $"conflict ({count} explicit claimants)" : $"ambiguous ({count} matches)";
        if (result.Status == ContributionLookupStatus.InvalidMetadata || limited)
        {
            string reason = result.Status == ContributionLookupStatus.InvalidMetadata
                ? "unreadable metadata" : "effect limit reached";
            return $"incomplete ({reason}; " + (count > 1 ? $"known {multiple}" : $"{count} inspected {noun}")
                + ")" + MatchingEffects(result);
        }
        return result.Status switch
        {
            ContributionLookupStatus.Found => exclusive ? "single claimant" : "unique (1 match)",
            ContributionLookupStatus.Ambiguous => multiple + MatchingEffects(result),
            _ => "not present"
        };
    }

    private static string MatchingEffects(ContributionLookupResult<ContributionHediff> result) => result.Matches.Count > 1
        ? "\n  Matching effects: " + string.Join(", ", result.Matches.Select(entry => entry.GetUniqueLoadID()))
        : "";

    private static string ScopeName(BodyPartRecord? part, int number) => part is null
        ? "whole body"
        : $"part {number}; label={Field(part.customLabel ?? part.def?.label)}; def={Field(part.def?.defName)}";

    private static bool Append(StringBuilder report, string text) =>
        DiagnosticText.Append(report, text, CompleteDiagnostic);

    private sealed class Scope(BodyPartRecord? part, Hediff[] effects)
    {
        internal BodyPartRecord? Part { get; } = part;
        internal Hediff[] Effects { get; } = effects;
        // Preserve native instance identity for the common resolver; bind base metadata at each query.
        internal ContributionHediff[] Contributions { get; } = effects.OfType<ContributionHediff>().ToArray();
    }

}
