namespace RimWeave.Contributions;

/// <summary>Exposes a contribution's attribution independently of its storage and target.</summary>
/// <remarks>Reading Metadata must return the same non-null instance without applying an effect.</remarks>
public interface IContribution
{
    /// <summary>The same non-null metadata instance for this contribution's lifetime.</summary>
    ContributionMetadata Metadata { get; }
}
