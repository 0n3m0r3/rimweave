using System.Reflection;
using RimWeave.Tests.Helpers;
using RimWorld;
using Xunit;
using Verse;

namespace RimWeave.Tests.Equipment;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeApparelLockTests
{
    [Fact]
    public void RepeatedNativeLockRequestsDoNotCreateIndependentClaims()
    {
        var tracker = new Pawn_ApparelTracker(HealthTestPawns.CreateForGuardChecks());
        var item = new Apparel();
        tracker.Lock(item);
        tracker.Lock(item);
        Assert.True(tracker.IsLocked(item));
        Assert.Single(tracker.LockedApparel);

        tracker.Unlock(item);

        Assert.False(tracker.IsLocked(item));
        Assert.Empty(tracker.LockedApparel);
    }

    [Fact]
    public void UnlockingOneInstancePreservesAnotherWithTheSameDefinition()
    {
        var tracker = new Pawn_ApparelTracker(HealthTestPawns.CreateForGuardChecks());
        var definition = new ThingDef();
        var first = new Apparel { def = definition };
        var second = new Apparel { def = definition };
        tracker.Lock(first);
        tracker.Lock(second);

        tracker.Unlock(first);

        Assert.False(tracker.IsLocked(first));
        Assert.True(tracker.IsLocked(second));
        Assert.Same(second, Assert.Single(tracker.LockedApparel));
    }

    [Fact]
    public void ReadingTheNativeFlagIsPassiveAndDoesNotEstablishWearing()
    {
        // Lock-list operations on an isolated tracker are not wear/remove or job integration.
        var tracker = new Pawn_ApparelTracker(HealthTestPawns.CreateForGuardChecks());
        var item = new Apparel();
        tracker.Lock(item);
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);
        for (int i = 0; i < 3; i++) Assert.True(tracker.IsLocked(item));
        Assert.Null(item.Wearer);
        Assert.Empty(tracker.WornApparel);
        Assert.Same(item, Assert.Single(tracker.LockedApparel));
        Assert.Equal(before, random.GetValue(null));
    }
}
