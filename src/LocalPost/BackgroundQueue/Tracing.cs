using System.Diagnostics;
using System.Reflection;

namespace LocalPost.BackgroundQueue;

internal static class Tracing
{
    public static readonly ActivitySource Source;

    public static bool IsEnabled => Source.HasListeners();

    static Tracing()
    {
        // See https://stackoverflow.com/a/909583/322079
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        Source = new ActivitySource(assembly.FullName, version.ToString());
    }
}
