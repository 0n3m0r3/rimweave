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

    /// <summary>The vocabulary namespace, normalized to invariant lowercase.</summary>
    public string NamespaceId { get; }
    /// <summary>The case-sensitive type name within that vocabulary.</summary>
    public string LocalId { get; }

    /// <summary>Defines or refers to a cause identifier without a central registration step.</summary>
    /// <param name="namespaceId">A nonblank vocabulary namespace with no surrounding whitespace.</param>
    /// <param name="localId">A nonblank local name with no surrounding whitespace.</param>
    /// <exception cref="ArgumentException">An identifier is null, blank or has surrounding whitespace.</exception>
    public ContributionOriginType(string namespaceId, string localId)
    {
        NamespaceId = IdentifierRules.NormalizeNamespace(namespaceId, nameof(namespaceId));
        LocalId = IdentifierRules.ValidatePart(localId, nameof(localId));
    }
}
