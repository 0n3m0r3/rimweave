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

    [Theory]
    [InlineData(typeof(AbstractContribution))]
    [InlineData(typeof(GenericContribution<>))]
    public void UncreatableClasses_AreDiagnosedBeforeInstantiation(Type effectType)
    {
        var definition = Definition();
        definition.hediffClass = effectType;
        var before = RandState();

        var error = Assert.Throws<InvalidOperationException>(() => ContributionDefExtension.Read(definition));

        Assert.Contains("concrete, closed type", error.Message);
        Assert.Contains("DeclarationTest", error.Message);
        Assert.Contains("example.provider", error.Message);
        Assert.Equal(error.Message, Assert.Single(definition.modExtensions[0].ConfigErrors()));
        Assert.Equal(before, RandState());
    }

    [Theory]
    [InlineData(typeof(PrivateConstructorContribution))]
    [InlineData(typeof(ParameterizedContribution))]
    public void MissingPublicDefaultConstructor_IsDiagnosedBeforeNativeCreation(Type effectType)
    {
        var definition = Definition();
        definition.hediffClass = effectType;
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var before = RandState();

        Assert.Throws<MissingMethodException>(() => HediffMaker.MakeHediff(definition, pawn));
        var error = Assert.Throws<InvalidOperationException>(() => ContributionDefExtension.Read(definition));

        Assert.Contains("public parameterless constructor", error.Message);
        Assert.Contains("DeclarationTest", error.Message);
        Assert.Contains("example.provider", error.Message);
        Assert.Equal(error.Message, Assert.Single(definition.modExtensions[0].ConfigErrors()));
        Assert.Equal(before, RandState());
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    [Theory]
    [InlineData(typeof(ContributionHediff))]
    [InlineData(typeof(DerivedContribution))]
    [InlineData(typeof(GenericContribution<int>))]
    [InlineData(typeof(ThrowingConstructorContribution))]
    public void ConcreteClasses_AreInspectedWithoutExecutingConstructors(Type effectType)
    {
        var definition = Definition();
        definition.hediffClass = effectType;
        var before = RandState();

        Assert.NotNull(ContributionDefExtension.Read(definition));
        Assert.Empty(definition.modExtensions[0].ConfigErrors());
        Assert.Equal(before, RandState());
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCreation_AfterDeclarationChange_KeepsEarlierInstancesIndependent(bool removeDeclaration)
    {
        using var game = new NativeGameTestScope();
        game.Game.uniqueIDsManager = new UniqueIDsManager();
        var definition = Definition();
        definition.comps = [new HediffCompProperties { compClass = typeof(CreationObserver) }];
        var original = (ContributionDefExtension)definition.modExtensions[0];
        original.exclusiveResponsibility = "example/old-claim";
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var first = Assert.IsType<ContributionHediff>(HediffMaker.MakeHediff(definition, pawn));
        var firstIdentity = first.Metadata.Identity;
        var origin = new ContributionOrigin(new ContributionOriginType("example.owner", "creation"));
        first.Metadata.DescribeOrigin(origin);
        original.localId = "Next";
        original.exclusiveResponsibility = "example/new-claim";
        if (removeDeclaration) definition.modExtensions.Clear();

        var second = Assert.IsType<ContributionHediff>(HediffMaker.MakeHediff(definition, pawn));

        Assert.Equal(firstIdentity, first.Metadata.Identity);
        Assert.Equal("example/old-claim", first.Metadata.ExclusiveResponsibility);
        Assert.Equal(origin, first.Metadata.Origin);
        Assert.NotEqual(first.loadID, second.loadID);
        Assert.NotSame(first.Metadata, second.Metadata);
        Assert.Null(second.Metadata.Origin);
        var observer = Assert.IsType<CreationObserver>(Assert.Single(second.comps));
        if (removeDeclaration)
        {
            Assert.Null(second.Metadata.Identity);
            Assert.Null(second.Metadata.ExclusiveResponsibility);
            Assert.Null(observer.IdentityAtCreation);
            Assert.Null(observer.ClaimAtCreation);
        }
        else
        {
            Assert.Equal(new ContributionId("example.owner", "Next"), second.Metadata.Identity);
            Assert.Equal("example/new-claim", second.Metadata.ExclusiveResponsibility);
            Assert.Equal(second.Metadata.Identity, observer.IdentityAtCreation);
            Assert.Equal(second.Metadata.ExclusiveResponsibility, observer.ClaimAtCreation);
        }
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingCreation_PreservesExistingMetadataBeforeNativeCallbacks(bool conflictingIdentity)
    {
        var definition = Definition();
        ((ContributionDefExtension)definition.modExtensions[0]).exclusiveResponsibility = "xml/claim";
        definition.comps = [new HediffCompProperties { compClass = typeof(CreationObserver) }];
        var effect = new ContributionHediff { def = definition, pawn = HealthTestPawns.CreateForGuardChecks() };
        effect.Metadata.ClaimExclusiveResponsibility(conflictingIdentity ? "xml/claim" : "code/claim");
        if (conflictingIdentity) effect.Metadata.AssignIdentity(new ContributionId("code.owner", "existing"));
        var identity = effect.Metadata.Identity;
        var claim = effect.Metadata.ExclusiveResponsibility;
        var origin = new ContributionOrigin(ContributionOriginType.Unknown, "code:source");
        effect.Metadata.DescribeOrigin(origin);

        Assert.Throws<InvalidOperationException>(effect.PostMake);

        Assert.Same(identity, effect.Metadata.Identity);
        Assert.Equal(claim, effect.Metadata.ExclusiveResponsibility);
        Assert.Same(origin, effect.Metadata.Origin);
        Assert.Null(effect.Metadata.LoadError);
        Assert.True(effect.comps is null || effect.comps.Count == 0);
        Assert.Empty(effect.pawn.health.hediffSet.hediffs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("code/claim")]
    public void NativeCreation_PreservesCompatibleConstructorAttribution(string? declaredClaim)
    {
        using var game = new NativeGameTestScope();
        game.Game.uniqueIDsManager = new UniqueIDsManager();
        var definition = Definition();
        definition.hediffClass = typeof(PreclaimedContribution);
        definition.comps = [new HediffCompProperties { compClass = typeof(CreationObserver) }];
        ((ContributionDefExtension)definition.modExtensions[0]).exclusiveResponsibility = declaredClaim;
        var pawn = HealthTestPawns.CreateForGuardChecks();

        var effect = Assert.IsType<PreclaimedContribution>(HediffMaker.MakeHediff(definition, pawn));

        Assert.Equal(new ContributionId("example.owner", "Marker"), effect.Metadata.Identity);
        Assert.Equal("code/claim", effect.Metadata.ExclusiveResponsibility);
        Assert.Null(effect.Metadata.Origin);
        var observer = Assert.IsType<CreationObserver>(Assert.Single(effect.comps));
        Assert.Equal(effect.Metadata.Identity, observer.IdentityAtCreation);
        Assert.Equal("code/claim", observer.ClaimAtCreation);
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

    public abstract class AbstractContribution : ContributionHediff;
    public sealed class GenericContribution<T> : ContributionHediff;
    public sealed class ThrowingConstructorContribution : ContributionHediff
    {
        public ThrowingConstructorContribution() => throw new InvalidOperationException("Inspection must not instantiate this type.");
    }

    public sealed class PrivateConstructorContribution : ContributionHediff
    {
        private PrivateConstructorContribution() => throw new InvalidOperationException("Must not run.");
    }

    public sealed class ParameterizedContribution : ContributionHediff
    {
        public ParameterizedContribution(int value) => throw new InvalidOperationException("Must not run.");
    }

    public sealed class DerivedContribution : ContributionHediff;

    public sealed class PreclaimedContribution : ContributionHediff
    {
        public PreclaimedContribution() => Metadata.ClaimExclusiveResponsibility("code/claim");
    }

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
