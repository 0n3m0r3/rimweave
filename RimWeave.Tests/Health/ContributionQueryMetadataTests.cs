using System.Reflection;
using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Persistence;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class ContributionQueryMetadataTests
{
    private static readonly ContributionId Saved = new("example.owner", "saved");
    private static readonly ContributionId Alternate = new("example.owner", "alternate");

    [Fact]
    public void HealthQueriesReadSavedMetadataWithoutInvokingAReimplementedGetter()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var effect = new RedirectedContribution { pawn = pawn, Part = part, ThrowOnRead = true };
        effect.Metadata.AssignIdentity(Saved);
        effect.Metadata.ClaimExclusiveResponsibility("example/saved");
        pawn.health.hediffSet.hediffs.AddRange([effect, effect]);
        var random = typeof(Rand).GetProperty("StateCompressed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = random.GetValue(null);

        Assert.Same(effect, Assert.Single(ContributionQueries.Find(pawn, Saved, part)));
        Assert.Equal(ContributionLookupStatus.Found, ContributionQueries.Resolve(pawn, Saved, part).Status);
        Assert.Same(effect, Assert.Single(ContributionQueries.ResolveExclusiveClaim(pawn, "example/saved", part).Matches));
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionQueries.Resolve(pawn, Saved).Status);
        Assert.Equal(0, effect.GetterCalls);
        Assert.Equal(before, random.GetValue(null));
        Assert.Equal(new Hediff[] { effect, effect }, pawn.health.hediffSet.hediffs);
    }

    [Fact]
    public void AlternateInterfaceAttributionDoesNotRetargetHealthQueries()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var effect = new RedirectedContribution { pawn = pawn };
        effect.Metadata.AssignIdentity(Saved);
        effect.Metadata.ClaimExclusiveResponsibility("example/saved");
        pawn.health.hediffSet.hediffs.Add(effect);

        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionQueries.Resolve(pawn, Alternate).Status);
        Assert.Equal(ContributionLookupStatus.NotPresent, ContributionQueries.ResolveExclusiveClaim(pawn, "example/alternate").Status);
        Assert.Equal(0, effect.GetterCalls);
        // The entity-independent API still reads the interface chosen by its caller.
        Assert.Same(effect, Assert.Single(ContributionResolver.Resolve([effect], Alternate).Matches));
        Assert.Equal(1, effect.GetterCalls);
    }

    [Fact]
    public void UnavailableSavedMetadataCannotBeHiddenByAnAlternateInterface()
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var effect = new RedirectedContribution { pawn = pawn };
        var storage = (ContributionMetadataScribe)typeof(ContributionHediff)
            .GetField("_metadataScribe", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(effect)!;
        typeof(ContributionMetadataScribe).GetProperty(nameof(ContributionMetadataScribe.Metadata))!
            .SetValue(storage, ContributionMetadata.Unavailable("Unreadable fixture"));
        pawn.health.hediffSet.hediffs.Add(effect);

        var result = ContributionQueries.Resolve(pawn, Alternate);
        Assert.Equal(ContributionLookupStatus.InvalidMetadata, result.Status);
        Assert.Same(effect, Assert.Single(result.InvalidCandidates));
        Assert.Equal(ContributionLookupStatus.InvalidMetadata,
            ContributionQueries.ResolveExclusiveClaim(pawn, "example/alternate").Status);
        Assert.Throws<InvalidOperationException>(() => ContributionQueries.Find(pawn, Alternate));
        Assert.Equal(0, effect.GetterCalls);
    }

    private sealed class RedirectedContribution : ContributionHediff, IContribution
    {
        internal bool ThrowOnRead;
        internal int GetterCalls;
        private readonly ContributionMetadata _alternate = new();
        internal RedirectedContribution()
        {
            _alternate.AssignIdentity(Alternate);
            _alternate.ClaimExclusiveResponsibility("example/alternate");
        }
        ContributionMetadata IContribution.Metadata
        {
            get
            {
                GetterCalls++;
                if (ThrowOnRead) throw new InvalidOperationException("Do not call the alternate getter.");
                return _alternate;
            }
        }
    }
}
