using System.Runtime.CompilerServices;
using Verse;

namespace RimWeave.Tests.Helpers;

internal sealed class NativeGameTestScope : IDisposable
{
    private readonly Game? _previous = Current.Game;
    internal Game Game { get; }

    internal NativeGameTestScope()
    {
        // Supply only a native game identity and component container, not a loaded colony.
        Game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        Game.components = [];
        Current.Game = Game;
    }

    public void Dispose() => Current.Game = _previous;
}
