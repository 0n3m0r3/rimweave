using Verse;

namespace RimWeave.Logging;

public static class RimWeaveLog
{
    public static void Message(string message)
    {
        Log.Message(LogMessageFormatter.Format(message));
    }
}