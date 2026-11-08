namespace RimWeave.Contributions;

/// <summary>Whether metadata was usable and how many contributions matched in the supplied scope.</summary>
public enum ContributionLookupStatus
{
    /// <summary>No contribution matched.</summary>
    NotPresent,
    /// <summary>Exactly one contribution matched.</summary>
    Found,
    /// <summary>Several contributions matched; no winner was selected.</summary>
    Ambiguous,
    /// <summary>A candidate's saved metadata is unavailable, so the query cannot establish a complete result.</summary>
    InvalidMetadata
}

/// <summary>A snapshot of candidates for an operation that requires a unique contribution.</summary>
/// <remarks>
/// Candidate membership is read-only; the referenced contributions remain owned by their providers.
/// A result does not reserve a candidate or guarantee that it is still present when later used.
/// </remarks>
/// <typeparam name="T">The contribution type retained in the result.</typeparam>
public sealed class ContributionLookupResult<T> where T : class, IContribution
{
    /// <summary>The read-only snapshot of valid matches, retaining references to the original contributions.</summary>
    public IReadOnlyList<T> Matches { get; }
    /// <summary>Scoped candidates whose metadata could not be read, preventing a complete lookup.</summary>
    public IReadOnlyList<T> InvalidCandidates { get; }
    /// <summary>Invalid metadata takes precedence over the number of valid matches; this is not an operation outcome.</summary>
    public ContributionLookupStatus Status => InvalidCandidates.Count > 0
        ? ContributionLookupStatus.InvalidMetadata
        : Matches.Count switch
    {
        0 => ContributionLookupStatus.NotPresent,
        1 => ContributionLookupStatus.Found,
        _ => ContributionLookupStatus.Ambiguous
    };

    internal ContributionLookupResult(IEnumerable<T> matches, IEnumerable<T> invalidCandidates)
    {
        Matches = Array.AsReadOnly(matches.ToArray());
        InvalidCandidates = Array.AsReadOnly(invalidCandidates.ToArray());
    }
}
