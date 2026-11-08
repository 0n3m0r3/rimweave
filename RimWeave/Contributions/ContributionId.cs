namespace RimWeave.Contributions;

/// <summary>A stable extension identifier and a case-sensitive local contribution key.</summary>
public sealed record ContributionId
{
    /// <summary>The provider identifier, normalized to invariant lowercase.</summary>
    public string OwnerId { get; }
    /// <summary>The provider's case-sensitive key within a caller-selected target scope.</summary>
    public string LocalId { get; }

    /// <summary>Creates a value identity without registering or attaching a contribution.</summary>
    /// <param name="ownerId">A nonblank provider identifier with no surrounding whitespace.</param>
    /// <param name="localId">A nonblank local key with no surrounding whitespace.</param>
    /// <exception cref="ArgumentException">An identifier is null, blank or has surrounding whitespace.</exception>
    /// <remarks>Identity is attribution, not an authorization boundary or a globally unique target identifier.</remarks>
    public ContributionId(string ownerId, string localId)
    {
        OwnerId = IdentifierRules.NormalizeNamespace(ownerId, nameof(ownerId));
        LocalId = IdentifierRules.ValidatePart(localId, nameof(localId));
    }
}
