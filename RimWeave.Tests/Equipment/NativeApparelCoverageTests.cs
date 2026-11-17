using System.Reflection;
using RimWeave.Tests.Helpers;
using RimWorld;
using Verse;
using Xunit;

namespace RimWeave.Tests.Equipment;

[Collection(ScribeTestCollection.Name)]
public sealed class NativeApparelCoverageTests
{
    [Fact]
    public void CoverageUsesNativeGroupIdentityAcrossRepeatedParts()
    {
        var hands = new BodyPartGroupDef { defName = "Hands" };
        var sameName = new BodyPartGroupDef { defName = "Hands" };
        var properties = new ApparelProperties { bodyPartGroups = [hands] };
        var definition = new BodyPartDef();
        var left = new BodyPartRecord { def = definition, groups = [hands] };
        var right = new BodyPartRecord { def = definition, groups = [hands] };
        var unrelated = new BodyPartRecord { def = definition, groups = [sameName] };

        Assert.True(properties.CoversBodyPart(left));
        Assert.True(properties.CoversBodyPart(right));
        Assert.False(properties.CoversBodyPart(unrelated));
        Assert.False(properties.CoversBodyPart(new BodyPartRecord { groups = [] }));
    }

    [Fact]
    public void GraphicPathDoesNotDeclareCoverageAndRepeatedReadsStayPassive()
    {
        var group = new BodyPartGroupDef();
        var part = new BodyPartRecord { groups = [group] };
        var properties = new ApparelProperties { bodyPartGroups = [group], wornGraphicPath = "Visible/Example" };
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);
        Assert.True(properties.CoversBodyPart(part));
        properties.wornGraphicPath = "";
        for (int i = 0; i < 3; i++) Assert.True(properties.CoversBodyPart(part));
        Assert.Same(group, Assert.Single(properties.bodyPartGroups));
        Assert.Same(group, Assert.Single(part.groups));
        Assert.Equal(before, random.GetValue(null));
        properties.bodyPartGroups.Clear();
        properties.wornGraphicPath = "Visible/Example";
        Assert.False(properties.CoversBodyPart(part));
    }

    [Fact]
    public void WearerDistinguishesTheApparelHolderFromInventoryAndNoHolder()
    {
        // Supply holder relationships only; this is not native wear/remove integration.
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var apparel = new Apparel();
        Assert.Null(apparel.Wearer);
        var inventory = new Pawn_InventoryTracker(pawn);
        apparel.holdingOwner = inventory.GetDirectlyHeldThings();
        Assert.Null(apparel.Wearer);
        var tracker = new Pawn_ApparelTracker(pawn);
        apparel.holdingOwner = tracker.GetDirectlyHeldThings();
        var owner = apparel.holdingOwner;
        for (int i = 0; i < 3; i++) Assert.Same(pawn, apparel.Wearer);
        Assert.Same(owner, apparel.holdingOwner);
        apparel.holdingOwner = null;
        Assert.Null(apparel.Wearer);
    }
}
