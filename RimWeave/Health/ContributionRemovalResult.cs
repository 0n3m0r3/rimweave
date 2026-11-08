using RimWeave.Contributions;

namespace RimWeave.Health;

/// <summary>The resolved candidates and, when unique, the guarded removal outcome.</summary>
/// <param name="Lookup">Membership observed before the removal attempt.</param>
/// <param name="Removal">
/// Null unless Lookup.Status is Found, including when scoped metadata is unreadable. A non-null result does not necessarily mean
/// removal succeeded; inspect its status. Native exceptions propagate instead of returning a result.
/// </param>
public sealed record ContributionRemovalResult(
    ContributionLookupResult<ContributionHediff> Lookup,
    HediffRemovalResult? Removal);
