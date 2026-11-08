namespace RimWeave.Contributions;

internal static class IdentifierRules
{
    internal static string NormalizeNamespace(string value, string parameterName) =>
        ValidatePart(value, parameterName).ToLowerInvariant();

    internal static string ValidatePart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value != value.Trim())
            throw new ArgumentException("Identifiers must not have surrounding whitespace.", parameterName);

        return value;
    }
}
