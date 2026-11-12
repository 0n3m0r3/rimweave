using System.Text;
using System.Xml.Linq;
using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class ContributionPersistenceTests
{
    [Fact]
    public void HealthRoundTrip_PreservesNativeStateAndExactScopeWithoutAddingEffects()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new HealthDefinitions();
        var firstPawn = new HealthSnapshot(definitions.Body);
        var secondPawn = new HealthSnapshot(definitions.Body);
        var identity = new ContributionId("example.owner", "marker");
        var left = Add(firstPawn, definitions, 101, definitions.Left, identity);
        var right = Add(firstPawn, definitions, 102, definitions.Right, identity);
        var wholeBody = Add(firstPawn, definitions, 103, null, identity);
        Add(secondPawn, definitions, 104, definitions.Left, identity);
        var origin = new ContributionOrigin(new ContributionOriginType("example.cause", "Surgery"), "past:event/7");
        left.Metadata.DescribeOrigin(origin);
        right.Metadata.DescribeOrigin(new ContributionOrigin(ContributionOriginType.Unknown));
        wholeBody.Metadata.ClaimExclusiveResponsibility("example/whole-body");
        var secondXml = scribe.Save(secondPawn);

        var loaded = scribe.RoundTrip(firstPawn, definitions.Body);
        loaded = scribe.RoundTrip(loaded, definitions.Body);
        var otherLoaded = scribe.Load<HealthSnapshot>(secondXml, definitions.Body);

        Assert.Equal(3, loaded.Effects.hediffs.Count);
        Assert.Single(otherLoaded.Effects.hediffs);
        var loadedLeft = Assert.Single(ContributionQueries.Resolve(loaded.Pawn, identity, definitions.Left).Matches);
        var loadedRight = Assert.Single(ContributionQueries.Resolve(loaded.Pawn, identity, definitions.Right).Matches);
        var loadedWholeBody = Assert.Single(ContributionQueries.Resolve(loaded.Pawn, identity).Matches);
        Assert.Equal(101, loadedLeft.loadID);
        Assert.Equal(102, loadedRight.loadID);
        Assert.Equal(103, loadedWholeBody.loadID);
        Assert.Equal(104, Assert.Single(ContributionQueries.Resolve(otherLoaded.Pawn, identity, definitions.Left).Matches).loadID);
        Assert.Equal(origin, loadedLeft.Metadata.Origin);
        Assert.Equal(ContributionOriginType.Unknown, loadedRight.Metadata.Origin!.Type);
        Assert.Equal("example/whole-body", loadedWholeBody.Metadata.ExclusiveResponsibility);
        Assert.NotSame(left, loadedLeft);
        Assert.All(loaded.Effects.hediffs, effect =>
        {
            Assert.Same(loaded.Pawn, effect.pawn);
            Assert.Equal(1234, effect.ageTicks);
            Assert.Equal(42, effect.tickAdded);
            Assert.Equal(0.25f, effect.Severity);
            var observer = Assert.IsType<SaveObserver>(Assert.Single(((ContributionHediff)effect).comps));
            Assert.Equal(0, observer.AddCalls);
            Assert.Equal(0, observer.RemoveCalls);
            Assert.Equal(1, observer.PostLoadCalls);
        });
    }

    [Fact]
    public void HealthRoundTrip_PreservesDuplicateIdentitiesAndExclusiveClaims()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new HealthDefinitions();
        var state = new HealthSnapshot(definitions.Body);
        var identity = new ContributionId("example.a", "marker");
        Add(state, definitions, 201, definitions.Left, identity)
            .Metadata.ClaimExclusiveResponsibility("example/claim");
        Add(state, definitions, 202, definitions.Left, identity);
        Add(state, definitions, 203, definitions.Left, new ContributionId("example.b", "marker"))
            .Metadata.ClaimExclusiveResponsibility("example/claim");

        var loaded = scribe.RoundTrip(state, definitions.Body);

        Assert.Equal(ContributionLookupStatus.Ambiguous,
            ContributionQueries.Resolve(loaded.Pawn, identity, definitions.Left).Status);
        Assert.Equal(ContributionLookupStatus.Ambiguous,
            ContributionQueries.ResolveExclusiveClaim(loaded.Pawn, "example/claim", definitions.Left).Status);
        var removal = ContributionOperations.Remove(loaded.Pawn, identity, definitions.Left);
        Assert.Null(removal.Removal);
        Assert.Equal(3, loaded.Effects.hediffs.Count);
    }

    [Fact]
    public void LegacyHealthSave_WithoutMetadataKeepsNativeEffectUnattributed()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new HealthDefinitions();
        var state = new HealthSnapshot(definitions.Body);
        Add(state, definitions, 301, definitions.Left, new ContributionId("example.owner", "marker"));
        var xml = XDocument.Parse(scribe.Save(state));
        xml.Descendants("rimweaveContribution").Remove();
        definitions.Effect.modExtensions = [XmlDeclaration()];

        var loaded = scribe.Load<HealthSnapshot>(xml.ToString(), definitions.Body);

        var effect = Assert.IsType<ContributionHediff>(Assert.Single(loaded.Effects.hediffs));
        Assert.Same(definitions.Left, effect.Part);
        Assert.Equal(301, effect.loadID);
        Assert.Null(effect.Metadata.Identity);
        Assert.Null(effect.Metadata.Origin);
        Assert.Null(effect.Metadata.LoadError);
    }

    [Fact]
    public void UnreadableCandidate_BlocksFalseUniqueResultsOnlyInItsNativeScope()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new HealthDefinitions();
        var state = new HealthSnapshot(definitions.Body);
        var identity = new ContributionId("example.owner", "marker");
        Add(state, definitions, 501, definitions.Left, identity);
        Add(state, definitions, 502, definitions.Left, identity);
        Add(state, definitions, 503, definitions.Right, identity);
        var xml = XDocument.Parse(scribe.Save(state));
        xml.Descendants("rimweaveContribution").First().Element("version")!.Value = "unsupported";

        var loaded = scribe.Load<HealthSnapshot>(xml.ToString(), definitions.Body);
        var result = ContributionQueries.Resolve(loaded.Pawn, identity, definitions.Left);

        Assert.Equal(ContributionLookupStatus.InvalidMetadata, result.Status);
        Assert.Equal(501, Assert.Single(result.InvalidCandidates).loadID);
        Assert.Equal(502, Assert.Single(result.Matches).loadID);
        Assert.Throws<InvalidOperationException>(() =>
            ContributionQueries.Find(loaded.Pawn, identity, definitions.Left));
        Assert.Equal(ContributionLookupStatus.InvalidMetadata,
            ContributionQueries.ResolveExclusiveClaim(loaded.Pawn, "example/claim", definitions.Left).Status);
        Assert.Null(ContributionOperations.Remove(loaded.Pawn, identity, definitions.Left).Removal);
        Assert.Equal(503, Assert.Single(ContributionQueries.Find(loaded.Pawn, identity, definitions.Right)).loadID);
        Assert.Equal(3, loaded.Effects.hediffs.Count);
        Assert.All(loaded.Effects.hediffs.Cast<ContributionHediff>(), effect =>
            Assert.Equal(0, Assert.IsType<SaveObserver>(Assert.Single(effect.comps)).RemoveCalls));
    }

    [Theory]
    [InlineData("<version>1</version><ownerId>example.owner</ownerId>")]
    [InlineData("<version>2</version><futureField flag='yes'>retained</futureField>")]
    public void InvalidMetadata_PreservesNativeEffectAndRemainsUnavailableAfterAnotherSave(string fields)
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new HealthDefinitions();
        var state = new HealthSnapshot(definitions.Body);
        var identity = new ContributionId("example.owner", "marker");
        Add(state, definitions, 401, definitions.Left, identity);
        var xml = XDocument.Parse(scribe.Save(state));
        var damaged = XElement.Parse("<rimweaveContribution>" + fields + "</rimweaveContribution>");
        Assert.Single(xml.Descendants("rimweaveContribution")).ReplaceWith(damaged);
        definitions.Effect.modExtensions = [XmlDeclaration()];

        var loaded = scribe.Load<HealthSnapshot>(xml.ToString(), definitions.Body);
        AssertUnavailable(loaded, definitions, identity);
        var preserved = XDocument.Parse(scribe.Save(loaded));
        var retained = Encoding.Unicode.GetString(Convert.FromBase64String(
            Assert.Single(preserved.Descendants("unreadableXml")).Value));
        Assert.True(XNode.DeepEquals(damaged, XElement.Parse(retained)));

        var reloaded = scribe.Load<HealthSnapshot>(preserved.ToString(), definitions.Body);
        AssertUnavailable(reloaded, definitions, identity);
    }

    [Fact]
    public void Reload_AfterXmlChanges_PreservesSavedAttributionWithoutApplyingNewDefaults()
    {
        using var scribe = new ScribeTestScope();
        using var definitions = new HealthDefinitions();
        var state = new HealthSnapshot(definitions.Body);
        var savedIdentity = new ContributionId("saved.owner", "existing");
        var effect = Add(state, definitions, 601, definitions.Left, savedIdentity);
        effect.Metadata.ClaimExclusiveResponsibility("saved/claim");
        var xml = scribe.Save(state);
        definitions.Effect.modExtensions = [XmlDeclaration()];

        var loaded = scribe.Load<HealthSnapshot>(xml, definitions.Body);

        var restored = Assert.IsType<ContributionHediff>(Assert.Single(loaded.Effects.hediffs));
        Assert.Equal(savedIdentity, restored.Metadata.Identity);
        Assert.Equal("saved/claim", restored.Metadata.ExclusiveResponsibility);
        Assert.Equal(601, restored.loadID);
        Assert.Same(definitions.Left, restored.Part);
        Assert.Equal(0, Assert.IsType<SaveObserver>(Assert.Single(restored.comps)).AddCalls);
        Assert.Equal(new ContributionId("xml.owner", "new"), ContributionDefExtension.Read(definitions.Effect)!.Identity);
    }

    private static ContributionDefExtension XmlDeclaration() => new()
    {
        ownerId = "xml.owner", localId = "new", exclusiveResponsibility = "xml/claim"
    };

    private static void AssertUnavailable(HealthSnapshot state, HealthDefinitions definitions, ContributionId identity)
    {
        var effect = Assert.IsType<ContributionHediff>(Assert.Single(state.Effects.hediffs));
        Assert.Equal(401, effect.loadID);
        Assert.Equal(1234, effect.ageTicks);
        Assert.Same(definitions.Left, effect.Part);
        Assert.NotNull(effect.Metadata.LoadError);
        Assert.Null(effect.Metadata.Identity);
        var removal = ContributionOperations.Remove(state.Pawn, identity, definitions.Left);
        Assert.Equal(ContributionLookupStatus.InvalidMetadata, removal.Lookup.Status);
        Assert.Null(removal.Removal);
        Assert.Single(state.Effects.hediffs);
        Assert.Throws<InvalidOperationException>(() => effect.Metadata.AssignIdentity(identity));
        var observer = Assert.IsType<SaveObserver>(Assert.Single(effect.comps));
        Assert.Equal(0, observer.AddCalls);
        Assert.Equal(0, observer.RemoveCalls);
    }

    private static ContributionHediff Add(HealthSnapshot state, HealthDefinitions definitions,
        int loadId, BodyPartRecord? part, ContributionId identity)
    {
        // Populate a controlled native save graph; this is not a successful gameplay-add test.
        var effect = new ContributionHediff
        {
            def = definitions.Effect,
            pawn = state.Pawn,
            Part = part,
            loadID = loadId,
            ageTicks = 1234,
            tickAdded = 42,
            Severity = 0.25f
        };
        effect.Metadata.AssignIdentity(identity);
        effect.comps = [new SaveObserver { parent = effect, props = definitions.Effect.comps[0] }];
        state.Effects.hediffs.Add(effect);
        return effect;
    }

    public sealed class HealthSnapshot : IExposable
    {
        public Pawn Pawn { get; }
        public HediffSet Effects;

        public HealthSnapshot(BodyDef body)
        {
            Pawn = new Pawn { def = new ThingDef { race = new RaceProperties { body = body } } };
            Pawn.health = new Pawn_HealthTracker(Pawn);
            Effects = Pawn.health.hediffSet;
        }

        public void ExposeData()
        {
            // Exercise the native set, effect, body-part and component load paths without loading a colony.
            Scribe_Deep.Look(ref Effects, "health", Pawn);
            Pawn.health.hediffSet = Effects;
        }
    }

    public sealed class SaveObserver : HediffComp
    {
        public int AddCalls;
        public int RemoveCalls;
        public int PostLoadCalls;
        public override void CompPostPostAdd(DamageInfo? dinfo) => AddCalls++;
        public override void CompPostPostRemoved() => RemoveCalls++;
        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit) PostLoadCalls++;
        }
    }

    private sealed class HealthDefinitions : IDisposable
    {
        private readonly BodyDef[] _previousBodies = DefDatabase<BodyDef>.AllDefs.ToArray();
        private readonly HediffDef[] _previousEffects = DefDatabase<HediffDef>.AllDefs.ToArray();
        internal BodyDef Body { get; } = new() { defName = "RimWeaveSaveTestBody" };
        internal BodyPartRecord Left { get; }
        internal BodyPartRecord Right { get; }
        internal HediffDef Effect { get; } = new()
        {
            defName = "RimWeaveSaveTestEffect",
            hediffClass = typeof(ContributionHediff),
            comps = [new HediffCompProperties { compClass = typeof(SaveObserver) }]
        };

        internal HealthDefinitions()
        {
            var hand = new BodyPartDef { defName = "RimWeaveSaveTestHand" };
            Body.corePart = new BodyPartRecord { def = new BodyPartDef { defName = "RimWeaveSaveTestTorso" } };
            Left = new BodyPartRecord { def = hand };
            Right = new BodyPartRecord { def = hand };
            Body.corePart.parts.Add(Left);
            Body.corePart.parts.Add(Right);
            Body.ResolveReferences();
            DefDatabase<BodyDef>.Add(Body);
            DefDatabase<HediffDef>.Add(Effect);
        }

        public void Dispose()
        {
            DefDatabase<BodyDef>.Clear();
            DefDatabase<BodyDef>.Add(_previousBodies);
            DefDatabase<HediffDef>.Clear();
            DefDatabase<HediffDef>.Add(_previousEffects);
        }
    }
}
