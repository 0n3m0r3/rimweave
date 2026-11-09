using System.Reflection;
using System.Runtime.CompilerServices;
using RimWeave.Contributions;
using RimWeave.Diagnostics;
using RimWeave.Health;
using RimWeave.Persistence;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Diagnostics;

[Collection(ScribeTestCollection.Name)]
public sealed class HealthContributionReportTests
{
    private static readonly PropertyInfo RandomState = typeof(Rand)
        .GetProperty("StateCompressed", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void Create_SeparatesDeclaredOwnerFromDefinitionProviderAndHidesOpaqueSource()
    {
        var fixture = new HealthFixture();
        var effect = fixture.Add(101, new ContributionId("declared.owner", "support"), fixture.Left);
        effect.def.modContentPack = DefinitionProvider("definition.provider");
        effect.Metadata.DescribeOrigin(new ContributionOrigin(
            new ContributionOriginType("example.causes", "Equipment"), "private-source-value"));

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("[Hediff_101]", report);
        Assert.Contains("definition.provider", report);
        Assert.Contains("Owner: declared.owner", report);
        Assert.Contains("Contribution: support", report);
        Assert.Contains("Origin: example.causes/Equipment", report);
        Assert.Contains("Source: declared (opaque; value hidden, not resolved)", report);
        Assert.DoesNotContain("private-source-value", report);
        Assert.DoesNotContain("private-installation-path", report);
    }

    [Fact]
    public void Create_DistinguishesOrdinaryEffectsUndeclaredFieldsAndExplicitUnknownOrigin()
    {
        var fixture = new HealthFixture();
        fixture.Pawn.health.hediffSet.hediffs.Add(new Hediff
        {
            pawn = fixture.Pawn, loadID = 201, def = new HediffDef { defName = "OrdinaryEffect" }
        });
        fixture.Add(202);
        fixture.Add(203).Metadata.DescribeOrigin(new ContributionOrigin(ContributionOriginType.Unknown));

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("Metadata: not declared (non-cooperating effect)", EffectBlock(report, 201));
        Assert.Contains("Metadata: available", EffectBlock(report, 202));
        Assert.Contains("Origin: not declared", EffectBlock(report, 202));
        Assert.Contains("Source: not declared", EffectBlock(report, 202));
        Assert.Contains("Origin: explicitly unknown", EffectBlock(report, 203));
        Assert.DoesNotContain("unreadable saved metadata", report);
    }

    [Fact]
    public void Create_RestrictsIdentityAmbiguityToTheExactPartAndKeepsWholeBodySeparate()
    {
        var fixture = new HealthFixture();
        var identity = new ContributionId("example.mod", "same-id");
        fixture.Add(301, identity, fixture.Left);
        fixture.Add(302, identity, fixture.Left);
        fixture.Add(303, identity, fixture.Right);
        fixture.Add(304, identity);

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("Identity lookup: ambiguous (2 matches)", EffectBlock(report, 301));
        Assert.Contains("Identity lookup: ambiguous (2 matches)", EffectBlock(report, 302));
        Assert.Contains("Identity lookup: unique (1 match)", EffectBlock(report, 303));
        Assert.Contains("Identity lookup: unique (1 match)", EffectBlock(report, 304));
        Assert.Contains("Scope: whole body", report);
        Assert.Contains("left hand", report);
        Assert.Contains("right hand", report);
        Assert.DoesNotContain("Exclusive lookup: conflict", report);
    }

    [Fact]
    public void Create_DoesNotConfuseEqualLocalNamesOwnedByDifferentProviders()
    {
        var fixture = new HealthFixture();
        fixture.Add(401, new ContributionId("example.first", "support"), fixture.Left);
        fixture.Add(402, new ContributionId("example.second", "support"), fixture.Left);

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("Identity lookup: unique (1 match)", EffectBlock(report, 401));
        Assert.Contains("Identity lookup: unique (1 match)", EffectBlock(report, 402));
        Assert.DoesNotContain("Identity lookup: ambiguous", report);
        Assert.DoesNotContain("Exclusive lookup: conflict", report);
    }

    [Fact]
    public void Create_ReportsOnlyExplicitExclusiveClaimantsInTheirExactScope()
    {
        var fixture = new HealthFixture();
        fixture.Add(501, new ContributionId("example.first", "support"), fixture.Left)
            .Metadata.ClaimExclusiveResponsibility("example/function");
        fixture.Add(502, new ContributionId("example.second", "support"), fixture.Left)
            .Metadata.ClaimExclusiveResponsibility("example/function");
        fixture.Add(503, new ContributionId("example.observer", "support"), fixture.Left);
        fixture.Add(504, new ContributionId("example.third", "support"), fixture.Right)
            .Metadata.ClaimExclusiveResponsibility("example/function");
        fixture.Add(505, new ContributionId("example.fourth", "support"), fixture.Left)
            .Metadata.ClaimExclusiveResponsibility("example/Function");

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("Exclusive lookup: conflict (2 explicit claimants)", EffectBlock(report, 501));
        Assert.Contains("Exclusive lookup: conflict (2 explicit claimants)", EffectBlock(report, 502));
        Assert.Contains("Matching effects:", EffectBlock(report, 501));
        Assert.Contains("Hediff_502", EffectBlock(report, 501));
        Assert.DoesNotContain("Hediff_503", EffectBlock(report, 501));
        Assert.DoesNotContain("Hediff_504", EffectBlock(report, 501));
        Assert.DoesNotContain("Exclusive lookup: conflict", EffectBlock(report, 503));
        Assert.Contains("Exclusive lookup: single claimant", EffectBlock(report, 504));
        Assert.Contains("Exclusive lookup: single claimant", EffectBlock(report, 505));
    }

    [Fact]
    public void Create_UnreadableMetadataMakesOnlyItsOwnScopeIncompleteWithoutLeakingTheError()
    {
        var fixture = new HealthFixture();
        var identity = new ContributionId("example.mod", "support");
        fixture.Add(601, identity, fixture.Left).Metadata.ClaimExclusiveResponsibility("example/function");
        var unreadable = fixture.Add(602, part: fixture.Left);
        MakeUnreadable(unreadable, @"Failed reading C:\private-test\save.rws: raw private payload");
        fixture.Add(603, identity, fixture.Right).Metadata.ClaimExclusiveResponsibility("example/function");

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("Identity lookup: incomplete", EffectBlock(report, 601));
        Assert.Contains("Exclusive lookup: incomplete", EffectBlock(report, 601));
        Assert.Contains("Metadata: unreadable saved metadata", EffectBlock(report, 602));
        Assert.Contains("Identity lookup: unique (1 match)", EffectBlock(report, 603));
        Assert.Contains("Exclusive lookup: single claimant", EffectBlock(report, 603));
        Assert.DoesNotContain("private-test", report);
        Assert.DoesNotContain("raw private payload", report);
    }

    [Fact]
    public void Create_DoesNotDeclareUniquenessWhenAnUnscannedEffectCouldConflict()
    {
        var fixture = new HealthFixture();
        var identity = new ContributionId("example.mod", "collision");
        fixture.Add(701, identity).Metadata.ClaimExclusiveResponsibility("example/function");
        for (int i = 1; i < HealthContributionReport.MaxEffects; i++)
            fixture.Add(701 + i, new ContributionId("example.mod", $"other-{i}"));
        fixture.Add(999, identity).Metadata.ClaimExclusiveResponsibility("example/function");

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Identity lookup: incomplete", EffectBlock(report, 701));
        Assert.Contains("Exclusive lookup: incomplete", EffectBlock(report, 701));
        Assert.DoesNotContain("Identity lookup: unique", report);
        Assert.DoesNotContain("Exclusive lookup: single claimant", report);
        Assert.DoesNotContain("[Hediff_999]", report);
        Assert.Equal(HealthContributionReport.MaxEffects + 1, fixture.Pawn.health.hediffSet.hediffs.Count);
    }

    [Fact]
    public void Create_IsPassiveAndNeverInvokesPresentationOrReimplementedMetadataGetters()
    {
        var fixture = new HealthFixture();
        var effect = fixture.Add<HostileContribution>(801, new ContributionId("example.mod", "support"));
        var identity = effect.Metadata.Identity;
        var nativeEffects = fixture.Pawn.health.hediffSet.hediffs.ToArray();
        ulong before = (ulong)RandomState.GetValue(null)!;
        try
        {
            string first = HealthContributionReport.Create(fixture.Pawn);
            string second = HealthContributionReport.Create(fixture.Pawn);

            Assert.Contains("Owner: example.mod", first);
            Assert.Equal(first, second);
            Assert.Equal(0, effect.HostileGetterCalls);
            Assert.Equal(before, (ulong)RandomState.GetValue(null)!);
            Assert.Equal(nativeEffects, fixture.Pawn.health.hediffSet.hediffs);
            Assert.Same(identity, effect.Metadata.Identity);
        }
        finally
        {
            RandomState.SetValue(null, before);
        }
    }

    [Fact]
    public void Create_RefreshesNativeMembershipInsteadOfKeepingAReportCache()
    {
        var fixture = new HealthFixture();
        var first = fixture.Add(901, new ContributionId("example.mod", "old"));
        string before = HealthContributionReport.Create(fixture.Pawn);
        fixture.Pawn.health.hediffSet.hediffs.Remove(first);
        fixture.Add(902, new ContributionId("example.mod", "new"));

        string after = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("[Hediff_901]", before);
        Assert.DoesNotContain("[Hediff_901]", after);
        Assert.Contains("[Hediff_902]", after);
        Assert.Single(fixture.Pawn.health.hediffSet.hediffs);
    }

    [Fact]
    public void Create_EscapesUntrustedFieldsAndMarksTheirTruncation()
    {
        var fixture = new HealthFixture();
        var effect = fixture.Add(1001);
        effect.def.defName = "marker\nOwner: forged\r\t\u0001"
            + new string('x', HealthContributionReport.MaxFieldLength) + "UNSEEN_TAIL";

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.Contains("[field truncated]", report);
        Assert.DoesNotContain("\nOwner: forged", report);
        Assert.DoesNotContain('\u0001', report);
        Assert.DoesNotContain("UNSEEN_TAIL", report);
        Assert.True(report.Length <= HealthContributionReport.MaxReportLength);
    }

    [Fact]
    public void Create_KeepsUnicodeSeparatorsAndInvalidUtf16InsideOneDisplayLine()
    {
        var fixture = new HealthFixture();
        var effect = fixture.Add(1051);
        effect.def.defName = "before\u2028\u2029\uD800\uDC00\uD800\u202Eafter";

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.DoesNotContain('\u2028', report);
        Assert.DoesNotContain('\u2029', report);
        Assert.DoesNotContain('\u202E', report);
        Assert.DoesNotContain(report, char.IsSurrogate);
        Assert.Contains("before", report);
        Assert.Contains("after", report);
    }

    [Fact]
    public void Create_BoundsTheCompleteReportAndMarksOmittedContent()
    {
        var fixture = new HealthFixture();
        for (int i = 0; i < HealthContributionReport.MaxEffects; i++)
        {
            var effect = fixture.Add(1100 + i,
                new ContributionId("example." + new string('a', 100), $"effect-{i}-" + new string('b', 100)));
            effect.def.defName = new string('d', HealthContributionReport.MaxFieldLength);
            effect.Metadata.ClaimExclusiveResponsibility(new string('c', 100));
            effect.Metadata.DescribeOrigin(new ContributionOrigin(
                new ContributionOriginType("cause." + new string('e', 100), new string('f', 100))));
        }

        string report = HealthContributionReport.Create(fixture.Pawn);

        Assert.True(report.Length <= HealthContributionReport.MaxReportLength);
        Assert.Contains("[REPORT TRUNCATED: incomplete diagnostic", report);
        Assert.Contains("Diagnostic: INCOMPLETE", report);
        Assert.DoesNotContain("Diagnostic: complete", report);
        Assert.Equal(HealthContributionReport.MaxEffects, fixture.Pawn.health.hediffSet.hediffs.Count);
    }

    [Fact]
    public void Create_ReportsUnavailableHealthAndRejectsANullOwner()
    {
        var pawn = new Pawn { def = new ThingDef { defName = "NoHealthPawn", race = new RaceProperties() } };

        Assert.Contains("unavailable", HealthContributionReport.Create(pawn), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentNullException>(() => HealthContributionReport.Create(null!));
    }

    private static string EffectBlock(string report, int id)
    {
        string marker = $"[Hediff_{id}]";
        int start = report.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {marker} in report:\n{report}");
        int end = report.IndexOf("[Hediff_", start + marker.Length, StringComparison.Ordinal);
        return end < 0 ? report[start..] : report[start..end];
    }

    private static void MakeUnreadable(ContributionHediff effect, string error)
    {
        // Inject the already-tested persistence failure state without loading a whole native graph.
        var storage = (ContributionMetadataScribe)typeof(ContributionHediff)
            .GetField("_metadataScribe", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(effect)!;
        typeof(ContributionMetadataScribe).GetProperty(nameof(ContributionMetadataScribe.Metadata))!
            .SetValue(storage, ContributionMetadata.Unavailable(error));
    }

    private static ModContentPack DefinitionProvider(string packageId)
    {
        // Read-only fixture: do not mount a mod or initialize the native content system.
        var pack = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
        typeof(ModContentPack).GetField("packageIdInt", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pack, packageId);
        typeof(ModContentPack).GetField("rootDirInt", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pack, new DirectoryInfo(Path.Combine(Path.GetTempPath(), "private-installation-path")));
        return pack;
    }

    private sealed class HealthFixture
    {
        internal Pawn Pawn { get; } = HealthTestPawns.CreateForGuardChecks();
        internal BodyPartRecord Left { get; }
        internal BodyPartRecord Right { get; }

        internal HealthFixture()
        {
            var hand = new BodyPartDef { defName = "Hand" };
            var body = new BodyDef { defName = "DiagnosticBody" };
            body.corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Torso" } };
            Left = new BodyPartRecord { def = hand, customLabel = "left hand" };
            Right = new BodyPartRecord { def = hand, customLabel = "right hand" };
            body.corePart.parts.Add(Left);
            body.corePart.parts.Add(Right);
            body.ResolveReferences();
            Pawn.def.defName = "DiagnosticPawn";
            Pawn.def.race.body = body;
            Pawn.thingIDNumber = 71;
        }

        internal ContributionHediff Add(int id, ContributionId? identity = null, BodyPartRecord? part = null) =>
            Add<ContributionHediff>(id, identity, part);

        internal T Add<T>(int id, ContributionId? identity = null, BodyPartRecord? part = null)
            where T : ContributionHediff, new()
        {
            var effect = new T
            {
                pawn = Pawn, Part = part, loadID = id, comps = [],
                def = new HediffDef { defName = "DiagnosticEffect", hediffClass = typeof(T) }
            };
            if (identity is not null) effect.Metadata.AssignIdentity(identity);
            Pawn.health.hediffSet.hediffs.Add(effect);
            return effect;
        }
    }

    private sealed class HostileContribution : ContributionHediff, IContribution
    {
        public HostileContribution() { }

        internal int HostileGetterCalls { get; private set; }

        ContributionMetadata IContribution.Metadata
        {
            get
            {
                HostileGetterCalls++;
                _ = Rand.Int;
                throw new InvalidOperationException("Do not invoke a provider's reimplemented metadata getter.");
            }
        }

        public override string Label => throw new InvalidOperationException("Do not read virtual labels.");
        public override string LabelBase => throw new InvalidOperationException("Do not read virtual labels.");
        public override string Description => throw new InvalidOperationException("Do not read virtual descriptions.");
        public override string ToString() => throw new InvalidOperationException("Do not format provider objects.");
    }
}
