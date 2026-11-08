using Verse;
using RimWeave.Logging;

namespace RimWeave;

public sealed class RimWeaveMod : Mod
{
    public RimWeaveMod(ModContentPack content) : base(content)
    {
        RimWeaveLog.Message("RimWeaveMod loaded!");
    }
}