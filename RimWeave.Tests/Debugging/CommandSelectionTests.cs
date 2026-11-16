#if DEBUG
using System.Reflection;
using RimWeave.Debugging;
using RimWeave.Tests.Helpers;
using Verse;
using Xunit;

namespace RimWeave.Tests.Debugging;

[Collection(ScribeTestCollection.Name)]
public sealed class CommandSelectionTests : IDisposable
{
    private static readonly PropertyInfo RandomState = typeof(Rand)
        .GetProperty("StateCompressed", BindingFlags.Static | BindingFlags.NonPublic)!;
    private readonly ulong _initialState = ReadState();

    public void Dispose() => RandomState.SetValue(null, _initialState);

    [Fact]
    public void Selection_RunsAfterPresentationAndRetainsItsSimulationDraw()
    {
        int calls = 0;
        ulong afterAction = 0;
        CommandSelection.Present(select =>
        {
            _ = Rand.Int;
            select(() =>
            {
                Assert.Equal(_initialState, ReadState());
                calls++;
                _ = Rand.Int;
                afterAction = ReadState();
            });
            Assert.Equal(0, calls);
            _ = Rand.Int;
        });

        Assert.Equal(1, calls);
        Assert.NotEqual(_initialState, afterAction);
        Assert.Equal(afterAction, ReadState());
        CommandSelection.Present(select => { _ = Rand.Int; });
        Assert.Equal(1, calls);
        Assert.Equal(afterAction, ReadState());
    }

    [Fact]
    public void NoSelection_LeavesSimulationRandomnessUnchanged()
    {
        CommandSelection.Present(select => { _ = Rand.Int; });

        Assert.Equal(_initialState, ReadState());
    }

    [Fact]
    public void FailedPresentation_DiscardsTheSelectionAndPropagatesTheOriginalError()
    {
        var failure = new InvalidOperationException("Presentation failed.");
        int calls = 0;

        var thrown = Assert.Throws<InvalidOperationException>(() => CommandSelection.Present(select =>
        {
            _ = Rand.Int;
            select(() => calls++);
            throw failure;
        }));

        Assert.Same(failure, thrown);
        Assert.Equal(0, calls);
        Assert.Equal(_initialState, ReadState());
    }

    [Fact]
    public void FailedAction_PreservesItsPartialEffectsAndDoesNotReplay()
    {
        var failure = new InvalidOperationException("Gameplay failed.");
        int calls = 0;
        ulong afterAction = 0;

        var thrown = Assert.Throws<InvalidOperationException>(() => CommandSelection.Present(select =>
            select(() =>
            {
                calls++;
                _ = Rand.Int;
                afterAction = ReadState();
                throw failure;
            })));

        Assert.Same(failure, thrown);
        Assert.Equal(afterAction, ReadState());
        Assert.NotEqual(_initialState, afterAction);
        CommandSelection.Present(select => { });
        Assert.Equal(1, calls);
        Assert.Equal(afterAction, ReadState());
    }

    private static ulong ReadState() => (ulong)RandomState.GetValue(null)!;
}
#endif
