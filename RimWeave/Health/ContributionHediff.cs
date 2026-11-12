using RimWeave.Contributions;
using RimWeave.Logging;
using RimWeave.Persistence;
using RimWeave.References;
using Verse;

namespace RimWeave.Health;

/// <summary>A native health effect whose contribution must retain its own lifetime.</summary>
/// <remarks>
/// Opt in through an extension's HediffDef. This does not alter ordinary Hediffs or
/// prevent unrelated patches from changing native behavior. Call on the simulation thread.
/// </remarks>
public class ContributionHediff : HediffWithComps, IContribution
{
    private readonly ContributionMetadataScribe _metadataScribe = new();

    /// <summary>The shared metadata restored with this native effect, including any load diagnostic.</summary>
    public ContributionMetadata Metadata => _metadataScribe.Metadata;

    /// <summary>Initializes optional XML attribution before native creation callbacks.</summary>
    /// <remarks>
    /// Use HediffMaker.MakeHediff for creation. Native Scribe loading does not call this method.
    /// Subclasses must call base.PostMake; invalid declarations fail before native creation callbacks.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Declared attribution is invalid or conflicts with existing metadata.</exception>
    public override void PostMake()
    {
        var declaration = ContributionDefExtension.Read(def);
        if (declaration is not null)
        {
            Metadata.AssignIdentity(declaration.Identity!);
            if (declaration.ExclusiveResponsibility is not null)
                Metadata.ClaimExclusiveResponsibility(declaration.ExclusiveResponsibility);
        }
        base.PostMake();
    }

    /// <summary>Restores native health state and contribution metadata without replaying an effect.</summary>
    /// <remarks>Overrides in extensions must call base.ExposeData in every native save/load phase.</remarks>
    public override void ExposeData()
    {
        base.ExposeData();
        _metadataScribe.ExposeData();
        if (Scribe.mode == LoadSaveMode.PostLoadInit && Metadata.LoadError is not null)
            RimWeaveLog.Message($"Contribution metadata unavailable on {def?.defName} (Hediff {loadID}): "
                + $"{Metadata.LoadError} The native effect and unreadable metadata are retained.");
    }

    /// <summary>Records the owner's death without deciding whether a consumer may still use its reference.</summary>
    public override void Notify_PawnDied(DamageInfo? dinfo, Hediff? culprit = null)
    {
        ReferenceTrackingGameComponent.NotifyHealth(this, ReferenceChanges.Died);
        base.Notify_PawnDied(dinfo, culprit);
    }

    /// <summary>Records native resurrection without restarting a consumer's activity.</summary>
    public override void Notify_Resurrected()
    {
        ReferenceTrackingGameComponent.NotifyHealth(this, ReferenceChanges.Resurrected);
        base.Notify_Resurrected();
    }

    /// <summary>Distinguishes native pawn discard from destruction of its corpse alone.</summary>
    public override void Notify_PawnCorpseDestroyed()
    {
        if (pawn.Discarded)
            ReferenceTrackingGameComponent.NotifyHealth(this, ReferenceChanges.Discarded);
        base.Notify_PawnCorpseDestroyed();
    }

    /// <summary>Records detachment of this exact instance and releases its active subscriptions.</summary>
    public override void PostRemoved()
    {
        if (!pawn.health.hediffSet.hediffs.Any(effect => ReferenceEquals(effect, this)))
            ReferenceTrackingGameComponent.NotifyHealth(this, ReferenceChanges.EffectDetached);
        base.PostRemoved();
    }

    /// <summary>Declines every native merge request to preserve this instance's independent lifetime.</summary>
    /// <param name="other">The native effect offered for merging.</param>
    /// <returns>Always false.</returns>
    // Native AddDirect asks existing effects whether they accept a merge.
    public sealed override bool TryMergeWith(Hediff other) => false;
}
