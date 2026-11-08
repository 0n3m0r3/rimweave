namespace RimWeave.Contributions;

public enum ContributionLookupStatus
{
    NotPresent,
    Found,
    Ambiguous
}

/// <summary>A snapshot of candidates for an operation that requires a unique contribution.</summary>
/// <remarks>
/// Candidate membership is read-only; the referenced contributions remain owned by their providers.
/// A result does not reserve a candidate or guarantee that it is still present when later used.
/// </remarks>
public sealed class ContributionLookupResult<T> where T : class, IContribution
{
    public IReadOnlyList<T> Matches { get; }
    public ContributionLookupStatus Status => Matches.Count switch
    {
        0 => ContributionLookupStatus.NotPresent,
        1 => ContributionLookupStatus.Found,
        _ => ContributionLookupStatus.Ambiguous
    };

    internal ContributionLookupResult(IEnumerable<T> matches)
    {
        Matches = Array.AsReadOnly(matches.ToArray());
    }
}
