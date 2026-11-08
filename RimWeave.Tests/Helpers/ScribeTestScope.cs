using Verse;
using Xunit;

namespace RimWeave.Tests.Helpers;

// Scribe, native definitions and the profiler are process-wide state.
[CollectionDefinition(ScribeTestCollection.Name, DisableParallelization = true)]
public sealed class ScribeTestCollection
{
    public const string Name = "Native Scribe";
}

internal sealed class ScribeTestScope : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rimweave-scribe-" + Guid.NewGuid());
    private readonly bool _profilerEnabled = DeepProfiler.enabled;

    internal ScribeTestScope()
    {
        Assert.Equal(LoadSaveMode.Inactive, Scribe.mode);
        Directory.CreateDirectory(_directory);
        // FinalizeLoading otherwise reads player preferences, which this test host does not load.
        DeepProfiler.enabled = false;
    }

    internal string Save<T>(T value) where T : class, IExposable
    {
        var path = Path.Combine(_directory, "save.xml");
        try
        {
            Scribe.saver.InitSaving(path, "test");
            Scribe_Deep.Look(ref value, "state");
            Scribe.saver.FinalizeSaving();
            return File.ReadAllText(path);
        }
        finally
        {
            Scribe.ForceStop();
        }
    }

    internal T Load<T>(string xml, params object[] constructorArguments) where T : class, IExposable
    {
        var path = Path.Combine(_directory, "load.xml");
        File.WriteAllText(path, xml);
        try
        {
            T? value = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref value, "state", constructorArguments);
            Scribe.loader.FinalizeLoading();
            return Assert.IsType<T>(value);
        }
        finally
        {
            Scribe.ForceStop();
        }
    }

    internal T RoundTrip<T>(T value, params object[] constructorArguments) where T : class, IExposable =>
        Load<T>(Save(value), constructorArguments);

    public void Dispose()
    {
        Scribe.ForceStop();
        DeepProfiler.enabled = _profilerEnabled;
        Directory.Delete(_directory, recursive: true);
    }
}
