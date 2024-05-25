using System.Diagnostics;
using System.Reflection;

namespace LocalPost;

internal static class BackgroundActivitySource
{
    public static readonly ActivitySource Source;

    public static bool IsEnabled => Source.HasListeners();

    static BackgroundActivitySource()
    {
        // See https://stackoverflow.com/a/909583/322079
        var assembly = Assembly.GetExecutingAssembly();
        var version = AssemblyName.GetAssemblyName(assembly.Location).Version;
        Source = new System.Diagnostics.ActivitySource(assembly.FullName, version.ToString());
    }

    // public static Activity? StartProcessing<T>(ConsumeContext<T> context)
    // {
    //     var activity = Source.CreateActivity($"{context.Client.Topic} process", ActivityKind., ac);
    //
    //     return activity;
    // }
}
