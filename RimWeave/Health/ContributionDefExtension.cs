using RimWeave.Contributions;
using Verse;

namespace RimWeave.Health;

/// <summary>Declares initial attribution in a cooperating health effect's native XML definition.</summary>
/// <remarks>
/// Add at most one instance to a HediffDef using ContributionHediff or a subclass.
/// Declarations affect newly created instances only; saved metadata remains authoritative.
/// Definitions may share an identity. This declaration neither registers nor applies an effect.
/// </remarks>
public sealed class ContributionDefExtension : DefModExtension
{
    /// <summary>The contribution owner's namespace, using the shared identity validation rules.</summary>
    public string ownerId = "";

    /// <summary>The owner's case-sensitive local contribution identifier.</summary>
    public string localId = "";

    /// <summary>An optional exclusive responsibility; null leaves the contribution unclaimed.</summary>
    public string? exclusiveResponsibility;

    private Def? _definition;

    /// <summary>Retains native definition context for configuration diagnostics without creating effects.</summary>
    /// <param name="parentDef">The definition owning this extension.</param>
    public override void ResolveReferences(Def parentDef)
    {
        base.ResolveReferences(parentDef);
        _definition = parentDef;
    }

    /// <summary>Reports an invalid declaration with its native definition and supplying mod.</summary>
    /// <returns>Configuration errors, or an empty sequence for a valid declaration.</returns>
    public override IEnumerable<string> ConfigErrors()
    {
        if (_definition is null)
            return ["Contribution declaration has no definition context; resolve native references first."];
        try
        {
            ReadDeclaration(_definition);
            return [];
        }
        catch (InvalidOperationException error)
        {
            return [error.Message];
        }
    }

    /// <summary>Reads a fresh metadata snapshot from an optional health definition declaration.</summary>
    /// <param name="definition">The native definition to inspect on the simulation thread.</param>
    /// <returns>Independent metadata, or null when the definition has no contribution declaration.</returns>
    /// <exception cref="ArgumentNullException">The definition is null.</exception>
    /// <exception cref="InvalidOperationException">The declaration is invalid, duplicated or incompatible.</exception>
    /// <remarks>
    /// Uses the same validation as native configuration diagnostics and effect creation.
    /// Editing the returned metadata does not change the definition or any other instance.
    /// Origin and source remain the consumer's responsibility. No gameplay operation is performed.
    /// </remarks>
    public static ContributionMetadata? Read(HediffDef definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ReadDeclaration(definition);
    }

    private static ContributionMetadata? ReadDeclaration(Def definition)
    {
        var declarations = definition.modExtensions?.OfType<ContributionDefExtension>().ToArray() ?? [];
        if (declarations.Length == 0)
            return null;
        if (declarations.Length != 1)
            throw Invalid(definition, "Only one contribution declaration is allowed per definition.");
        if (definition is not HediffDef health || health.hediffClass is null
            || !typeof(ContributionHediff).IsAssignableFrom(health.hediffClass))
            throw Invalid(definition, "The definition must use ContributionHediff or a subclass.");

        var declaration = declarations[0];
        try
        {
            var metadata = new ContributionMetadata();
            metadata.AssignIdentity(new ContributionId(declaration.ownerId, declaration.localId));
            if (declaration.exclusiveResponsibility is not null)
                metadata.ClaimExclusiveResponsibility(declaration.exclusiveResponsibility);
            return metadata;
        }
        catch (ArgumentException error)
        {
            throw Invalid(definition, error.Message, error);
        }
    }

    private static InvalidOperationException Invalid(Def definition, string reason, Exception? inner = null) =>
        new($"Contribution declaration on '{definition.defName}' "
            + $"(mod '{definition.modContentPack?.PackageId ?? "<unknown>"}'): {reason}", inner);
}
