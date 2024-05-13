using System.Diagnostics;

namespace LocalPost;

internal static class ActivityEx
{
    // See https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.8.1/src/OpenTelemetry.Api/Trace/ActivityExtensions.cs#L81-L105
    public static Activity? Error(this Activity? activity, Exception ex, bool escaped = true)
    {
        var tags = new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped", escaped }
        };
        activity?.AddEvent(new ActivityEvent("exception", tags: tags));

        activity?.SetTag("otel.status_code", "ERROR");
        // activity.SetTag("otel.status_description", ex is PostgresException pgEx ? pgEx.SqlState : ex.Message);
        activity?.SetTag("otel.status_description", ex.Message);

        return activity;
    }

    public static Activity? Success(this Activity? activity) => activity?.SetTag("otel.status_code", "OK");
}
