#if DEBUG
using System.Reflection;
using RimWeave.Debugging;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Debugging;

[Collection(ScribeTestCollection.Name)]
public sealed class InspectionPresentationTests : IDisposable
{
    private static readonly PropertyInfo RandomState = typeof(Rand)
        .GetProperty("StateCompressed", BindingFlags.Static | BindingFlags.NonPublic)!;
    private readonly ulong _initialState = ReadState();

    public void Dispose() => RandomState.SetValue(null, _initialState);

    [Fact]
    public void SuccessfulPresentation_PreservesTheStateAfterEarlierSimulationDraws()
    {
        _ = Rand.Int;
        ulong afterSimulation = ReadState();
        bool displayed = false;

        InspectionPresentation.PreserveRandomState(() =>
        {
            _ = Rand.Int;
            _ = Rand.Int;
            displayed = true;
        });

        Assert.True(displayed);
        Assert.NotEqual(_initialState, afterSimulation);
        Assert.Equal(afterSimulation, ReadState());
    }

    [Fact]
    public void FailedPresentation_RestoresRandomnessAndPropagatesTheOriginalError()
    {
        var failure = new InvalidOperationException("Presentation failed.");

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            InspectionPresentation.PreserveRandomState(() =>
            {
                _ = Rand.Int;
                throw failure;
            }));

        Assert.Same(failure, thrown);
        Assert.Equal(_initialState, ReadState());
    }

    [Fact]
    public void NestedPresentation_RestoresEachScopeWithoutUnbalancingTheOuterState()
    {
        InspectionPresentation.PreserveRandomState(() =>
        {
            _ = Rand.Int;
            ulong outerState = ReadState();

            InspectionPresentation.PreserveRandomState(() => _ = Rand.Int);

            Assert.Equal(outerState, ReadState());
            _ = Rand.Int;
        });

        Assert.Equal(_initialState, ReadState());
        _ = Rand.Int;
        Assert.NotEqual(_initialState, ReadState());
    }

    private static ulong ReadState() => (ulong)RandomState.GetValue(null)!;
}
#endif
