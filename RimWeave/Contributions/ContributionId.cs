using System.Globalization;

namespace RimWeave.Contributions;

/// <summary>A stable extension identifier and a case-sensitive local contribution key.</summary>
public sealed record ContributionId
{
    public string OwnerId { get; }
    public string LocalId { get; }

    public ContributionId(string ownerId, string localId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localId);
        if (ownerId != ownerId.Trim() || localId != localId.Trim())
        {
            throw new ArgumentException("Identifiers must not have surrounding whitespace.");
        }

        OwnerId = ownerId.ToLower(CultureInfo.InvariantCulture);
        LocalId = localId;
    }
}
