using System.Text;
using System.Xml;
using System.Xml.Linq;
using RimWeave.Contributions;
using RimWeave.Persistence;
using RimWeave.Tests.Helpers;
using Xunit;

namespace RimWeave.Tests.Persistence;

[Collection(ScribeTestCollection.Name)]
public sealed class ContributionMetadataScribeTests
{
    [Theory]
    [InlineData("opaque:Event/AbC?x=<7>&y=é")]
    [InlineData("before\rafter")]
    [InlineData("before\r\nafter")]
    [InlineData("before\n\tafter")]
    [InlineData("before\0\vafter")]
    public void RoundTrip_PreservesEveryStringValueExactly(string value)
    {
        using var scribe = new ScribeTestScope();
        var original = new ContributionMetadataScribe();
        original.Metadata.AssignIdentity(new ContributionId(value, value));
        original.Metadata.ClaimExclusiveResponsibility(value);
        original.Metadata.DescribeOrigin(new ContributionOrigin(new ContributionOriginType(value, value), value));

        var loaded = scribe.RoundTrip(original);
        loaded = scribe.RoundTrip(loaded);

        Assert.Equal(original.Metadata.Identity, loaded.Metadata.Identity);
        Assert.Equal(value, loaded.Metadata.ExclusiveResponsibility);
        Assert.Equal(original.Metadata.Origin, loaded.Metadata.Origin);
        Assert.Null(loaded.Metadata.LoadError);
    }

    [Fact]
    public void RoundTrip_PreservesIsolatedUtf16SurrogatesWithoutReplacement()
    {
        // Construct at runtime: a test runner may itself serialize InlineData strings as Unicode.
        RoundTrip_PreservesEveryStringValueExactly("before" + (char)0xD800 + "after" + (char)0xDC00);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTrip_PreservesOriginWithoutRequiringIdentityOrSource(bool explicitUnknown)
    {
        using var scribe = new ScribeTestScope();
        var original = new ContributionMetadataScribe();
        var type = explicitUnknown ? ContributionOriginType.Unknown : new ContributionOriginType("example.types", "custom");
        original.Metadata.DescribeOrigin(new ContributionOrigin(type));

        var loaded = scribe.RoundTrip(original);

        Assert.Null(loaded.Metadata.Identity);
        Assert.Equal(type, loaded.Metadata.Origin!.Type);
        Assert.Null(loaded.Metadata.Origin.SourceReference);
        Assert.Null(loaded.Metadata.LoadError);
    }

    [Fact]
    public void RoundTrip_PreservesAnIndependentClaimWithoutInventingAnOriginOrIdentity()
    {
        using var scribe = new ScribeTestScope();
        var original = new ContributionMetadataScribe();
        original.Metadata.ClaimExclusiveResponsibility("example/claim");

        var loaded = scribe.RoundTrip(original);

        Assert.Equal("example/claim", loaded.Metadata.ExclusiveResponsibility);
        Assert.Null(loaded.Metadata.Identity);
        Assert.Null(loaded.Metadata.Origin);
        Assert.Null(loaded.Metadata.LoadError);
    }

    [Fact]
    public void EmptyMetadata_AddsNoNodeAndLoadsAsNormallyAbsent()
    {
        using var scribe = new ScribeTestScope();
        string xml = scribe.Save(new ContributionMetadataScribe());
        Assert.Empty(XDocument.Parse(xml).Descendants("rimweaveContribution"));

        var loaded = scribe.Load<ContributionMetadataScribe>(xml);

        Assert.Null(loaded.Metadata.Identity);
        Assert.Null(loaded.Metadata.ExclusiveResponsibility);
        Assert.Null(loaded.Metadata.Origin);
        Assert.Null(loaded.Metadata.LoadError);
        loaded.Metadata.AssignIdentity(new ContributionId("example.owner", "new"));
    }

    [Fact]
    public void ExplicitNullFields_DoNotBecomeEmptyStringsOrErrors()
    {
        using var scribe = new ScribeTestScope();
        var node = ValidNode();
        node.Add(new XElement("ownerId", new XAttribute("IsNull", "True")),
            new XElement("localId", new XAttribute("IsNull", "True")),
            new XElement("sourceReference", new XAttribute("IsNull", "True")));

        var loaded = LoadNode(scribe, node);

        Assert.Null(loaded.Metadata.Identity);
        Assert.Null(loaded.Metadata.Origin);
        Assert.Null(loaded.Metadata.LoadError);
    }

    [Theory]
    [InlineData("missing-version")]
    [InlineData("future-version")]
    [InlineData("unknown-encoding")]
    [InlineData("partial-identity")]
    [InlineData("partial-origin")]
    [InlineData("source-without-origin")]
    [InlineData("invalid-value")]
    [InlineData("invalid-base64")]
    [InlineData("partial-code-unit")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-field")]
    [InlineData("nested-field")]
    [InlineData("unexpected-attribute")]
    [InlineData("empty-quarantine")]
    public void UnreadableMetadata_RemainsUnavailableAndRetainsItsPayloadAcrossSaves(string damage)
    {
        using var scribe = new ScribeTestScope();
        var node = ValidNode();
        node.Add(Encoded("ownerId", "example.owner"), Encoded("localId", "marker"));
        switch (damage)
        {
            case "missing-version": node.Element("version")!.Remove(); break;
            case "future-version": node.Element("version")!.Value = "2"; break;
            case "unknown-encoding": node.Element("encoding")!.Value = "future"; break;
            case "partial-identity": node.Element("localId")!.Remove(); break;
            case "partial-origin": node.Add(Encoded("originNamespace", "example.cause")); break;
            case "source-without-origin": node.Add(Encoded("sourceReference", "past:event/7")); break;
            case "invalid-value": node.Element("localId")!.ReplaceWith(Encoded("localId", " ")); break;
            case "invalid-base64": node.Element("localId")!.Value = "not-base64!"; break;
            case "partial-code-unit": node.Element("localId")!.Value = "YQ=="; break;
            case "unknown-field": node.Add(new XElement("future", new XAttribute("version", "2"), "a\rb")); break;
            case "duplicate-field": node.Add(Encoded("localId", "another")); break;
            case "nested-field": node.Element("localId")!.Add(new XElement("unexpected")); break;
            case "unexpected-attribute": node.Add(new XAttribute("future", "yes")); break;
            case "empty-quarantine": node.Add(new XElement("unreadableXml", new XAttribute("IsNull", "True"))); break;
        }

        var loaded = LoadNode(scribe, node);
        Assert.NotNull(loaded.Metadata.LoadError);
        Assert.Null(loaded.Metadata.Identity);
        Assert.Null(loaded.Metadata.ExclusiveResponsibility);
        Assert.Null(loaded.Metadata.Origin);
        string firstSave = scribe.Save(loaded);
        string retained = RetainedXml(firstSave);
        Assert.True(XNode.DeepEquals(node, XElement.Parse(retained)));

        var reloaded = scribe.Load<ContributionMetadataScribe>(firstSave);

        Assert.Equal(loaded.Metadata.LoadError, reloaded.Metadata.LoadError);
        Assert.Equal(retained, RetainedXml(scribe.Save(reloaded)));
        Assert.Throws<InvalidOperationException>(() =>
            reloaded.Metadata.AssignIdentity(new ContributionId("example.owner", "replacement")));
    }

    [Fact]
    public void DuplicateNodes_AreNotResolvedByXmlOrderAndBothAreRetained()
    {
        using var scribe = new ScribeTestScope();
        var first = ValidNode();
        first.Add(Encoded("ownerId", "example.a"), Encoded("localId", "marker"));
        var second = ValidNode();
        second.Add(Encoded("ownerId", "example.b"), Encoded("localId", "marker"));
        string xml = new XElement("test", new XElement("state", first, second)).ToString();

        var loaded = scribe.Load<ContributionMetadataScribe>(xml);

        Assert.NotNull(loaded.Metadata.LoadError);
        Assert.Null(loaded.Metadata.Identity);
        var retained = XElement.Parse("<retained>" + RetainedXml(scribe.Save(loaded)) + "</retained>");
        Assert.Equal(2, retained.Elements("rimweaveContribution").Count());
        Assert.True(XNode.DeepEquals(first, retained.Elements().First()));
        Assert.True(XNode.DeepEquals(second, retained.Elements().Last()));
    }

    private static XElement ValidNode() => new("rimweaveContribution",
        new XElement("version", "1"), new XElement("encoding", "utf16le-base64"));

    private static XElement Encoded(string label, string value) =>
        new(label, Convert.ToBase64String(Encoding.Unicode.GetBytes(value)));

    private static ContributionMetadataScribe LoadNode(ScribeTestScope scribe, XElement node)
    {
        var text = new StringBuilder();
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings
        {
            OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.Entitize
        }))
            new XElement("test", new XElement("state", node)).WriteTo(writer);
        return scribe.Load<ContributionMetadataScribe>(text.ToString());
    }

    private static string RetainedXml(string xml) => Encoding.Unicode.GetString(Convert.FromBase64String(
        Assert.Single(XDocument.Parse(xml).Descendants("unreadableXml")).Value));
}
