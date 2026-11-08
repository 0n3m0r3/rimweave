using System.Buffers.Binary;
using System.Text;
using System.Xml;
using RimWeave.Contributions;
using Verse;

namespace RimWeave.Persistence;

/// <summary>Persists shared contribution metadata inside an owner's native Scribe node.</summary>
/// <remarks>
/// The owner calls ExposeData once in each native save/load phase and exposes Metadata to consumers.
/// This adapter does not save the owner, resolve sources or apply effects. Unreadable metadata is
/// retained as opaque XML text on subsequent saves; it requires an explicit future recovery policy.
/// </remarks>
public sealed class ContributionMetadataScribe : IExposable
{
    private const string NodeName = "rimweaveContribution";
    private const string CurrentVersion = "1";
    private const string StringEncoding = "utf16le-base64";
    private static readonly HashSet<string> FieldNames =
    [
        "version", "encoding", "ownerId", "localId", "exclusiveResponsibility",
        "originNamespace", "originLocalId", "sourceReference", "loadError", "unreadableXml"
    ];

    private string? _unreadableXml;

    /// <summary>The current metadata, replaced during LoadingVars rather than merged with defaults.</summary>
    public ContributionMetadata Metadata { get; private set; } = new();

    /// <summary>Reads or writes the owner's metadata without performing a gameplay operation.</summary>
    public void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            Load();
        else if (Scribe.mode == LoadSaveMode.Saving)
            Save();
    }

    private void Save()
    {
        if (Metadata.LoadError is null && Metadata.Identity is null
            && Metadata.ExclusiveResponsibility is null && Metadata.Origin is null)
            return;

        if (!Scribe.EnterNode(NodeName))
            throw new InvalidOperationException("Could not save contribution metadata.");

        try
        {
            WriteScalar("version", CurrentVersion);
            WriteScalar("encoding", StringEncoding);
            if (Metadata.LoadError is not null)
            {
                // Keep the original payload even when this version cannot interpret it.
                Write("loadError", Metadata.LoadError);
                Write("unreadableXml", _unreadableXml);
                return;
            }

            Write("ownerId", Metadata.Identity?.OwnerId);
            Write("localId", Metadata.Identity?.LocalId);
            Write("exclusiveResponsibility", Metadata.ExclusiveResponsibility);
            Write("originNamespace", Metadata.Origin?.Type.NamespaceId);
            Write("originLocalId", Metadata.Origin?.Type.LocalId);
            Write("sourceReference", Metadata.Origin?.SourceReference);
        }
        finally
        {
            Scribe.ExitNode();
        }
    }

    private void Load()
    {
        Metadata = new ContributionMetadata();
        _unreadableXml = null;
        var nodes = Scribe.loader.curXmlParent.ChildNodes.OfType<XmlElement>()
            .Where(node => node.Name == NodeName).ToArray();
        if (nodes.Length == 0)
            return; // Older saves and undeclared metadata have no attribution to recover.

        string originalXml = PreserveXml(nodes);
        string? shapeError = nodes.Length == 1
            ? ValidateShape(nodes[0])
            : "Multiple contribution metadata nodes were found.";
        if (shapeError is not null)
        {
            RetainUnreadable(originalXml, shapeError);
            return;
        }

        if (!Scribe.EnterNode(NodeName))
        {
            RetainUnreadable(originalXml, "The contribution metadata node could not be read.");
            return;
        }

        try
        {
            if (ReadScalar("version") != CurrentVersion || ReadScalar("encoding") != StringEncoding)
            {
                RetainUnreadable(originalXml, "The contribution metadata version or string encoding is missing or unsupported.");
                return;
            }

            string? error = Read("loadError");
            string? retained = Read("unreadableXml");
            if (nodes[0]["loadError"] is not null || nodes[0]["unreadableXml"] is not null)
            {
                if (string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(retained)
                    || nodes[0].ChildNodes.OfType<XmlElement>().Count() != 4)
                    RetainUnreadable(originalXml, "The retained contribution metadata is malformed.");
                else
                    RetainUnreadable(retained, error);
                return;
            }

            // Publish only a fully validated set; never expose partial attribution as reliable.
            var restored = new ContributionMetadata();
            string? ownerId = Read("ownerId");
            string? localId = Read("localId");
            if (ownerId is not null || localId is not null)
                restored.AssignIdentity(new ContributionId(ownerId!, localId!));

            string? responsibility = Read("exclusiveResponsibility");
            if (responsibility is not null)
                restored.ClaimExclusiveResponsibility(responsibility);

            string? originNamespace = Read("originNamespace");
            string? originLocalId = Read("originLocalId");
            string? source = Read("sourceReference");
            if (originNamespace is not null || originLocalId is not null || source is not null)
                restored.DescribeOrigin(new ContributionOrigin(
                    new ContributionOriginType(originNamespace!, originLocalId!), source));

            Metadata = restored;
        }
        catch (ArgumentException error)
        {
            RetainUnreadable(originalXml, $"A contribution metadata value is invalid: {error.ParamName}.");
        }
        catch (FormatException)
        {
            RetainUnreadable(originalXml, "A contribution metadata string has an invalid encoding.");
        }
        finally
        {
            Scribe.ExitNode();
        }
    }

    private static string? ValidateShape(XmlElement node)
    {
        if (node.HasAttributes)
            return "Unexpected attributes on the contribution metadata node.";

        var seen = new HashSet<string>();
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child is not XmlElement field)
            {
                if (child is XmlComment || string.IsNullOrWhiteSpace(child.Value))
                    continue;
                return "Unexpected content in the contribution metadata node.";
            }

            if (!FieldNames.Contains(field.Name) || !seen.Add(field.Name))
                return "Unexpected or duplicate contribution metadata fields.";

            if (field.ChildNodes.OfType<XmlElement>().Any()
                || field.Attributes.Cast<XmlAttribute>().Any(attribute =>
                    attribute.Name != "IsNull" || attribute.Value != "True")
                || (field.HasAttribute("IsNull") && field.HasChildNodes))
                return $"The contribution metadata field {field.Name} has an unsupported structure.";
        }

        return null;
    }

    private void RetainUnreadable(string xml, string error)
    {
        _unreadableXml = xml;
        Metadata = ContributionMetadata.Unavailable(error);
    }

    private static string PreserveXml(IEnumerable<XmlElement> nodes)
    {
        var text = new StringBuilder();
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings
        {
            ConformanceLevel = ConformanceLevel.Fragment,
            OmitXmlDeclaration = true,
            NewLineHandling = NewLineHandling.Entitize
        }))
        {
            foreach (var node in nodes)
                node.WriteTo(writer);
        }
        return text.ToString();
    }

    // XML normalizes CR and cannot represent every .NET string. Preserve UTF-16 code units,
    // including isolated surrogates, without an encoder's replacement-character fallback.
    private static void Write(string label, string? value)
    {
        if (value is null)
            return;
        var bytes = new byte[value.Length * 2];
        for (int index = 0; index < value.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), value[index]);
        WriteScalar(label, Convert.ToBase64String(bytes));
    }

    private static string? Read(string label)
    {
        string? encoded = ReadScalar(label);
        if (encoded is null)
            return null;
        byte[] bytes = Convert.FromBase64String(encoded);
        if (bytes.Length % 2 != 0)
            throw new FormatException("An encoded UTF-16 string must contain complete code units.");
        var characters = new char[bytes.Length / 2];
        for (int index = 0; index < characters.Length; index++)
            characters[index] = (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2, 2));
        return new string(characters);
    }

    private static void WriteScalar(string label, string? value) => Scribe_Values.Look(ref value, label);

    private static string? ReadScalar(string label)
    {
        string? value = null;
        Scribe_Values.Look(ref value, label);
        return value;
    }
}
