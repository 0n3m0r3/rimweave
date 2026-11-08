namespace RimWeave.Logging;

internal static class LogMessageFormatter
{
    private const string Prefix = "[RimWeave] ";

    public static string Format(string message)
    {
        string text = message.ReplaceLineEndings("\n").TrimEnd('\n');
        return Prefix + text.Replace("\n", "\n" + Prefix);
    }
}