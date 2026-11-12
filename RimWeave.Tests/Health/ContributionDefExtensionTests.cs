using System.Reflection;
using System.Runtime.CompilerServices;
using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using RimWorld;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class ContributionDefExtensionTests
{
    [Fact]
    public void PeerXml_LoadsThroughTheNativeParserAndExposesAnIndependentDeclaration()
    {
        using var definitions = new PeerDefinitionScope();
        var definition = definitions.Definition;
        var extension = Assert.IsType<ContributionDefExtension>(Assert.Single(definition.modExtensions));
        Assert.Empty(extension.ConfigErrors());

        var first = ContributionDefExtension.Read(definition)!;
        var second = ContributionDefExtension.Read(definition)!;
        Assert.Equal(new ContributionId("rimweave.example.peer", "hand-state"), first.Identity);
        Assert.Equal("rimweave.examples/hand-state", first.ExclusiveResponsibility);
        Assert.NotSame(first, second);
        first.DescribeOrigin(new ContributionOrigin(ContributionOriginType.Unknown));
        Assert.Null(second.Origin);
        Assert.Null(ContributionDefExtension.Read(definition)!.Origin);
    }

    [Fact]
    public void Read_UsesSharedNormalizationAndDoesNotCacheMutableDefinitionValues()
    {
        var definition = Definition();
        var declaration = (ContributionDefExtension)definition.modExtensions[0];
        declaration.ownerId = "Example.OWNER";
        var first = ContributionDefExtension.Read(definition)!;
        declaration.localId = "Different";
        var second = ContributionDefExtension.Read(definition)!;

        Assert.Equal(new ContributionId("example.owner", "Marker"), first.Identity);
        Assert.Equal(new ContributionId("example.owner", "Different"), second.Identity);
        Assert.Null(first.ExclusiveResponsibility);
    }

    [Fact]
    public void Read_IsPassiveAndRepeatedDefinitionIdentitiesAreAllowed()
    {
        var first = Definition();
        var second = Definition();
        second.defName = "AnotherDefinition";
        Rand.PushState(1741);
        try
        {
            var before = RandState();
            Assert.Equal(ContributionDefExtension.Read(first)!.Identity, ContributionDefExtension.Read(second)!.Identity);
            Assert.Empty(first.modExtensions[0].ConfigErrors());
            Assert.Equal(before, RandState());
        }
        finally { Rand.PopState(); }
    }

    [Theory]
    [InlineData("owner", null)]
    [InlineData("owner", "")]
    [InlineData("owner", " bad ")]
    [InlineData("local", null)]
    [InlineData("local", " ")]
    [InlineData("local", " bad ")]
    [InlineData("claim", "")]
    [InlineData("claim", " ")]
    [InlineData("claim", " bad ")]
    public void InvalidFields_HaveTheSameContextualDiagnosticDuringInspectionAndConfiguration(string field, string? value)
    {
        var definition = Definition();
        var declaration = (ContributionDefExtension)definition.modExtensions[0];
        if (field == "owner") declaration.ownerId = value!;
        if (field == "local") declaration.localId = value!;
        if (field == "claim") declaration.exclusiveResponsibility = value;

        var error = Assert.Throws<InvalidOperationException>(() => ContributionDefExtension.Read(definition));

        Assert.Contains("DeclarationTest", error.Message);
        Assert.Contains("example.provider", error.Message);
        Assert.Equal(error.Message, Assert.Single(declaration.ConfigErrors()));
    }

    [Fact]
    public void DuplicateDeclarations_AreRejectedEvenWhenIdentical()
    {
        var definition = Definition();
        definition.modExtensions.Add(new ContributionDefExtension { ownerId = "example.owner", localId = "Marker" });
        definition.ResolveReferences();
        var error = Assert.Throws<InvalidOperationException>(() => ContributionDefExtension.Read(definition));
        Assert.Contains("Only one", error.Message);
        Assert.All(definition.modExtensions, extension => Assert.Equal(error.Message, Assert.Single(extension.ConfigErrors())));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompatibleHealthTypes_AreDiagnosedWithoutCreatingAnEffect(bool missingClass)
    {
        var definition = Definition();
        definition.hediffClass = missingClass ? null : typeof(HediffWithComps);
        Assert.Contains("must use ContributionHediff", Assert.Single(definition.modExtensions[0].ConfigErrors()));
        Assert.Throws<InvalidOperationException>(() => ContributionDefExtension.Read(definition));
    }

    [Fact]
    public void NonHealthDefinition_IsDiagnosedByTheNativeExtensionHook()
    {
        var definition = Definition();
        var other = new ThingDef { defName = "WrongDefinition", modContentPack = definition.modContentPack,
            modExtensions = definition.modExtensions };
        other.modExtensions[0].ResolveReferences(other);
        var error = Assert.Single(other.modExtensions[0].ConfigErrors());
        Assert.Contains("WrongDefinition", error);
        Assert.Contains("example.provider", error);
        Assert.Contains("must use ContributionHediff", error);
    }

    [Fact]
    public void NativeCreation_InitializesBeforeComponentsAndDoesNotAttachOrShareMetadata()
    {
        using var game = new NativeGameTestScope();
        game.Game.uniqueIDsManager = new UniqueIDsManager();
        var definition = Definition();
        definition.hediffClass = typeof(DerivedContribution);
        definition.comps = [new HediffCompProperties { compClass = typeof(CreationObserver) }];
        var declaration = (ContributionDefExtension)definition.modExtensions[0];
        declaration.exclusiveResponsibility = "example/claim";
        var pawn = HealthTestPawns.CreateForGuardChecks();

        var first = Assert.IsType<DerivedContribution>(HediffMaker.MakeHediff(definition, pawn));
        var second = Assert.IsType<DerivedContribution>(HediffMaker.MakeHediff(definition, pawn));

        Assert.Equal(new ContributionId("example.owner", "Marker"), first.Metadata.Identity);
        Assert.Equal("example/claim", first.Metadata.ExclusiveResponsibility);
        var observer = Assert.IsType<CreationObserver>(Assert.Single(first.comps));
        Assert.Equal(first.Metadata.Identity, observer.IdentityAtCreation);
        Assert.Equal("example/claim", observer.ClaimAtCreation);
        Assert.NotSame(first.Metadata, second.Metadata);
        first.Metadata.DescribeOrigin(new ContributionOrigin(ContributionOriginType.Unknown));
        Assert.Null(second.Metadata.Origin);
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    [Fact]
    public void InvalidCreation_FailsBeforeNativeCallbacksAndRandomDraws()
    {
        var definition = Definition();
        ((ContributionDefExtension)definition.modExtensions[0]).ownerId = "";
        var effect = new ContributionHediff { def = definition, pawn = HealthTestPawns.CreateForGuardChecks() };
        Rand.PushState(73);
        try
        {
            var before = RandState();
            var error = Assert.Throws<InvalidOperationException>(effect.PostMake);
            Assert.Contains("DeclarationTest", error.Message);
            Assert.Equal(before, RandState());
            Assert.Null(effect.Metadata.Identity);
            Assert.Empty(effect.pawn.health.hediffSet.hediffs);
        }
        finally { Rand.PopState(); }
    }

    [Fact]
    public void WithoutDeclaration_ExistingCodeCanAssignAttributionAfterNativeCreation()
    {
        using var game = new NativeGameTestScope();
        game.Game.uniqueIDsManager = new UniqueIDsManager();
        var definition = Definition();
        definition.modExtensions.Clear();
        Assert.Null(ContributionDefExtension.Read(definition));
        Assert.Throws<ArgumentNullException>(() => ContributionDefExtension.Read(null!));
        var effect = Assert.IsType<ContributionHediff>(HediffMaker.MakeHediff(definition, HealthTestPawns.CreateForGuardChecks()));
        Assert.Null(effect.Metadata.Identity);
        effect.Metadata.AssignIdentity(new ContributionId("code.owner", "legacy"));
        Assert.Equal("code.owner", effect.Metadata.Identity!.OwnerId);
    }

    private static object RandState() => typeof(Rand)
        .GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static HediffDef Definition()
    {
        // Only the mod identity is needed; constructing a content pack mounts game assets.
        var mod = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
        typeof(ModContentPack).GetField("packageIdInt", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mod, "example.provider");
        var definition = new HediffDef
        {
            defName = "DeclarationTest", modContentPack = mod, hediffClass = typeof(ContributionHediff),
            modExtensions = [new ContributionDefExtension { ownerId = "example.owner", localId = "Marker" }]
        };
        definition.ResolveReferences();
        return definition;
    }

    public sealed class DerivedContribution : ContributionHediff;

    public sealed class CreationObserver : HediffComp
    {
        public ContributionId? IdentityAtCreation;
        public string? ClaimAtCreation;
        public override void CompPostMake()
        {
            var metadata = ((ContributionHediff)parent).Metadata;
            IdentityAtCreation = metadata.Identity;
            ClaimAtCreation = metadata.ExclusiveResponsibility;
        }
    }
}
