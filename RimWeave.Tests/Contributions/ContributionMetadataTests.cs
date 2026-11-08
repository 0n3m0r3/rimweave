using RimWeave.Contributions;
using Xunit;

namespace RimWeave.Tests.Contributions;

public sealed class ContributionMetadataTests
{
    [Fact]
    public void NewMetadata_DoesNotInventAttributionOrAClaim()
    {
        var metadata = new ContributionMetadata();
        Assert.Null(metadata.Identity);
        Assert.Null(metadata.ExclusiveResponsibility);
    }

    [Fact]
    public void AssignIdentity_AcceptsTheSameValueAgain()
    {
        var metadata = new ContributionMetadata();
        metadata.AssignIdentity(new ContributionId("example.mod", "first"));
        metadata.AssignIdentity(new ContributionId("EXAMPLE.MOD", "first"));
        Assert.Equal(new ContributionId("example.mod", "first"), metadata.Identity);
    }

    [Fact]
    public void AssignIdentity_RejectsReassignmentAndPreservesTheOriginal()
    {
        var metadata = new ContributionMetadata();
        var original = new ContributionId("example.mod", "first");
        metadata.AssignIdentity(original);

        Assert.Throws<InvalidOperationException>(() =>
            metadata.AssignIdentity(new ContributionId("example.mod", "second")));
        Assert.Same(original, metadata.Identity);
    }

    [Fact]
    public void AssignIdentity_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ContributionMetadata().AssignIdentity(null!));
    }

    [Fact]
    public void Claim_AcceptsTheSameResponsibilityAgain()
    {
        var metadata = new ContributionMetadata();
        metadata.ClaimExclusiveResponsibility("example/property");
        metadata.ClaimExclusiveResponsibility("example/property");
        Assert.Equal("example/property", metadata.ExclusiveResponsibility);
    }

    [Fact]
    public void Claim_RejectsAChangeAndPreservesTheOriginal()
    {
        var metadata = new ContributionMetadata();
        metadata.ClaimExclusiveResponsibility("example/property");

        Assert.Throws<InvalidOperationException>(() =>
            metadata.ClaimExclusiveResponsibility("example/Property"));
        Assert.Equal("example/property", metadata.ExclusiveResponsibility);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" example/property ")]
    public void Claim_RejectsInvalidNamesWithoutChangingMetadata(string? responsibility)
    {
        var metadata = new ContributionMetadata();
        Assert.ThrowsAny<ArgumentException>(() => metadata.ClaimExclusiveResponsibility(responsibility!));
        Assert.Null(metadata.ExclusiveResponsibility);
    }
}
