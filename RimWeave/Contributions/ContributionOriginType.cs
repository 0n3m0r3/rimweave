namespace RimWeave.Contributions;

/// <summary>An extensible origin type identified by a namespace and a case-sensitive local key.</summary>
/// <remarks>
/// Any provider can define a type without registration or a framework update.
/// The namespace identifies the type's vocabulary, not the contribution's owner or source owner.
/// Equal namespace/key pairs denote the same type; providers define their meaning cooperatively.
/// </remarks>
public sealed record ContributionOriginType
{
    /// <summary>An explicit declaration that the cause is unknown.</summary>
    public static ContributionOriginType Unknown { get; } = new("rimweave.framework", "unknown");

    public string NamespaceId { get; }
    public string LocalId { get; }

    public ContributionOriginType(string namespaceId, string localId)
    {
        NamespaceId = IdentifierRules.NormalizeNamespace(namespaceId, nameof(namespaceId));
        LocalId = IdentifierRules.ValidatePart(localId, nameof(localId));
    }
}
