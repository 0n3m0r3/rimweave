namespace RimWeave.Contributions;

/// <summary>Exposes a contribution's attribution independently of its storage and target.</summary>
/// <remarks>
/// Reading Metadata must return the owner's current non-null instance without applying an effect.
/// A load operation may replace that instance; read it from the restored owner after loading.
/// </remarks>
public interface IContribution
{
    /// <summary>The contribution's current metadata, retained between reads until the owner restores it.</summary>
    ContributionMetadata Metadata { get; }
}
