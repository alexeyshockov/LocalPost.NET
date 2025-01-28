using System.Diagnostics;
using System.Reflection;

namespace LocalPost.BackgroundQueue;

internal static class Tracing
{
    public static readonly ActivitySource Source;

    public static bool IsEnabled => Source.HasListeners();

    static Tracing()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        Source = new ActivitySource("LocalPost.BackgroundQueue", version);
    }
}
