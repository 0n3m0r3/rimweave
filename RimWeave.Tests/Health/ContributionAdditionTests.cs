using RimWeave.Contributions;
using RimWeave.Health;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Health;

public sealed class ContributionAdditionTests
{
    [Fact]
    public void Add_NullArguments_AreRejectedBeforeMutation()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);

        Assert.Throws<ArgumentNullException>(() => ContributionOperations.Add(null!, effect, null));
        Assert.Throws<ArgumentNullException>(() => ContributionOperations.Add(pawn, null!, null));
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Equal(0, effect.AddCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_WrongOrMissingOwner_DoesNotReassignTheInstance(bool missingOwner)
    {
        var requested = CreatePawn();
        var owner = missingOwner ? null : CreatePawn();
        var effect = Prepare(owner!);

        var result = ContributionOperations.Add(requested, effect, null);

        Assert.Equal(ContributionAdditionStatus.PawnMismatch, result.Status);
        Assert.Same(owner, result.ObservedPawn);
        Assert.Same(owner, effect.pawn);
        Assert.Empty(requested.health.hediffSet.hediffs);
        Assert.Equal(0, effect.AddCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Add_InconsistentPreparedPart_DoesNotRetargetTheInstance(int scenario)
    {
        var left = new BodyPartRecord();
        var right = new BodyPartRecord();
        var pawn = CreatePawn(left, right);
        var preparedPart = scenario == 0 ? null : left;
        var requestedPart = scenario == 1 ? null : right;
        var effect = Prepare(pawn, preparedPart);

        var result = ContributionOperations.Add(pawn, effect, requestedPart);

        Assert.Equal(ContributionAdditionStatus.PartMismatch, result.Status);
        Assert.Same(requestedPart, result.Part);
        Assert.Same(preparedPart, result.ObservedPart);
        Assert.Same(preparedPart, effect.Part);
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Equal(0, effect.AddCalls);
    }

    [Fact]
    public void Add_ForeignBodyPart_IsRejectedEvenWithTheSamePartDefinition()
    {
        var definition = new BodyPartDef();
        var part = new BodyPartRecord { def = definition };
        var foreignPart = new BodyPartRecord { def = definition };
        var pawn = CreatePawn(part);
        var effect = Prepare(pawn, foreignPart);

        Assert.Equal(ContributionAdditionStatus.InvalidPart,
            ContributionOperations.Add(pawn, effect, foreignPart).Status);
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Equal(0, effect.AddCalls);
    }

    [Fact]
    public void Add_MissingPart_DoesNotReuseAnIdenticalContributionOnTheOtherHand()
    {
        var definition = new BodyPartDef();
        var part = new BodyPartRecord { def = definition };
        var otherHand = new BodyPartRecord { def = definition };
        var pawn = CreatePawn(part, otherHand);
        var missing = new Hediff_MissingPart { pawn = pawn, Part = part };
        pawn.health.hediffSet.hediffs.Add(missing);
        var identity = new ContributionId("example.provider", "hand-state");
        var independent = ContributionTestData.AttachForLookup(pawn, identity, otherHand);
        var effect = Prepare(pawn, part);
        effect.Metadata.AssignIdentity(identity);

        var result = ContributionOperations.Add(pawn, effect, part);

        Assert.Equal(ContributionAdditionStatus.MissingPart, result.Status);
        Assert.Same(effect, result.Hediff);
        Assert.Same(part, result.ObservedPart);
        Assert.Same(otherHand, independent.Part);
        Assert.Equal(identity, independent.Metadata.Identity);
        Assert.Equal(new Hediff[] { missing, independent }, pawn.health.hediffSet.hediffs);
        Assert.Equal(ContributionLookupStatus.NotPresent,
            ContributionQueries.Resolve(pawn, identity, part).Status);
        Assert.Same(independent, Assert.Single(ContributionQueries.Resolve(pawn, identity, otherHand).Matches));
        Assert.Equal(0, effect.AddCalls);
    }

    [Fact]
    public void Add_WholeBodyWithDefaultPart_DoesNotChooseAnImplicitTarget()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);
        effect.def.defaultInstallPart = new BodyPartDef();

        Assert.Equal(ContributionAdditionStatus.IncompatibleTarget,
            ContributionOperations.Add(pawn, effect, null).Status);
        Assert.Null(effect.Part);
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Equal(0, effect.AddCalls);
    }

    [Fact]
    public void Add_MissingDefinition_IsRejectedBeforeTheNativeCall()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);
        effect.def = null;

        Assert.Equal(ContributionAdditionStatus.InvalidDefinition,
            ContributionOperations.Add(pawn, effect, null).Status);
        Assert.Empty(pawn.health.hediffSet.hediffs);
        Assert.Equal(0, effect.AddCalls);
    }

    [Fact]
    public void Add_AlreadyAttached_DoesNotRepeatCallbacksOrResetAge()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);
        effect.ageTicks = 123;
        pawn.health.hediffSet.hediffs.Add(effect);

        var result = ContributionOperations.Add(pawn, effect, null);

        Assert.Equal(ContributionAdditionStatus.AlreadyPresent, result.Status);
        Assert.Same(pawn, result.Pawn);
        Assert.Same(effect, result.Hediff);
        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        Assert.Equal(123, effect.ageTicks);
        Assert.Equal(0, effect.AddCalls);
    }

    [Fact]
    public void Add_ReentrantBeforeAttachment_DoesNotReenterNativeMergeCallbacks()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);
        var failure = new InvalidOperationException("Stop before native attachment.");
        ContributionAdditionResult? nested = null;
        var observer = new MergeObserver { pawn = pawn };
        observer.OnMerge = _ =>
        {
            if (observer.Calls == 1)
                nested = ContributionOperations.Add(pawn, effect, null);
            throw failure;
        };
        pawn.health.hediffSet.hediffs.Add(observer);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(
            () => ContributionOperations.Add(pawn, effect, null)));

        Assert.NotNull(nested);
        Assert.Equal(ContributionAdditionStatus.AlreadyAdding, nested.Status);
        Assert.Equal(1, observer.Calls);
        Assert.Equal(0, effect.AddCalls);
        Assert.Same(observer, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void Add_MergeCallbackException_ReleasesGuardForRetry()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);
        var failure = new InvalidOperationException("Stop before native attachment.");
        var observer = new MergeObserver { pawn = pawn, OnMerge = _ => throw failure };
        pawn.health.hediffSet.hediffs.Add(observer);

        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(
                () => ContributionOperations.Add(pawn, effect, null)));

        Assert.Equal(2, observer.Calls);
        Assert.Equal(0, effect.AddCalls);
        Assert.Same(observer, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void Add_PostAddException_PreservesPartialAttachmentAndReleasesGuard()
    {
        var pawn = CreatePawn();
        var effect = Prepare(pawn);
        var failure = new InvalidOperationException("Stop after native insertion.");
        effect.OnAdd = () => throw failure;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(
            () => ContributionOperations.Add(pawn, effect, null)));

        Assert.Same(effect, Assert.Single(pawn.health.hediffSet.hediffs));
        Assert.Equal(1, effect.AddCalls);
        Assert.Equal(ContributionAdditionStatus.AlreadyPresent,
            ContributionOperations.Add(pawn, effect, null).Status);
        Assert.Equal(1, effect.AddCalls);

        // Fixture cleanup only: a real consumer must use native removal and handle its effects.
        pawn.health.hediffSet.hediffs.Clear();
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(
            () => ContributionOperations.Add(pawn, effect, null)));
        Assert.Equal(2, effect.AddCalls);
    }

    [Fact]
    public void Add_NestedValueEqualInstances_RemainIndependent()
    {
        var pawn = CreatePawn();
        var first = new ValueEqualContribution { pawn = pawn, def = new HediffDef() };
        var second = new ValueEqualContribution { pawn = pawn, def = new HediffDef() };
        var firstFailure = new InvalidOperationException("Stop outer native insertion.");
        var secondFailure = new InvalidOperationException("Stop nested native insertion.");
        var offered = new List<Hediff>();
        var observer = new MergeObserver
        {
            pawn = pawn,
            OnMerge = candidate =>
            {
                offered.Add(candidate);
                if (ReferenceEquals(candidate, second)) throw secondFailure;
                Assert.Same(secondFailure, Assert.Throws<InvalidOperationException>(
                    () => ContributionOperations.Add(pawn, second, null)));
                throw firstFailure;
            }
        };
        pawn.health.hediffSet.hediffs.Add(observer);

        Assert.Same(firstFailure, Assert.Throws<InvalidOperationException>(
            () => ContributionOperations.Add(pawn, first, null)));
        Assert.Equal(2, observer.Calls);
        Assert.Same(first, offered[0]);
        Assert.Same(second, offered[1]);
        Assert.Same(observer, Assert.Single(pawn.health.hediffSet.hediffs));
    }

    [Fact]
    public void Add_ValueEqualAttachedInstance_DoesNotPreventANewInstance()
    {
        var pawn = CreatePawn();
        var existing = new ValueEqualContribution { pawn = pawn, def = new HediffDef() };
        var effect = new ValueEqualContribution { pawn = pawn, def = new HediffDef() };
        var failure = new InvalidOperationException("Stop after inserting the distinct instance.");
        effect.OnAdd = () => throw failure;
        pawn.health.hediffSet.hediffs.Add(existing);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(
            () => ContributionOperations.Add(pawn, effect, null)));
        Assert.Equal(2, pawn.health.hediffSet.hediffs.Count);
        Assert.Same(effect, pawn.health.hediffSet.hediffs[1]);
        Assert.Equal(1, effect.AddCalls);
    }

    [Fact]
    public void Add_ResultCapturesObservedFieldsBeforeSubsequentChanges()
    {
        var pawn = CreatePawn();
        var other = CreatePawn();
        var part = new BodyPartRecord();
        var effect = Prepare(other, part);

        var result = ContributionOperations.Add(pawn, effect, null);
        effect.pawn = pawn;
        effect.Part = null;

        Assert.Equal(ContributionAdditionStatus.PawnMismatch, result.Status);
        Assert.Same(other, result.ObservedPawn);
        Assert.Same(part, result.ObservedPart);
        Assert.Same(pawn, result.Pawn);
        Assert.Null(result.Part);
    }

    private static Pawn CreatePawn(params BodyPartRecord[] parts)
    {
        var pawn = HealthTestPawns.CreateForGuardChecks();
        pawn.RaceProps.body = new BodyDef();
        pawn.RaceProps.body.AllParts.AddRange(parts);
        return pawn;
    }

    private static ObservedContribution Prepare(Pawn pawn, BodyPartRecord? part = null) =>
        new() { pawn = pawn, Part = part, def = new HediffDef() };

    private class ObservedContribution : ContributionHediff
    {
        internal int AddCalls { get; private set; }
        internal Action? OnAdd { get; set; }

        public override void PostAdd(DamageInfo? dinfo)
        {
            AddCalls++;
            OnAdd?.Invoke();
            throw new InvalidOperationException("Guard fixtures cannot complete native health mutations.");
        }
    }

    private sealed class ValueEqualContribution : ObservedContribution
    {
        public override bool Equals(object? obj) => obj is ValueEqualContribution;
        public override int GetHashCode() => 1;
    }

    private sealed class MergeObserver : Hediff
    {
        internal int Calls { get; private set; }
        internal Func<Hediff, bool>? OnMerge { get; set; }

        public override bool TryMergeWith(Hediff other)
        {
            Calls++;
            return OnMerge?.Invoke(other) ?? false;
        }
    }
}
