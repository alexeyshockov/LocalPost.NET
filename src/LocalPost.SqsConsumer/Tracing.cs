using System.Diagnostics;
using System.Reflection;
using Amazon.SQS.Model;

namespace LocalPost.SqsConsumer;

internal static class MessageUtils
{
    public static void ExtractTraceField(object? carrier, string fieldName,
        out string? fieldValue, out IEnumerable<string>? fieldValues)
    {
        fieldValues = null;
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
        activity?.SetTag("messaging.system", "aws_sqs");

        activity?.SetTag("messaging.destination.name", client.QueueName);

        // activity?.SetTag("messaging.client_id", "service_name");
        // activity?.SetTag("server.address", client.QueueUrl.Host);
        // activity?.SetTag("server.port", client.QueueUrl.Port);
    }

    public static Activity? SetTagsFor<T>(this Activity? activity, ConsumeContext<T> context) =>
        activity?.SetTag("messaging.message.id", context.MessageId);

    public static Activity? SetTagsFor<T>(this Activity? activity, IReadOnlyCollection<ConsumeContext<T>> batch) =>
        activity?.SetTag("messaging.batch.message_count", batch.Count);

    public static Activity? SetTagsFor(this Activity? activity, ReceiveMessageResponse response) =>
        activity?.SetTag("messaging.batch.message_count", response.Messages.Count);
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
        Source = new ActivitySource("LocalPost.SqsConsumer", version);
    }

    public static Activity? StartProcessing<T>(IReadOnlyCollection<ConsumeContext<T>> batch)
    {
        Debug.Assert(batch.Count > 0);
        var client = batch.First().Client;
        var activity = Source.StartActivity($"process {client.QueueName}", ActivityKind.Consumer);
        if (activity is { IsAllDataRequested: true })
        {
            activity?.SetTag("messaging.operation.type", "process");
            activity.SetDefaultTags(client);
            activity.SetTagsFor(batch);
            // TODO Distributed tracing (OTEL links)
        }

        activity?.Start();

        return activity;
    }

    public static Activity? StartProcessing<T>(ConsumeContext<T> context)
    {
        var activity = Source.StartActivity($"process {context.Client.QueueName}", ActivityKind.Consumer);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("messaging.operation.type", "process");
            activity.SetDefaultTags(context.Client);
            activity.SetTagsFor(context);
            activity.AcceptDistributedTracingFrom(context.Message);
        }

        activity?.Start();

        return activity;
    }

    public static Activity? StartSettling<T>(ConsumeContext<T> context)
    {
        var activity = Source.StartActivity($"settle {context.Client.QueueName}", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("messaging.operation.type", "settle");
        activity.SetDefaultTags(context.Client);
        activity.SetTag("messaging.message.id", context.MessageId);

        return activity;
    }

    public static Activity? StartReceiving(QueueClient client)
    {
        var activity = Source.StartActivity($"receive {client.QueueName}", ActivityKind.Consumer);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("messaging.operation.type", "receive");
        activity.SetDefaultTags(client);

        return activity;
    }
}
