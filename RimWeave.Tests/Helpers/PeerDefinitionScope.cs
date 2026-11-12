using System.Xml;
using System.Runtime.CompilerServices;
using RimWeave.Health;
using Verse;

namespace RimWeave.Tests.Helpers;

internal sealed class PeerDefinitionScope : IDisposable
{
    private readonly HediffDef[] _previous = DefDatabase<HediffDef>.AllDefs.ToArray();
    private readonly ModContentPack _framework;
    private readonly bool _profilerEnabled = DeepProfiler.enabled;
    internal HediffDef Definition { get; }

    internal PeerDefinitionScope()
    {
        // Native XML resolves custom classes through active mod assemblies, not the test host's assemblies.
        _framework = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
        _framework.assemblies = new ModAssemblyHandler(_framework);
        _framework.assemblies.loadedAssemblies.Add(typeof(ContributionHediff).Assembly);
        LoadedModManager.RunningModsListForReading.Add(_framework);
        GenTypes.ClearCache();
        DeepProfiler.enabled = false;
        try
        {
            var xml = new XmlDocument();
            xml.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PeerComposition.xml"));
            Definition = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement!.FirstChild!, false);
            if (Definition.modExtensions?.Count != 1)
                throw new InvalidOperationException("Peer declaration did not load: "
                    + string.Join("\n", Log.Messages.Select(message => message.text)));
            Definition.ResolveReferences();
            DefDatabase<HediffDef>.Add(Definition);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        DefDatabase<HediffDef>.Clear();
        DefDatabase<HediffDef>.Add(_previous);
        LoadedModManager.RunningModsListForReading.Remove(_framework);
        GenTypes.ClearCache();
        DeepProfiler.enabled = _profilerEnabled;
    }
}
