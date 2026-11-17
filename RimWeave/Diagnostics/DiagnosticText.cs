using System.Globalization;
using System.Text;

namespace RimWeave.Diagnostics;

internal static class DiagnosticText
{
    internal const int MaxFieldLength = 120;
    internal const int MaxReportLength = 16000;
    private const string TruncatedReport =
        "\n[REPORT TRUNCATED: incomplete diagnostic. Omitted content and conclusions are not displayed.]\n";

    internal static string Field(string? value)
    {
        if (value is null) return "not declared";
        var result = new StringBuilder();
        foreach (char character in value.AsSpan(0, Math.Min(value.Length, MaxFieldLength)))
        {
            // Keep provider text on one line and prevent native rich-text markup or hidden controls.
            if (character == '\\') result.Append("\\\\");
            else if (character is '<' or '>' || char.IsControl(character)
                || char.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.LineSeparator
                    or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate)
                result.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            else result.Append(character);
        }
        if (value.Length > MaxFieldLength) result.Append(" [field truncated]");
        return result.ToString();
    }

    internal static bool Append(StringBuilder report, string text, string completeDiagnostic)
    {
        int remaining = MaxReportLength - TruncatedReport.Length - report.Length;
        if (text.Length <= remaining)
        {
            report.Append(text);
            return true;
        }
        report.Append(text.AsSpan(0, Math.Max(0, remaining)));
        report.Append(TruncatedReport);
        // A clipped display must not retain the complete-report headline. Field text cannot inject newlines.
        int headline = report.ToString().IndexOf("\n" + completeDiagnostic + "\n", StringComparison.Ordinal);
        if (headline >= 0)
            report.Remove(headline + 1, completeDiagnostic.Length)
                .Insert(headline + 1, "Diagnostic: INCOMPLETE (report text limit reached).");
        return false;
    }
}
