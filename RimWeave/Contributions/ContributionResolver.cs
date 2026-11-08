namespace RimWeave.Contributions;

/// <summary>Resolves contributions in a caller-supplied scope without keeping a registry.</summary>
/// <remarks>
/// The caller selects the authoritative collection and target scope. Candidates are enumerated
/// once per query; their order never selects a winner. Use the owning domain's mutation thread.
/// </remarks>
public static class ContributionResolver
{
    public static ContributionLookupResult<T> Resolve<T>(
        IEnumerable<T> candidates, ContributionId identity) where T : class, IContribution
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(identity);

        return new ContributionLookupResult<T>(candidates
            .Where(candidate => ReadMetadata(candidate).Identity == identity));
    }

    /// <summary>Finds explicit exclusive claimants without treating ordinary observers as owners.</summary>
    public static ContributionLookupResult<T> ResolveExclusiveClaim<T>(
        IEnumerable<T> candidates, string responsibility) where T : class, IContribution
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ContributionMetadata.ValidateResponsibility(responsibility);

        return new ContributionLookupResult<T>(candidates
            .Where(candidate => ReadMetadata(candidate).ExclusiveResponsibility == responsibility));
    }

    private static ContributionMetadata ReadMetadata<T>(T candidate) where T : class, IContribution
    {
        if (candidate is null)
            throw new ArgumentException("Candidates must not contain null contributions.", nameof(candidate));

        return candidate.Metadata
            ?? throw new InvalidOperationException("A contribution must expose non-null metadata.");
    }
}
