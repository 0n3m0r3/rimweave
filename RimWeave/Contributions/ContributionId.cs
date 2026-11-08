namespace RimWeave.Contributions;

/// <summary>A stable extension identifier and a case-sensitive local contribution key.</summary>
public sealed record ContributionId
{
    public string OwnerId { get; }
    public string LocalId { get; }

    public ContributionId(string ownerId, string localId)
    {
        OwnerId = IdentifierRules.NormalizeNamespace(ownerId, nameof(ownerId));
        LocalId = IdentifierRules.ValidatePart(localId, nameof(localId));
    }
}
