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
        fieldValues = null;
        fieldValue = null;
        if (carrier is not IEnumerable<IHeader> message)
            return;

        var headerValue = message.FirstOrDefault(header => header.Key == fieldName)?.GetValueBytes();
        if (headerValue is not null)
            fieldValue = Encoding.UTF8.GetString(headerValue);
    }
}

// See https://opentelemetry.io/docs/specs/semconv/messaging/kafka/
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

    public static Activity? SetDefaultTags(this Activity? activity, Client client)
    {
        // See https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#messaging-attributes
        activity?.SetTag("messaging.system", "kafka");

        // activity?.SetTag("messaging.kafka.consumer.group", context.ClientConfig.GroupId);
        activity?.SetTag("messaging.consumer.group.name", client.Config.GroupId);

        // activity?.SetTag("messaging.client.id", "service_name");
        // activity?.SetTag("server.address", context.ClientConfig.BootstrapServers);
        // activity?.SetTag("server.port", context.ClientConfig.BootstrapServers);

        return activity;
    }

    public static Activity? SetTagsFor<T>(this Activity? activity, ConsumeContext<T> context)
    {
        // See https://github.com/open-telemetry/opentelemetry-specification/issues/2971#issuecomment-1324621326
        // activity?.SetTag("messaging.message.id", context.MessageId);
        activity?.SetTag("messaging.destination.name", context.Topic);
        activity?.SetTag("messaging.destination.partition.id", context.ConsumeResult.Partition.Value);
        activity?.SetTag("messaging.kafka.message.offset", (long)context.ConsumeResult.Offset);

        activity?.SetTag("messaging.message.body.size", context.Message.Value.Length);

        // Skip, as we always ignore the key on consumption
        // activity.SetTag("messaging.kafka.message.key", context.Message.Key);

        // TODO error.type

        return activity;
    }

    public static Activity? SetTagsFor<T>(this Activity? activity, IReadOnlyCollection<ConsumeContext<T>> batch)
    {
        activity?.SetTag("messaging.batch.message_count", batch.Count);
        if (batch.Count > 0)
            activity?.SetTag("messaging.destination.name", batch.First().Topic);

        return activity;
    }

    // public static Activity? SetTagsFor<T>(this Activity? activity, IReadOnlyCollection<ConsumeContext<T>> batch) =>
    //     activity?.SetTag("messaging.batch.message_count", batch.Count);
}

// Based on Semantic Conventions 1.30.0, see
// https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
// Also Npgsql as an inspiration:
//  - https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlActivitySource.cs
//  - https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlCommand.cs
internal static class Tracing
{
    private static readonly ActivitySource Source;

    public static bool IsEnabled => Source.HasListeners();

    static Tracing()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        Source = new ActivitySource("LocalPost.KafkaConsumer", version);
    }

    public static Activity? StartProcessing<T>(IReadOnlyCollection<ConsumeContext<T>> batch)
    {
        Debug.Assert(batch.Count > 0);
        var activity = Source.StartActivity($"process {batch.First().Topic}", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("messaging.operation.type", "process");
        activity.SetDefaultTags(batch.First().Client);
        activity.SetTagsFor(batch);

        return activity;
    }

    public static Activity? StartProcessing<T>(ConsumeContext<T> context)
    {
        var activity = Source.StartActivity($"process {context.Topic}", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("messaging.operation.type", "process");
        activity.SetDefaultTags(context.Client);
        activity.SetTagsFor(context);
        activity.AcceptDistributedTracingFrom(context.Message);

        return activity;
    }
}
