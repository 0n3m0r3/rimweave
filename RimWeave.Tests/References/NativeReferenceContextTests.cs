using System.Runtime.CompilerServices;
using RimWeave.References;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.References;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeReferenceContextTests : IDisposable
{
    private readonly NativeGameTestScope _game = new();

    public void Dispose() => _game.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedHolder_ReportsImmediateOwnerAndRootMapWithoutDirectSpawning(bool targetsEffect)
    {
        var pawn = NativeReferenceTests.CreatePawn(1);
        var effect = NativeReferenceTests.AddEffect(pawn, 1);
        var map = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        var outer = new Holder { ParentHolder = map };
        var inner = new Holder { ParentHolder = outer };
        inner.AttachFixture(pawn);
        using var reference = targetsEffect ? new NativeReference(effect) : new NativeReference(pawn);

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Resolved, snapshot.Resolution);
        Assert.Same(pawn, snapshot.Pawn);
        Assert.Same(targetsEffect ? effect : null, snapshot.Effect);
        Assert.Same(inner, snapshot.ParentHolder);
        Assert.Same(map, snapshot.MapHeld);
        Assert.Null(snapshot.Map);
        Assert.False(snapshot.Spawned);
        Assert.Same(pawn, Assert.Single(inner.Contents));
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        Assert.False(reference.IsTracking);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Fact]
    public void HolderContext_IsReevaluatedWithoutInventingMovementNotifications()
    {
        var pawn = NativeReferenceTests.CreatePawn(2);
        var map = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        var holder = new Holder { ParentHolder = map };
        holder.AttachFixture(pawn);
        using var reference = new NativeReference(pawn);
        reference.StartTracking(new ReferenceTrackingSession());
        var onMap = reference.Capture();

        holder.ParentHolder = null;
        var offMap = reference.Capture();
        holder.Contents.InnerListForReading.Clear();
        pawn.holdingOwner = null;
        var unheld = reference.Capture();

        Assert.Same(map, onMap.MapHeld);
        Assert.Same(holder, onMap.ParentHolder);
        Assert.Same(holder, offMap.ParentHolder);
        Assert.Null(offMap.MapHeld);
        Assert.Null(unheld.ParentHolder);
        Assert.Null(unheld.MapHeld);
        Assert.Equal(NativeReferenceResolution.Resolved, unheld.Resolution);
        Assert.True(reference.IsTracking);
        Assert.Equal(ReferenceTrackingCoverage.Maps, reference.TrackingCoverage);
        Assert.Equal(ReferenceChanges.None, reference.PendingChanges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidReference_DoesNotExposeItsFormerHolderContext(bool differentGame)
    {
        var pawn = NativeReferenceTests.CreatePawn(3);
        var effect = NativeReferenceTests.AddEffect(pawn, 2);
        var holder = new Holder { ParentHolder = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map)) };
        holder.AttachFixture(pawn);
        using var reference = new NativeReference(effect);
        if (differentGame)
            Current.Game = null;
        else
            effect.pawn = NativeReferenceTests.CreatePawn(4);

        var snapshot = reference.Capture();

        Assert.Equal(differentGame ? NativeReferenceResolution.DifferentGame : NativeReferenceResolution.Mismatch,
            snapshot.Resolution);
        Assert.Null(snapshot.ParentHolder);
        Assert.Null(snapshot.MapHeld);
        Assert.Null(snapshot.Map);
        Assert.Null(snapshot.Pawn);
    }

    [Fact]
    public void UnresolvedReference_HasNoInventedHolderContext()
    {
        using var reference = new NativeReference();

        var snapshot = reference.Capture();

        Assert.Equal(NativeReferenceResolution.Unresolved, snapshot.Resolution);
        Assert.Null(snapshot.ParentHolder);
        Assert.Null(snapshot.MapHeld);
    }

    private sealed class Holder : IThingHolder
    {
        internal ThingOwner<Thing> Contents { get; }
        public IThingHolder? ParentHolder { get; set; }

        internal Holder() => Contents = new ThingOwner<Thing>(this);

        internal void AttachFixture(Pawn pawn)
        {
            // Construct native storage for passive reads; live entry/exit requires a game scenario.
            Contents.InnerListForReading.Add(pawn);
            pawn.holdingOwner = Contents;
        }

        public ThingOwner GetDirectlyHeldThings() => Contents;
        public void GetChildHolders(List<IThingHolder> outChildren) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, Contents);
    }
}
