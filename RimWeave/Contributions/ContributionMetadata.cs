namespace RimWeave.Contributions;

/// <summary>Attribution and provenance shared by contributions with different native owners.</summary>
/// <remarks>
/// Metadata does not register, attach, serialize or apply a contribution.
/// Missing attribution remains unknown. The containing owner controls its lifetime.
/// Mutate metadata on the owning domain's thread.
/// </remarks>
public sealed class ContributionMetadata
{
    public ContributionId? Identity { get; private set; }
    public string? ExclusiveResponsibility { get; private set; }
    public ContributionOrigin? Origin { get; private set; }

    public void AssignIdentity(ContributionId identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (Identity is not null && Identity != identity)
            throw new InvalidOperationException("A contribution cannot be reassigned.");

        Identity = identity;
    }

    /// <summary>Declares an exclusive claim without choosing a winner or applying an effect.</summary>
    public void ClaimExclusiveResponsibility(string responsibility)
    {
        ValidateResponsibility(responsibility);
        if (ExclusiveResponsibility is not null && ExclusiveResponsibility != responsibility)
            throw new InvalidOperationException("An exclusive responsibility cannot be reassigned.");

        ExclusiveResponsibility = responsibility;
    }

    /// <summary>Records a declared origin without applying an effect or making it reversible.</summary>
    public void DescribeOrigin(ContributionOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (Origin is not null && Origin != origin)
            throw new InvalidOperationException("A declared origin cannot be reassigned.");

        Origin = origin;
    }

    internal static void ValidateResponsibility(string responsibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibility);
        if (responsibility != responsibility.Trim())
            throw new ArgumentException("Responsibilities must not have surrounding whitespace.", nameof(responsibility));
    }
}
