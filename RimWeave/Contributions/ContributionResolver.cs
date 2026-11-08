namespace RimWeave.Contributions;

/// <summary>Resolves contributions in a caller-supplied scope without keeping a registry.</summary>
/// <remarks>
/// The caller selects the authoritative collection and target scope. Candidates are enumerated
/// once per query; their order never selects a winner. Use the owning domain's mutation thread.
/// </remarks>
public static class ContributionResolver
{
    /// <summary>Returns every candidate with the requested identity, without modifying storage.</summary>
    /// <typeparam name="T">The caller's contribution type.</typeparam>
    /// <param name="candidates">The authoritative candidates in a scope already selected by the caller.</param>
    /// <param name="identity">The attribution value to match.</param>
    /// <returns>A read-only snapshot reporting invalid metadata, no match, one match or multiple matches.</returns>
    /// <exception cref="ArgumentNullException">The sequence or identity is null.</exception>
    /// <exception cref="ArgumentException">The sequence contains a null candidate.</exception>
    /// <exception cref="InvalidOperationException">A candidate exposes null metadata.</exception>
    /// <remarks>Enumeration and provider exceptions propagate. A match does not reserve an instance.</remarks>
    public static ContributionLookupResult<T> Resolve<T>(
        IEnumerable<T> candidates, ContributionId identity) where T : class, IContribution
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(identity);

        return ResolveWhere(candidates, metadata => metadata.Identity == identity);
    }

    /// <summary>Finds explicit exclusive claimants without treating ordinary observers as owners.</summary>
    /// <typeparam name="T">The caller's contribution type.</typeparam>
    /// <param name="candidates">The authoritative candidates in one selected responsibility scope.</param>
    /// <param name="responsibility">The exact, case-sensitive key agreed by cooperating providers.</param>
    /// <returns>A snapshot reporting invalid metadata before considering the number of explicit claimants.</returns>
    /// <exception cref="ArgumentNullException">The candidate sequence is null.</exception>
    /// <exception cref="ArgumentException">The key is invalid or a candidate is null.</exception>
    /// <exception cref="InvalidOperationException">A candidate exposes null metadata.</exception>
    /// <remarks>This query neither discovers arbitrary effects nor infers their gameplay compatibility.</remarks>
    public static ContributionLookupResult<T> ResolveExclusiveClaim<T>(
        IEnumerable<T> candidates, string responsibility) where T : class, IContribution
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ContributionMetadata.ValidateResponsibility(responsibility);

        return ResolveWhere(candidates, metadata => metadata.ExclusiveResponsibility == responsibility);
    }

    private static ContributionLookupResult<T> ResolveWhere<T>(
        IEnumerable<T> candidates, Func<ContributionMetadata, bool> matchesQuery) where T : class, IContribution
    {
        var matches = new List<T>();
        var invalidCandidates = new List<T>();
        foreach (var candidate in candidates)
        {
            var metadata = ReadMetadata(candidate);
            if (metadata.LoadError is not null)
                invalidCandidates.Add(candidate);
            else if (matchesQuery(metadata))
                matches.Add(candidate);
        }

        return new ContributionLookupResult<T>(matches, invalidCandidates);
    }

    private static ContributionMetadata ReadMetadata<T>(T candidate) where T : class, IContribution
    {
        if (candidate is null)
            throw new ArgumentException("Candidates must not contain null contributions.", nameof(candidate));

        return candidate.Metadata
            ?? throw new InvalidOperationException("A contribution must expose non-null metadata.");
    }
}
