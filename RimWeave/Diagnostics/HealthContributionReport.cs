using System.Globalization;
using System.Text;
using RimWeave.Contributions;
using RimWeave.Health;
using Verse;

namespace RimWeave.Diagnostics;

// A one-shot view of native health storage, not a registry or a saved gameplay object.
internal static class HealthContributionReport
{
    internal const int MaxEffects = 40;
    internal const int MaxFieldLength = 120;
    internal const int MaxReportLength = 16000;
    private const string CompleteDiagnostic = "Diagnostic: complete for available RimWeave declarations.";
    private const string TruncatedReport =
        "\n[REPORT TRUNCATED: incomplete diagnostic. Omitted effects and conclusions are not displayed.]\n";

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
                if (ContributionResolver.Resolve(scope.Contributions, identity).Matches.Count > 1) ambiguous++;
            foreach (var claim in scope.Contributions.Select(entry => entry.Metadata.ExclusiveResponsibility).OfType<string>().Distinct(StringComparer.Ordinal))
                if (ContributionResolver.ResolveExclusiveClaim(scope.Contributions, claim).Matches.Count > 1) conflicts++;
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
                                ContributionResolver.Resolve(scope.Contributions, identity), limited, exclusive: false));
                        if (metadata.ExclusiveResponsibility is { } claim)
                            block.AppendLine("Exclusive lookup: " + Lookup(
                                ContributionResolver.ResolveExclusiveClaim(scope.Contributions, claim), limited, exclusive: true));
                    }
                }
                if (!Append(report, block.ToString())) return report.ToString();
            }
        }
        return report.ToString();
    }

    private static string Lookup(ContributionLookupResult<ContributionView> result, bool limited, bool exclusive)
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

    private static string MatchingEffects(ContributionLookupResult<ContributionView> result) => result.Matches.Count > 1
        ? "\n  Matching effects: " + string.Join(", ", result.Matches.Select(entry => entry.NativeIdentity))
        : "";

    private static string ScopeName(BodyPartRecord? part, int number) => part is null
        ? "whole body"
        : $"part {number}; label={Field(part.customLabel ?? part.def?.label)}; def={Field(part.def?.defName)}";

    private static string Field(string? value)
    {
        if (value is null) return "not declared";
        var result = new StringBuilder();
        foreach (char character in value.AsSpan(0, Math.Min(value.Length, MaxFieldLength)))
        {
            // Keep provider text on one line and prevent native rich-text markup or hidden controls.
            if (character == '\\') result.Append("\\\\");
            else if (character is '<' or '>' || char.IsControl(character)
                || char.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.LineSeparator
                    or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate)
                result.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            else result.Append(character);
        }
        if (value.Length > MaxFieldLength) result.Append(" [field truncated]");
        return result.ToString();
    }

    private static bool Append(StringBuilder report, string text)
    {
        int remaining = MaxReportLength - TruncatedReport.Length - report.Length;
        if (text.Length <= remaining)
        {
            report.Append(text);
            return true;
        }
        report.Append(text.AsSpan(0, Math.Max(0, remaining)));
        report.Append(TruncatedReport);
        // A clipped display must not retain the complete-report headline. Field text cannot inject newlines.
        int headline = report.ToString().IndexOf("\n" + CompleteDiagnostic + "\n", StringComparison.Ordinal);
        if (headline >= 0)
            report.Remove(headline + 1, CompleteDiagnostic.Length)
                .Insert(headline + 1, "Diagnostic: INCOMPLETE (report text limit reached).");
        return false;
    }

    private sealed class Scope(BodyPartRecord? part, Hediff[] effects)
    {
        internal BodyPartRecord? Part { get; } = part;
        internal Hediff[] Effects { get; } = effects;
        internal ContributionView[] Contributions { get; } = effects.OfType<ContributionHediff>()
            .Select(effect => new ContributionView(effect)).ToArray();
    }

    // Only these local, sealed candidates reach the common resolver; no provider getter is invoked.
    private sealed class ContributionView(ContributionHediff effect) : IContribution
    {
        public ContributionMetadata Metadata { get; } = effect.Metadata;
        internal string NativeIdentity { get; } = effect.GetUniqueLoadID();
    }
}
