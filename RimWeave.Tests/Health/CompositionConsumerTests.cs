using System.Reflection;
using System.Runtime.CompilerServices;
using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;
using Medical = RimWeave.Example.Composition.CompositionContribution;
using Temporary = RimWeave.Example.Peer.Composition.CompositionContribution;

namespace RimWeave.Tests.Health;

[Collection(ScribeTestCollection.Name)]
public sealed class CompositionConsumerTests : IDisposable
{
    private readonly PeerDefinitionScope _definitions = new();

    public void Dispose() => _definitions.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reapply_UsesItsOwnAttachedIdentityDespiteTheSharedExclusiveClaim(bool peer)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var medical = ContributionTestData.AttachForLookup(pawn, Medical.Identity, part);
        var temporary = ContributionTestData.AttachForLookup(pawn, Temporary.Identity, part);
        medical.Metadata.ClaimExclusiveResponsibility(Medical.Responsibility);
        temporary.Metadata.ClaimExclusiveResponsibility(Temporary.Responsibility);
        var expected = peer ? temporary : medical;
        expected.ageTicks = 123;

        var result = Add(peer, pawn, part);

        Assert.Equal(ContributionAdditionStatus.AlreadyPresent, result.Status);
        Assert.Same(expected, result.Hediff);
        Assert.Equal(123, expected.ageTicks);
        Assert.Equal(new Hediff[] { medical, temporary }, pawn.health.hediffSet.hediffs);
        Assert.Equal(ContributionLookupStatus.Ambiguous,
            ContributionQueries.ResolveExclusiveClaim(pawn, Medical.Responsibility, part).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateOwnIdentity_NeitherAddsNorRemovesAnArbitraryCandidate(bool peer)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var identity = peer ? Temporary.Identity : Medical.Identity;
        ContributionTestData.AttachForLookup(pawn, identity, part);
        ContributionTestData.AttachForLookup(pawn, identity, part);
        var before = pawn.health.hediffSet.hediffs.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => Add(peer, pawn, part));
        var removal = Remove(peer, pawn, part);

        Assert.Contains("Ambiguous", error.Message);
        Assert.Equal(ContributionLookupStatus.Ambiguous, removal.Lookup.Status);
        Assert.Null(removal.Removal);
        Assert.Equal(before, pawn.health.hediffSet.hediffs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_MissingOwnIdentity_PreservesTheOtherProviderWithTheSameLocalId(bool peer)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var part = new BodyPartRecord();
        var other = ContributionTestData.AttachForLookup(pawn, peer ? Medical.Identity : Temporary.Identity, part);

        var result = Remove(peer, pawn, part);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Same(other, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_DoesNotFallBackToItsContributionOnAnotherPart(bool peer)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var first = new BodyPartRecord();
        var second = new BodyPartRecord();
        var effect = ContributionTestData.AttachForLookup(pawn, peer ? Temporary.Identity : Medical.Identity, second);

        var result = Remove(peer, pawn, first);

        Assert.Equal(ContributionLookupStatus.NotPresent, result.Lookup.Status);
        Assert.Null(result.Removal);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void Consumers_AreSeparateAssembliesWithoutAMutualReference()
    {
        var first = typeof(Medical).Assembly;
        var second = typeof(Temporary).Assembly;

        Assert.NotSame(first, second);
        Assert.DoesNotContain(first.GetReferencedAssemblies(), name => name.Name == second.GetName().Name);
        Assert.DoesNotContain(second.GetReferencedAssemblies(), name => name.Name == first.GetName().Name);
        Assert.Contains(first.GetReferencedAssemblies(), name => name.Name == typeof(ContributionOperations).Assembly.GetName().Name);
        Assert.Contains(second.GetReferencedAssemblies(), name => name.Name == typeof(ContributionOperations).Assembly.GetName().Name);
        Assert.Equal(Medical.Identity.LocalId, Temporary.Identity.LocalId);
        Assert.NotEqual(Medical.Identity.OwnerId, Temporary.Identity.OwnerId);
        Assert.Equal(Medical.Responsibility, Temporary.Responsibility);
    }

    [Fact]
    public void CompositionCommands_AreOnlyIncludedInDebugBuilds()
    {
        var names = new[]
        {
            (typeof(Medical).Assembly, "RimWeave.Example.Composition.CompositionActions"),
            (typeof(Temporary).Assembly, "RimWeave.Example.Peer.Composition.CompositionActions")
        };
        foreach (var (assembly, name) in names)
        {
#if DEBUG
            Assert.NotNull(assembly.GetType(name));
#else
            Assert.Null(assembly.GetType(name));
#endif
        }
    }

#if DEBUG
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PreparedCommand_RejectsExecutionAfterDeveloperModeIsDisabled(bool peer, bool remove)
    {
        using var game = new NativeGameTestScope();
        using var preferences = new DeveloperPreferencesScope();
        var pawn = HealthTestPawns.CreateForGuardChecks();
        preferences.Data.devMode = true;
        var expectedGame = game.Game;
        string Command() => Execute(peer, expectedGame, pawn, remove);
        preferences.Data.devMode = false;

        Assert.Contains("developer mode", Command(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Empty(game.Game.components);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PreparedCommand_RejectsAReplacedGameBeforeNativeCreation(bool peer, bool remove)
    {
        using var game = new NativeGameTestScope();
        using var preferences = new DeveloperPreferencesScope();
        preferences.Data.devMode = true;
        var pawn = HealthTestPawns.CreateForGuardChecks();
        var expectedGame = game.Game;
        string Command() => Execute(peer, expectedGame, pawn, remove);
        using var replacement = new NativeGameTestScope();

        Assert.Contains("game", Command(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Command_RejectsAnUnavailablePawnBeforeNativeCreation(bool peer)
    {
        using var game = new NativeGameTestScope();
        using var preferences = new DeveloperPreferencesScope();
        preferences.Data.devMode = true;
        var pawn = HealthTestPawns.CreateForGuardChecks();

        Assert.Contains("available pawn", Execute(peer, game.Game, pawn, false), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(pawn.health.hediffSet.hediffs);
    }

    private static string Execute(bool peer, Game game, Pawn pawn, bool remove) => peer
        ? Example.Peer.Composition.CompositionActions.Execute(game, pawn, remove)
        : Example.Composition.CompositionActions.Execute(game, pawn, remove);

    private sealed class DeveloperPreferencesScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = Field.GetValue(null);
        internal PrefsData Data { get; } = (PrefsData)RuntimeHelpers.GetUninitializedObject(typeof(PrefsData));
        internal DeveloperPreferencesScope() => Field.SetValue(null, Data);
        public void Dispose() => Field.SetValue(null, _previous);
    }
#endif

    private static ContributionAdditionResult Add(bool peer, Pawn pawn, BodyPartRecord part) =>
        peer ? Temporary.Add(pawn, part) : Medical.Add(pawn, part);

    private static ContributionRemovalResult Remove(bool peer, Pawn pawn, BodyPartRecord part) =>
        peer ? Temporary.Remove(pawn, part) : Medical.Remove(pawn, part);
}
