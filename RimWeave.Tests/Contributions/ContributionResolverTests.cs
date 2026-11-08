using RimWeave.Contributions;
using Xunit;

namespace RimWeave.Tests.Contributions;

public sealed class ContributionResolverTests
{
    private static readonly ContributionId LockId = new("example.mod", "lock");

    [Fact]
    public void Resolve_DistinguishesMissingUniqueAndAmbiguousCandidates()
    {
        var candidates = new List<LockContribution>();
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionResolver.Resolve(candidates, LockId).Status);

        var first = new LockContribution(LockId);
        candidates.Add(first);
        var unique = ContributionResolver.Resolve(candidates, LockId);
        Assert.Equal(ContributionLookupStatus.Found, unique.Status);
        Assert.Same(first, Assert.Single(unique.Matches));

        var second = new LockContribution(LockId);
        candidates.Add(second);
        var ambiguous = ContributionResolver.Resolve(candidates, LockId);
        Assert.Equal(ContributionLookupStatus.Ambiguous, ambiguous.Status);
        Assert.Equal(new[] { first, second }, ambiguous.Matches);
    }

    [Fact]
    public void Resolve_SeparatesOwnersWithoutMutatingUnattributedCandidates()
    {
        var expected = new LockContribution(LockId);
        var other = new LockContribution(new ContributionId("another.mod", LockId.LocalId));
        var unknown = new LockContribution();

        var result = ContributionResolver.Resolve(new[] { other, unknown, expected }, LockId);

        Assert.Same(expected, Assert.Single(result.Matches));
        Assert.Null(unknown.Metadata.Identity);
        Assert.Equal("another.mod", other.Metadata.Identity!.OwnerId);
    }

    [Fact]
    public void Resolve_UsesTheSuppliedOwnerCollectionWithoutRegisteringState()
    {
        var firstObjectLocks = new List<LockContribution> { new(LockId) };
        var secondObjectLocks = new List<LockContribution>();

        var first = ContributionResolver.Resolve(firstObjectLocks, LockId);
        Assert.Equal(ContributionLookupStatus.Found, first.Status);
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionResolver.Resolve(secondObjectLocks, LockId).Status);

        firstObjectLocks.Clear();
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionResolver.Resolve(firstObjectLocks, LockId).Status);
        Assert.Single(first.Matches);
    }

    [Fact]
    public void Resolve_ReturnsReadOnlyMembership()
    {
        var contribution = new LockContribution(LockId);
        var result = ContributionResolver.Resolve(new[] { contribution }, LockId);

        Assert.Throws<NotSupportedException>(() => ((IList<LockContribution>)result.Matches).Clear());
        Assert.Same(contribution, Assert.Single(result.Matches));
    }

    [Fact]
    public void Resolve_EnumeratesTheSourceOnceAndKeepsTheResolvedMembership()
    {
        var contribution = new LockContribution(LockId);
        int enumerations = 0;
        IEnumerable<LockContribution> Candidates()
        {
            enumerations++;
            yield return contribution;
        }

        var result = ContributionResolver.Resolve(Candidates(), LockId);
        Assert.Equal(ContributionLookupStatus.Found, result.Status);
        Assert.Same(contribution, Assert.Single(result.Matches));
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void Resolve_SupportsAnotherConsumerTypeWithoutANativeBaseClass()
    {
        var identity = new ContributionId("example.mod", "activity");
        var activity = new ActivityContribution();
        activity.Metadata.AssignIdentity(identity);

        ContributionLookupResult<ActivityContribution> result =
            ContributionResolver.Resolve(new[] { activity }, identity);

        Assert.Equal(ContributionLookupStatus.Found, result.Status);
        Assert.Same(activity, Assert.Single(result.Matches));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExclusiveClaim_ReportsEveryProviderRegardlessOfInsertionOrder(bool reverse)
    {
        var first = new LockContribution(new ContributionId("example.a", "lock"));
        var second = new LockContribution(new ContributionId("example.b", "lock"));
        first.Metadata.ClaimExclusiveResponsibility("example/property");
        second.Metadata.ClaimExclusiveResponsibility("example/property");
        var candidates = new List<LockContribution> { first, second };
        if (reverse) candidates.Reverse();

        var result = ContributionResolver.ResolveExclusiveClaim(candidates, "example/property");

        Assert.Equal(ContributionLookupStatus.Ambiguous, result.Status);
        Assert.Contains(first, result.Matches);
        Assert.Contains(second, result.Matches);
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void ExclusiveClaim_ExcludesOtherResponsibilitiesAndUnclaimedContributions()
    {
        var expected = new LockContribution(LockId);
        var other = new LockContribution(new ContributionId("example.mod", "other"));
        var unclaimed = new LockContribution(new ContributionId("example.mod", "observer"));
        expected.Metadata.ClaimExclusiveResponsibility("example/property");
        other.Metadata.ClaimExclusiveResponsibility("example/Property");

        var result = ContributionResolver.ResolveExclusiveClaim(new[] { expected, other, unclaimed }, "example/property");

        Assert.Equal(ContributionLookupStatus.Found, result.Status);
        Assert.Same(expected, Assert.Single(result.Matches));
        Assert.Null(unclaimed.Metadata.ExclusiveResponsibility);
    }

    [Fact]
    public void Resolve_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ContributionResolver.Resolve<LockContribution>(null!, LockId));
        Assert.Throws<ArgumentNullException>(() => ContributionResolver.Resolve(Array.Empty<LockContribution>(), null!));
        Assert.Throws<ArgumentNullException>(() => ContributionResolver.ResolveExclusiveClaim<LockContribution>(null!, "example/property"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" example/property ")]
    public void ExclusiveClaim_RejectsInvalidNames(string? responsibility)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            ContributionResolver.ResolveExclusiveClaim(Array.Empty<LockContribution>(), responsibility!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queries_RejectNullCandidates(bool exclusiveClaim)
    {
        var candidates = new LockContribution[] { null! };
        Assert.Throws<ArgumentException>(() =>
        {
            if (exclusiveClaim) ContributionResolver.ResolveExclusiveClaim(candidates, "example/property");
            else ContributionResolver.Resolve(candidates, LockId);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queries_RejectProvidersWithoutMetadata(bool exclusiveClaim)
    {
        var candidates = new[] { new InvalidContribution() };
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (exclusiveClaim) ContributionResolver.ResolveExclusiveClaim(candidates, "example/property");
            else ContributionResolver.Resolve(candidates, LockId);
        });
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void Queries_ReportUnavailableMetadataBeforeCountingMatches(bool exclusiveClaim, int matchCount)
    {
        var unavailable = new LockContribution
        {
            Metadata = ContributionMetadata.Unavailable("Incomplete saved identity.")
        };
        var unattributed = new LockContribution();
        var matching = Enumerable.Range(0, matchCount).Select(_ => new LockContribution(LockId)).ToArray();
        foreach (var contribution in matching)
            contribution.Metadata.ClaimExclusiveResponsibility("example/property");
        var candidates = new List<LockContribution> { unavailable, unattributed };
        candidates.AddRange(matching);

        foreach (bool reverse in new[] { false, true })
        {
            if (reverse) candidates.Reverse();
            var result = exclusiveClaim
                ? ContributionResolver.ResolveExclusiveClaim(candidates, "example/property")
                : ContributionResolver.Resolve(candidates, LockId);

            Assert.Equal(ContributionLookupStatus.InvalidMetadata, result.Status);
            Assert.Same(unavailable, Assert.Single(result.InvalidCandidates));
            Assert.Equal(matchCount, result.Matches.Count);
            foreach (var contribution in matching) Assert.Contains(contribution, result.Matches);
            Assert.DoesNotContain(unattributed, result.Matches);
            Assert.Null(unattributed.Metadata.LoadError);
            Assert.Equal("Incomplete saved identity.", unavailable.Metadata.LoadError);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queries_SnapshotUnavailableCandidatesAcrossConsumerTypesInOneEnumeration(bool exclusiveClaim)
    {
        var valid = new LockContribution(LockId);
        valid.Metadata.ClaimExclusiveResponsibility("example/property");
        var unavailableLock = new LockContribution
        {
            Metadata = ContributionMetadata.Unavailable("Invalid saved lock metadata.")
        };
        var unavailableActivity = new ActivityContribution
        {
            Metadata = ContributionMetadata.Unavailable("Unsupported activity metadata version.")
        };
        var storage = new List<IContribution> { unavailableActivity, valid, unavailableLock };
        int enumerations = 0;
        IEnumerable<IContribution> Candidates()
        {
            enumerations++;
            foreach (var candidate in storage) yield return candidate;
        }

        var result = exclusiveClaim
            ? ContributionResolver.ResolveExclusiveClaim(Candidates(), "example/property")
            : ContributionResolver.Resolve(Candidates(), LockId);
        storage.Clear();

        Assert.Equal(1, enumerations);
        Assert.Equal(ContributionLookupStatus.InvalidMetadata, result.Status);
        Assert.Same(valid, Assert.Single(result.Matches));
        Assert.Equal(new IContribution[] { unavailableActivity, unavailableLock }, result.InvalidCandidates);
        Assert.Throws<NotSupportedException>(() => ((IList<IContribution>)result.InvalidCandidates).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<IContribution>)result.Matches).Clear());
    }

    [Fact]
    public void Resolve_UnavailableMetadataOnlyAffectsTheSuppliedScope()
    {
        var valid = new LockContribution(LockId);
        var unavailable = new LockContribution
        {
            Metadata = ContributionMetadata.Unavailable("Invalid saved metadata.")
        };

        var affected = ContributionResolver.Resolve(new[] { unavailable }, LockId);
        var unaffected = ContributionResolver.Resolve(new[] { valid }, LockId);

        Assert.Equal(ContributionLookupStatus.InvalidMetadata, affected.Status);
        Assert.Equal(ContributionLookupStatus.Found, unaffected.Status);
        Assert.Empty(unaffected.InvalidCandidates);
        Assert.Same(valid, Assert.Single(unaffected.Matches));
    }

    // These are provider-owned records, not implementations of native locking or activities.
    private sealed class LockContribution : IContribution
    {
        public ContributionMetadata Metadata { get; init; } = new();

        public LockContribution(ContributionId? identity = null)
        {
            if (identity is not null) Metadata.AssignIdentity(identity);
        }
    }

    private sealed class ActivityContribution : IContribution
    {
        public ContributionMetadata Metadata { get; init; } = new();
    }

    private sealed class InvalidContribution : IContribution
    {
        public ContributionMetadata Metadata => null!;
    }
}
