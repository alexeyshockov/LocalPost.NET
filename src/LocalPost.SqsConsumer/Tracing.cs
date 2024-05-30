using System.Diagnostics;
using System.Reflection;
using Amazon.SQS.Model;

namespace LocalPost.SqsConsumer;

internal static class MessageUtils
{
    public static void ExtractTraceField(object? carrier, string fieldName,
        out string? fieldValue, out IEnumerable<string>? fieldValues)
    {
        fieldValues = default;
        fieldValue = null;
        if (carrier is not Message message)
            return;

        fieldValue = message.MessageAttributes.TryGetValue(fieldName, out var attribute)
            ? attribute.StringValue
            : null;
    }
}

internal static class SqsActivityExtensions
{
    public static void AcceptDistributedTracingFrom(this Activity activity, Message message)
    {
        var propagator = DistributedContextPropagator.Current;
        propagator.ExtractTraceIdAndState(message, MessageUtils.ExtractTraceField,
            out var traceParent, out var traceState);

        if (string.IsNullOrEmpty(traceParent))
            return;
        activity.SetParentId(traceParent!);
        if (!string.IsNullOrEmpty(traceState))
            activity.TraceStateString = traceState;

        var baggage = propagator.ExtractBaggage(message, MessageUtils.ExtractTraceField);
        if (baggage is null)
            return;
        foreach (var baggageItem in baggage)
            activity.AddBaggage(baggageItem.Key, baggageItem.Value);
    }

    public static void SetDefaultTags(this Activity? activity, QueueClient client)
    {
        // See https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#messaging-attributes
        activity?.SetTag("messaging.system", "sqs");

        activity?.SetTag("messaging.destination.name", client.QueueName);

        // activity?.SetTag("messaging.client_id", "service_name");
        // activity?.SetTag("server.address", client.QueueUrl.Host);
        // activity?.SetTag("server.port", client.QueueUrl.Port);
    }

    public static Activity? SetTagsFor<T>(this Activity? activity, ConsumeContext<T> context) =>
        activity?.SetTag("messaging.message.id", context.MessageId);

    public static Activity? SetTagsFor<T>(this Activity? activity, BatchConsumeContext<T> context) =>
        activity?.SetTag("messaging.batch.message_count", context.Count);

    public static Activity? SetTagsFor(this Activity? activity, ReceiveMessageResponse response) =>
        activity?.SetTag("messaging.batch.message_count", response.Messages.Count);
}

// Npgsql as an inspiration:
//  - https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlActivitySource.cs#LL61C31-L61C49
//  - https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlCommand.cs#L1639-L1644
// Also OTEL semantic convention: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
internal static class Tracing
{
    private static readonly ActivitySource Source;

    public static bool IsEnabled => Source.HasListeners();

    static Tracing()
    {
        // See https://stackoverflow.com/a/909583/322079
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        Source = new ActivitySource(assembly.FullName, version.ToString());
    }

    public static Activity? StartProcessing<T>(ConsumeContext<T> context)
    {
        var activity = Source.CreateActivity($"{context.Client.QueueName} process", ActivityKind.Consumer);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetDefaultTags(context.Client);
            activity.SetTagsFor(context);
            activity.AcceptDistributedTracingFrom(context.Message);
        }

        activity?.Start();

        return activity;
    }


    public static Activity? StartProcessing<T>(BatchConsumeContext<T> context)
    {
        var activity = Source.StartActivity($"{context.Client.QueueName} process", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetDefaultTags(context.Client);
        activity.SetTagsFor(context);

        // TODO Accept distributed tracing headers, per each message...

        return activity;
    }

    public static Activity? StartSettling<T>(ConsumeContext<T> context)
    {
        var activity = Source.StartActivity($"{context.Client.QueueName} settle", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetDefaultTags(context.Client);
        activity.SetTag("messaging.message.id", context.MessageId);

        return activity;
    }

    public static Activity? StartSettling<T>(BatchConsumeContext<T> context)
    {
        var activity = Source.StartActivity($"{context.Client.QueueName} settle", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetDefaultTags(context.Client);
        activity.SetTag("messaging.batch.message_count", context.Count);

        return activity;
    }

    public static Activity? StartReceiving(QueueClient client)
    {
        var activity = Source.StartActivity($"{client.QueueName} receive", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetDefaultTags(client);

        return activity;
    }
}
