using System.Diagnostics;
using System.Reflection;
using System.Text;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

internal static class MessageUtils
{
    public static void ExtractTraceFieldFromHeaders(object? carrier, string fieldName,
        out string? fieldValue, out IEnumerable<string>? fieldValues)
    {
        fieldValues = default;
        fieldValue = null;
        if (carrier is not IEnumerable<IHeader> message)
            return;

        var headerValue = message.FirstOrDefault(header => header.Key == fieldName)?.GetValueBytes();
        if (headerValue is not null)
            fieldValue = Encoding.UTF8.GetString(headerValue);
    }
}

internal static class KafkaActivityExtensions
{
    public static void AcceptDistributedTracingFrom<TKey, TValue>(this Activity activity, Message<TKey, TValue> message)
    {
        var propagator = DistributedContextPropagator.Current;
        propagator.ExtractTraceIdAndState(message.Headers, MessageUtils.ExtractTraceFieldFromHeaders,
            out var traceParent, out var traceState);

        if (string.IsNullOrEmpty(traceParent))
            return;
        activity.SetParentId(traceParent!);
        if (!string.IsNullOrEmpty(traceState))
            activity.TraceStateString = traceState;

        var baggage = propagator.ExtractBaggage(message.Headers, MessageUtils.ExtractTraceFieldFromHeaders);
        if (baggage is null)
            return;
        foreach (var baggageItem in baggage)
            activity.AddBaggage(baggageItem.Key, baggageItem.Value);
    }

    public static Activity? SetDefaultTags(this Activity? activity, KafkaTopicClient client)
    {
        // See https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#messaging-attributes
        activity?.SetTag("messaging.system", "kafka");

        activity?.SetTag("messaging.destination.name", client.Topic);
        activity?.SetTag("messaging.kafka.consumer.group", client.GroupId);

        // activity?.SetTag("messaging.client_id", "service_name");
        // activity?.SetTag("server.address", client.QueueUrl.Host);
        // activity?.SetTag("server.port", client.QueueUrl.Port);

        return activity;
    }

    public static Activity? SetTagsFor<T>(this Activity? activity, ConsumeContext<T> context)
    {
        // activity?.SetTag("messaging.message.id", context.MessageId);
        activity?.SetTag("messaging.kafka.message.offset", context.NextOffset.Offset.Value);

        // Skip, as we always ignore the key on consumption
        // activity.SetTag("messaging.kafka.message.key", context.Message.Key);

        return activity;
    }

    public static Activity? SetTagsFor<T>(this Activity? activity, BatchConsumeContext<T> context) =>
        activity?.SetTag("messaging.batch.message_count", context.Messages.Count);
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
        var activity = Source.CreateActivity($"{context.Client.Topic} process", ActivityKind.Consumer);
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
        var activity = Source.StartActivity($"{context.Client.Topic} process", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetDefaultTags(context.Client);
        activity.SetTagsFor(context);

        // TODO Accept distributed tracing headers, per each message...

        return activity;
    }
}
