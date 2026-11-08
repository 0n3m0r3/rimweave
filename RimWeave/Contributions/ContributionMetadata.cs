namespace RimWeave.Contributions;

/// <summary>Attribution and provenance shared by contributions with different native owners.</summary>
/// <remarks>
/// Metadata does not register, attach, serialize or apply a contribution.
/// Missing attribution remains unknown. The containing owner controls its lifetime.
/// When LoadError is present, values are unavailable rather than absent and mutations are refused.
/// Mutate metadata on the owning domain's thread.
/// </remarks>
public sealed class ContributionMetadata
{
    /// <summary>The declared attribution, or null when undeclared or unavailable.</summary>
    public ContributionId? Identity { get; private set; }
    /// <summary>An exact, case-sensitive exclusive responsibility key, or null when unclaimed or unavailable.</summary>
    public string? ExclusiveResponsibility { get; private set; }
    /// <summary>The declared cause, or null when undeclared or unavailable.</summary>
    /// <remarks>Null is not automatically converted to <see cref="ContributionOriginType.Unknown"/>.</remarks>
    public ContributionOrigin? Origin { get; private set; }
    /// <summary>Why saved metadata could not be read, or null when no load error occurred.</summary>
    /// <remarks>A load error makes all metadata unavailable; it does not remove the owning contribution.</remarks>
    public string? LoadError { get; private set; }

    /// <summary>Assigns an identity, accepting repeated equal values without changing ownership.</summary>
    /// <param name="identity">The identity declared by the provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A different identity has already been assigned, or saved metadata is unavailable.</exception>
    public void AssignIdentity(ContributionId identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        EnsureAvailable();
        if (Identity is not null && Identity != identity)
            throw new InvalidOperationException("A contribution cannot be reassigned.");

        Identity = identity;
    }

    /// <summary>Declares an exclusive claim without choosing a winner or applying an effect.</summary>
    /// <param name="responsibility">A nonblank, case-sensitive key with no surrounding whitespace.</param>
    /// <exception cref="ArgumentException">The key is null, blank or has surrounding whitespace.</exception>
    /// <exception cref="InvalidOperationException">A different responsibility has already been declared, or saved metadata is unavailable.</exception>
    /// <remarks>Equal claims are accepted again. Providers agree on key semantics and query scope.</remarks>
    public void ClaimExclusiveResponsibility(string responsibility)
    {
        ValidateResponsibility(responsibility);
        EnsureAvailable();
        if (ExclusiveResponsibility is not null && ExclusiveResponsibility != responsibility)
            throw new InvalidOperationException("An exclusive responsibility cannot be reassigned.");

        ExclusiveResponsibility = responsibility;
    }

    /// <summary>Records a declared origin without applying an effect or making it reversible.</summary>
    /// <param name="origin">The immutable description supplied by a provider or adapter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="origin"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A different origin has already been declared, or saved metadata is unavailable.</exception>
    /// <remarks>Repeated equal descriptions are accepted; a custom cause is never replaced by Unknown.</remarks>
    public void DescribeOrigin(ContributionOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        EnsureAvailable();
        if (Origin is not null && Origin != origin)
            throw new InvalidOperationException("A declared origin cannot be reassigned.");

        Origin = origin;
    }

    internal static ContributionMetadata Unavailable(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new ContributionMetadata { LoadError = error };
    }

    private void EnsureAvailable()
    {
        if (LoadError is not null)
            throw new InvalidOperationException("Metadata could not be loaded and cannot be changed.");
    }

    internal static void ValidateResponsibility(string responsibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibility);
        if (responsibility != responsibility.Trim())
            throw new ArgumentException("Responsibilities must not have surrounding whitespace.", nameof(responsibility));
    }
}
