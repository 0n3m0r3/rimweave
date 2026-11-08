using RimWeave.Logging;
using Verse;

namespace RimWeave.Example.Health;

public abstract class HediffComp_ExampleObserver : HediffComp
{
#if DEBUG
    internal int RemovalCallbackCount { get; private set; }
#endif

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        WriteCallback("added");
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
#if DEBUG
        RemovalCallbackCount++;
#endif
        WriteCallback("removed");
    }

    public override void CompPostMerged(Hediff other)
    {
        base.CompPostMerged(other);
        WriteCallback("merged");
    }

    private void WriteCallback(string callback)
    {
        int slot = parent.pawn.RaceProps.body.AllParts.IndexOf(parent.Part);
        bool attached = parent.pawn.health.hediffSet.hediffs.Contains(parent);

        RimWeaveLog.Message(
            $"Example observer={GetType().Name}, callback={callback}, " +
            $"pawn={parent.pawn.ThingID}, slot={slot}, attached={attached}");
    }
}

public sealed class HediffComp_FirstObserver : HediffComp_ExampleObserver
{
}

public sealed class HediffComp_SecondObserver : HediffComp_ExampleObserver
{
}
