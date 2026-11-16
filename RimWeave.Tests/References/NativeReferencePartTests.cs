using RimWeave.References;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.References;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeReferencePartTests : IDisposable
{
    private readonly NativeGameTestScope _game = new();

    public void Dispose() => _game.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capture_PreservesTheObservedPartWhenTheExactEffectChangesTarget(bool cooperating)
    {
        var pawn = NativeReferenceTests.CreatePawn(1);
        var definition = new BodyPartDef();
        var first = new BodyPartRecord { def = definition };
        var second = new BodyPartRecord { def = definition };
        Hediff effect = cooperating ? NativeReferenceTests.AddEffect(pawn, 1)
            : new Hediff { pawn = pawn, loadID = 1, def = new HediffDef() };
        if (!cooperating) pawn.health.hediffSet.hediffs.Add(effect);
        effect.Part = first;
        using var reference = new NativeReference(effect);
        reference.StartTracking(new ReferenceTrackingSession());
        var before = reference.Capture();

        // Fixture reassignment only: the adapter observes a native field and performs no retargeting.
        effect.Part = second;
        var after = reference.Capture();

        Assert.Same(first, before.EffectPart);
        Assert.Same(second, after.EffectPart);
        Assert.Same(effect, before.Effect);
        Assert.Same(effect, after.Effect);
        Assert.Same(second, before.Effect!.Part);
        Assert.Same(pawn, after.Pawn);
        Assert.True(after.EffectAttached);
        Assert.True(reference.IsTracking);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void Capture_DetachedEffectKeepsItsDeclaredPartWithoutSelectingAnotherMatchingEffect()
    {
        var pawn = NativeReferenceTests.CreatePawn(2);
        var definition = new BodyPartDef();
        var first = new BodyPartRecord { def = definition };
        var second = new BodyPartRecord { def = definition };
        var intended = NativeReferenceTests.AddEffect(pawn, 1);
        var other = NativeReferenceTests.AddEffect(pawn, 2);
        intended.Part = first;
        other.Part = second;
        using var reference = new NativeReference(intended);
        reference.StartTracking(new ReferenceTrackingSession());
        pawn.health.hediffSet.hediffs.Remove(intended);

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Resolved, snapshot.Resolution);
        Assert.Same(first, snapshot.EffectPart);
        Assert.Same(intended, snapshot.Effect);
        Assert.False(snapshot.EffectAttached);
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.EffectDetached | ReferenceChanges.TrackingInterrupted, reference.PendingChanges);
        Assert.Same(other, Assert.Single(pawn.health.hediffSet.hediffs));
        Assert.Same(second, other.Part);
        Assert.Equal(intended.Metadata.Identity, other.Metadata.Identity);
    }

    [Fact]
    public void Capture_SharedPartRecordRemainsScopedByItsObservedPawn()
    {
        var firstPawn = NativeReferenceTests.CreatePawn(3);
        var secondPawn = NativeReferenceTests.CreatePawn(4);
        var shared = new BodyPartRecord();
        var first = NativeReferenceTests.AddEffect(firstPawn, 1);
        var second = NativeReferenceTests.AddEffect(secondPawn, 2);
        first.Part = shared;
        second.Part = shared;
        using var firstReference = new NativeReference(first);
        using var secondReference = new NativeReference(second);

        var a = firstReference.Capture();
        var b = secondReference.Capture();

        Assert.Same(shared, a.EffectPart);
        Assert.Same(shared, b.EffectPart);
        Assert.Same(firstPawn, a.Pawn);
        Assert.Same(secondPawn, b.Pawn);
        Assert.NotEqual(a.PawnIdentity, b.PawnIdentity);
        Assert.NotSame(a.Effect, b.Effect);
    }

    [Fact]
    public void Capture_NullPartDistinguishesWholeBodyFromPawnOnlyByTheResolvedEffect()
    {
        var pawn = NativeReferenceTests.CreatePawn(5);
        var effect = NativeReferenceTests.AddEffect(pawn, 1);
        using var effectReference = new NativeReference(effect);
        using var pawnReference = new NativeReference(pawn);

        var wholeBody = effectReference.Capture();
        var pawnOnly = pawnReference.Capture();

        Assert.Equal(NativeReferenceResolution.Resolved, wholeBody.Resolution);
        Assert.Null(wholeBody.EffectPart);
        Assert.Same(effect, wholeBody.Effect);
        Assert.True(wholeBody.EffectAttached);
        Assert.Equal(NativeReferenceResolution.Resolved, pawnOnly.Resolution);
        Assert.Null(pawnOnly.EffectPart);
        Assert.Null(pawnOnly.Effect);
        Assert.Null(pawnOnly.EffectAttached);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capture_UnavailableTargetDoesNotLeakItsFormerPart(bool differentGame)
    {
        var pawn = NativeReferenceTests.CreatePawn(6);
        var effect = NativeReferenceTests.AddEffect(pawn, 1);
        var part = new BodyPartRecord();
        effect.Part = part;
        using var reference = new NativeReference(effect);
        var before = reference.Capture();
        if (differentGame) Current.Game = null;
        else effect.pawn = NativeReferenceTests.CreatePawn(7);

        var after = reference.Capture();

        Assert.Same(part, before.EffectPart);
        Assert.Equal(differentGame ? NativeReferenceResolution.DifferentGame : NativeReferenceResolution.Mismatch, after.Resolution);
        Assert.Null(after.EffectPart);
        Assert.Null(after.Effect);
        Assert.Null(after.Pawn);
        Assert.Same(part, effect.Part);
    }

    [Theory]
    [InlineData(LoadSaveMode.LoadingVars)]
    [InlineData(LoadSaveMode.ResolvingCrossRefs)]
    public void Capture_DuringNativeLoadingDoesNotExposeAnAlreadyKnownPart(LoadSaveMode mode)
    {
        var pawn = NativeReferenceTests.CreatePawn(8);
        var effect = NativeReferenceTests.AddEffect(pawn, 1);
        var part = new BodyPartRecord();
        effect.Part = part;
        using var reference = new NativeReference(effect);
        Assert.Same(part, reference.Capture().EffectPart);
        var previousMode = Scribe.mode;
        try
        {
            Scribe.mode = mode;

            var snapshot = reference.Capture();

            Assert.Equal(NativeReferenceResolution.Loading, snapshot.Resolution);
            Assert.Null(snapshot.EffectPart);
            Assert.Null(snapshot.Effect);
            Assert.Same(part, effect.Part);
        }
        finally
        {
            Scribe.mode = previousMode;
        }
        Assert.Same(part, reference.Capture().EffectPart);
    }

    [Fact]
    public void Capture_UnresolvedReferenceInventsNoBodyPart()
    {
        using var reference = new NativeReference();

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Unresolved, snapshot.Resolution);
        Assert.Null(snapshot.EffectPart);
        Assert.Null(snapshot.Effect);
    }
}
