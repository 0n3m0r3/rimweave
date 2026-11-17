using System.Reflection;
using RimWeave.Diagnostics;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Diagnostics;

[Collection(ScribeTestCollection.Name)]
public sealed class BodyReportTests
{
    [Fact]
    public void Create_DistinguishesOccurrencesParentsAndEffectTargets()
    {
        var pawn = Create(3);
        var parts = pawn.RaceProps.body.AllParts;
        pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, Part = parts[1], def = new HediffDef { defName = "Left" } });
        pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, Part = parts[2], def = new HediffDef { defName = "Right" } });
        pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, def = new HediffDef { defName = "Whole" } });
        pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, Part = new BodyPartRecord(), def = new HediffDef { defName = "Foreign" } });

        string report = BodyReport.Create(pawn);

        Assert.Contains("slot=1, parent=0", report);
        Assert.Contains("slot=2, parent=0", report);
        Assert.Contains("hediff=Left, target=slot=1", report);
        Assert.Contains("hediff=Right, target=slot=2", report);
        Assert.Contains("hediff=Whole, target=whole body", report);
        Assert.Contains("hediff=Foreign, target=unresolved part", report);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_ReportsEntryLimitsWithoutChangingNativeLists(bool manyParts)
    {
        var pawn = Create(manyParts ? BodyReport.MaxParts + 1 : 1);
        for (int i = 0; i < BodyReport.MaxEffects + 1; i++)
            pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, def = new HediffDef { defName = "Effect" + i } });
        var before = pawn.health.hediffSet.hediffs.ToArray();

        string report = BodyReport.Create(pawn);

        Assert.Contains("Diagnostic: INCOMPLETE", report);
        Assert.DoesNotContain("hediff=Effect40,", report);
        Assert.DoesNotContain("slot=100,", report);
        Assert.Equal(before, pawn.health.hediffSet.hediffs);
        Assert.Equal(manyParts ? BodyReport.MaxParts + 1 : 1, pawn.RaceProps.body.AllParts.Count);
    }

    [Fact]
    public void Create_EscapesProviderTextAndBoundsFieldsAndReport()
    {
        var pawn = Create(BodyReport.MaxParts);
        string hostile = "<color=red>\nforged\u202E" + new string('x', 300);
        foreach (var part in pawn.RaceProps.body.AllParts)
            part.customLabel = hostile;

        string report = BodyReport.Create(pawn);

        Assert.DoesNotContain("<color", report);
        Assert.DoesNotContain("\nforged", report);
        Assert.DoesNotContain('\u202E', report);
        Assert.Contains("[field truncated]", report);
        Assert.Contains("[REPORT TRUNCATED: incomplete diagnostic", report);
        Assert.Contains("Diagnostic: INCOMPLETE", report);
        Assert.DoesNotContain("Diagnostic: complete", report);
        Assert.InRange(report.Length, 1, DiagnosticText.MaxReportLength);
    }

    [Fact]
    public void Create_IsPassiveAndDoesNotUseEffectPresentationGetters()
    {
        var pawn = Create(3);
        var effect = new HostileEffect { pawn = pawn, def = new HediffDef { defName = "PlainDefinition" } };
        pawn.health.hediffSet.hediffs.Add(effect);
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        ulong before = (ulong)random.GetValue(null)!;
        try
        {
            string first = BodyReport.Create(pawn);
            Assert.Equal(first, BodyReport.Create(pawn));
            Assert.Contains("PlainDefinition", first);
            Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
            Assert.Equal(before, (ulong)random.GetValue(null)!);
        }
        finally { random.SetValue(null, before); }
    }

    [Fact]
    public void Create_RefreshesMissingStateWithoutApplyingAnything()
    {
        var pawn = Create(2);
        var part = pawn.RaceProps.body.AllParts[1];
        Assert.Contains("directMissing=False", BodyReport.Create(pawn));
        var missing = new Hediff_MissingPart { pawn = pawn, Part = part, def = new HediffDef { defName = "Missing" } };
        pawn.health.hediffSet.hediffs.Add(missing);

        Assert.Contains("directMissing=True", BodyReport.Create(pawn));
        Assert.Same(missing, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void Create_ReportsUnavailableStorageAndRejectsNull()
    {
        Assert.Contains("unavailable", BodyReport.Create(new Pawn()));
        Assert.Contains("unavailable", BodyReport.Create(HealthTestPawns.CreateForGuardChecks()));
        var pawn = Create(1);
        pawn.health = null;
        Assert.Contains("unavailable", BodyReport.Create(pawn));
        Assert.Throws<ArgumentNullException>(() => BodyReport.Create(null!));
    }

    private static Pawn Create(int count)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        pawn.thingIDNumber = 81;
        pawn.def.defName = "ReportPawn";
        var body = new BodyDef { defName = "ReportBody" };
        pawn.RaceProps.body = body;
        var root = new BodyPartRecord { def = new BodyPartDef { defName = "Root", label = "root" } };
        body.corePart = root;
        body.AllParts.Add(root);
        var definition = new BodyPartDef { defName = "Limb", label = "limb" };
        for (int i = 1; i < count; i++)
        {
            var part = new BodyPartRecord { def = definition, parent = root };
            root.parts.Add(part);
            body.AllParts.Add(part);
        }
        return pawn;
    }

    private sealed class HostileEffect : Hediff
    {
        public override string Label => throw new InvalidOperationException("Do not read virtual labels.");
        public override string LabelBase => throw new InvalidOperationException("Do not read virtual labels.");
        public override string Description => throw new InvalidOperationException("Do not read virtual descriptions.");
        public override string ToString() => throw new InvalidOperationException("Do not format effects.");
    }
}
