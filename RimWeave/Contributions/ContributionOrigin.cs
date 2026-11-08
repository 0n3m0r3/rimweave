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
    public ContributionOriginType Type { get; }
    public string? SourceReference { get; }

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
