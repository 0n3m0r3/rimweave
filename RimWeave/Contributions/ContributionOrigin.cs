namespace RimWeave.Contributions;

/// <summary>Describes a contribution's cause independently of its owner and target.</summary>
/// <remarks>
/// SourceReference is an optional, opaque reference supplied and interpreted by the provider.
/// It may identify a native or external source, event or contribution. Its format belongs to
/// the provider's contract; it is preserved verbatim, not resolved or checked for existence here.
/// No source object is copied, and the type implies no lifetime or reversal policy.
/// Missing provenance remains absent metadata; Unknown is an explicit provider declaration.
/// </remarks>
public sealed record ContributionOrigin
{
    /// <summary>The cause identifier, retained even when the framework does not interpret its meaning.</summary>
    public ContributionOriginType Type { get; }
    /// <summary>An optional provider-defined source reference, preserved verbatim and never resolved here.</summary>
    public string? SourceReference { get; }

    /// <summary>Creates an origin description without changing a contribution or a source object.</summary>
    /// <param name="type">A declared cause, including an explicit Unknown when appropriate.</param>
    /// <param name="sourceReference">An optional nonblank reference with no surrounding whitespace.</param>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
    /// <exception cref="ArgumentException">A supplied source reference is blank or has surrounding whitespace.</exception>
    public ContributionOrigin(ContributionOriginType type, string? sourceReference = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (sourceReference is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
            if (sourceReference != sourceReference.Trim())
                throw new ArgumentException("References must not have surrounding whitespace.", nameof(sourceReference));
        }

        Type = type;
        SourceReference = sourceReference;
    }
}
