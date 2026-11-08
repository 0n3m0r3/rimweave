using Verse;
using RimWeave.Logging;

namespace RimWeave;

/// <summary>The native mod entry point; loading emits a diagnostic without applying gameplay effects.</summary>
public sealed class RimWeaveMod : Mod
{
    /// <summary>Initializes the framework when RimWorld loads its content pack.</summary>
    /// <param name="content">The content pack supplied by the native mod loader.</param>
    public RimWeaveMod(ModContentPack content) : base(content)
    {
        RimWeaveLog.Message("RimWeaveMod loaded!");
    }
}
