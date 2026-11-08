using RimWeave.Contributions;
using Xunit;

namespace RimWeave.Tests.Contributions;

public sealed class ContributionOriginTypeTests
{
    [Theory]
    [InlineData("example.combat", "impact")]
    [InlineData("example.disease", "infection")]
    [InlineData("future.mod", "previously-unseen-cause")]
    public void Constructor_AcceptsCustomTypesWithoutRegistration(string namespaceId, string localId)
    {
        var type = new ContributionOriginType(namespaceId, localId);

        Assert.Equal(namespaceId, type.NamespaceId);
        Assert.Equal(localId, type.LocalId);
        Assert.NotEqual(ContributionOriginType.Unknown, type);
    }

    [Fact]
    public void Equality_NormalizesOnlyTheNamespace()
    {
        var type = new ContributionOriginType("EXAMPLE.COMBAT", "impact");
        var same = new ContributionOriginType("example.combat", "impact");
        var otherKey = new ContributionOriginType("example.combat", "Impact");

        Assert.Equal("example.combat", type.NamespaceId);
        Assert.Equal(type, same);
        Assert.NotEqual(type, otherKey);
        Assert.Equal(2, new HashSet<ContributionOriginType> { type, same, otherKey }.Count);
    }

    [Fact]
    public void Equality_DistinguishesNamespacesWithTheSameLocalName()
    {
        Assert.NotEqual(
            new ContributionOriginType("example.first", "impact"),
            new ContributionOriginType("example.second", "impact"));
    }

    [Fact]
    public void Unknown_IsAnExplicitStableIdentifier()
    {
        Assert.Equal(new ContributionOriginType("rimweave.framework", "unknown"),
            ContributionOriginType.Unknown);
        Assert.NotEqual(new ContributionOriginType("another.mod", "unknown"),
            ContributionOriginType.Unknown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" example.mod ")]
    public void Constructor_RejectsInvalidNamespaces(string? namespaceId)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() =>
            new ContributionOriginType(namespaceId!, "impact"));

        Assert.Equal("namespaceId", error.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" impact ")]
    public void Constructor_RejectsInvalidLocalNames(string? localId)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() =>
            new ContributionOriginType("example.mod", localId!));

        Assert.Equal("localId", error.ParamName);
    }
}
