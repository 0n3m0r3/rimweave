using RimWeave.Contributions;
using Xunit;

namespace RimWeave.Tests.Contributions;

public sealed class ContributionIdTests
{
    [Fact]
    public void Equality_NormalizesOwnerCase()
    {
        Assert.Equal(new ContributionId("Example.Mod", "marker"),
            new ContributionId("example.mod", "marker"));
    }

    [Fact]
    public void Equality_KeepsLocalNamesCaseSensitive()
    {
        Assert.NotEqual(new ContributionId("example.mod", "Marker"),
            new ContributionId("example.mod", "marker"));
    }

    [Fact]
    public void Equality_DistinguishesOwnersWithTheSameLocalName()
    {
        Assert.NotEqual(new ContributionId("example.first", "marker"),
            new ContributionId("example.second", "marker"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" example.mod ")]
    public void Constructor_RejectsInvalidOwners(string? owner)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ContributionId(owner!, "marker"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" marker ")]
    public void Constructor_RejectsInvalidLocalNames(string? local)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ContributionId("example.mod", local!));
    }
}
