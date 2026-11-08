using Verse;

namespace RimWeave.Logging;

/// <summary>Writes framework diagnostics through RimWorld's native log.</summary>
public static class RimWeaveLog
{
    /// <summary>Writes a message with the RimWeave prefix on every normalized line.</summary>
    /// <param name="message">The non-null diagnostic text. Trailing line breaks are removed.</param>
    /// <remarks>This logger does not infer errors, log operation results automatically or open a console.</remarks>
    public static void Message(string message)
    {
        Log.Message(LogMessageFormatter.Format(message));
    }
}
