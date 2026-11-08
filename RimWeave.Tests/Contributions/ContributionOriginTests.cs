using RimWeave.Contributions;
using Xunit;

namespace RimWeave.Tests.Contributions;

public sealed class ContributionOriginTests
{
    private static readonly ContributionOriginType Impact = new("example.combat", "impact");

    [Theory]
    [InlineData("example.combat", "impact", "external.weapons:hit/42")]
    [InlineData("example.disease", "infection", "external.health:wound/9")]
    [InlineData("future.mod", "unseen-cause", "external.events:occurrence/17")]
    public void Origin_PreservesCustomTypesAndOpaqueSources(
        string namespaceId, string localId, string sourceReference)
    {
        var type = new ContributionOriginType(namespaceId, localId);
        var origin = new ContributionOrigin(type, sourceReference);
        var metadata = new ContributionMetadata();

        metadata.DescribeOrigin(origin);

        Assert.Same(type, metadata.Origin!.Type);
        Assert.Equal(sourceReference, metadata.Origin.SourceReference);
        Assert.NotEqual(ContributionOriginType.Unknown, metadata.Origin.Type);
        Assert.Null(metadata.Identity);
    }

    [Fact]
    public void Origin_DoesNotInventAMissingSourceReference()
    {
        var origin = new ContributionOrigin(Impact);

        Assert.Equal(Impact, origin.Type);
        Assert.Null(origin.SourceReference);
    }

    [Fact]
    public void Equality_UsesTheTypeAndCaseSensitiveReference()
    {
        var origin = new ContributionOrigin(Impact, "external.mod:Source_42");

        Assert.Equal(origin, new ContributionOrigin(
            new ContributionOriginType("EXAMPLE.COMBAT", "impact"), "external.mod:Source_42"));
        Assert.NotEqual(origin, new ContributionOrigin(
            new ContributionOriginType("example.combat", "injury"), "external.mod:Source_42"));
        Assert.NotEqual(origin, new ContributionOrigin(Impact, "external.mod:source_42"));
        Assert.Equal("external.mod:Source_42", origin.SourceReference);
    }

    [Fact]
    public void Origin_RejectsNullTypeInsteadOfInventingOne()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new ContributionOrigin(null!));

        Assert.Equal("type", error.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(" Source_42")]
    [InlineData("Source_42 ")]
    public void Origin_RejectsInvalidReferences(string reference)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() =>
            new ContributionOrigin(Impact, reference));

        Assert.Equal("sourceReference", error.ParamName);
    }

    [Fact]
    public void Attribution_DoesNotInventAnOrigin()
    {
        var metadata = new ContributionMetadata();
        Assert.Null(metadata.Origin);

        metadata.AssignIdentity(new ContributionId("example.mod", "effect"));
        metadata.ClaimExclusiveResponsibility("example/property");

        Assert.Null(metadata.Origin);
    }

    [Fact]
    public void Description_CanExplicitlyDeclareAnUnknownOriginWithoutInventingAnOwner()
    {
        var metadata = new ContributionMetadata();
        var origin = new ContributionOrigin(ContributionOriginType.Unknown);

        metadata.DescribeOrigin(origin);

        Assert.Same(origin, metadata.Origin);
        Assert.Null(metadata.Identity);
        Assert.Null(metadata.ExclusiveResponsibility);
    }

    [Fact]
    public void UnknownType_CanKeepASourceReferenceWhenOnlyTheTypeIsUnknown()
    {
        var origin = new ContributionOrigin(ContributionOriginType.Unknown, "external.mod:event/42");

        Assert.Equal(ContributionOriginType.Unknown, origin.Type);
        Assert.Equal("external.mod:event/42", origin.SourceReference);
    }

    [Fact]
    public void Description_AcceptsAnEqualValueAgain()
    {
        var metadata = new ContributionMetadata();
        var origin = new ContributionOrigin(Impact, "external.mod:hit/42");
        metadata.DescribeOrigin(origin);

        metadata.DescribeOrigin(new ContributionOrigin(
            new ContributionOriginType("EXAMPLE.COMBAT", "impact"), "external.mod:hit/42"));

        Assert.Equal(origin, metadata.Origin);
    }

    [Fact]
    public void Description_RejectsNullWithoutChangingTheOrigin()
    {
        var metadata = new ContributionMetadata();
        Assert.Throws<ArgumentNullException>(() => metadata.DescribeOrigin(null!));
        Assert.Null(metadata.Origin);

        var origin = new ContributionOrigin(Impact, "external.mod:hit/42");
        metadata.DescribeOrigin(origin);
        Assert.Throws<ArgumentNullException>(() => metadata.DescribeOrigin(null!));
        Assert.Same(origin, metadata.Origin);
    }

    [Theory]
    [InlineData("another.mod", "impact", "external.mod:hit/42")]
    [InlineData("example.combat", "injury", "external.mod:hit/42")]
    [InlineData("example.combat", "impact", "external.mod:hit/43")]
    [InlineData("example.combat", "impact", null)]
    public void Description_RejectsRewritingTheTypeOrReference(
        string namespaceId, string localId, string? reference)
    {
        var metadata = new ContributionMetadata();
        var origin = new ContributionOrigin(Impact, "external.mod:hit/42");
        metadata.DescribeOrigin(origin);

        Assert.Throws<InvalidOperationException>(() =>
            metadata.DescribeOrigin(new ContributionOrigin(
                new ContributionOriginType(namespaceId, localId), reference)));

        Assert.Same(origin, metadata.Origin);
    }

    [Fact]
    public void OneOwner_CanDeclareIndependentOriginsWithoutChangingIdentityOrClaims()
    {
        var injury = new ContributionMetadata();
        var infection = new ContributionMetadata();
        var injuryId = new ContributionId("reaction.mod", "injury-response");
        var infectionId = new ContributionId("reaction.mod", "infection-response");
        injury.AssignIdentity(injuryId);
        infection.AssignIdentity(infectionId);
        injury.ClaimExclusiveResponsibility("example/property");
        infection.ClaimExclusiveResponsibility("example/property");

        injury.DescribeOrigin(new ContributionOrigin(Impact, "external.weapons:hit/42"));
        Assert.Null(infection.Origin);
        var infectionType = new ContributionOriginType("example.disease", "infection");
        infection.DescribeOrigin(new ContributionOrigin(infectionType, "external.health:wound/9"));

        Assert.Same(injuryId, injury.Identity);
        Assert.Same(infectionId, infection.Identity);
        Assert.Equal("example/property", injury.ExclusiveResponsibility);
        Assert.Equal("example/property", infection.ExclusiveResponsibility);
        Assert.Equal(Impact, injury.Origin!.Type);
        Assert.Equal(infectionType, infection.Origin!.Type);
    }
}
