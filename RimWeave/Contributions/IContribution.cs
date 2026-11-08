namespace RimWeave.Contributions;

/// <summary>Exposes a contribution's attribution independently of its storage and target.</summary>
/// <remarks>Reading Metadata must return the same non-null instance without applying an effect.</remarks>
public interface IContribution
{
    ContributionMetadata Metadata { get; }
}
