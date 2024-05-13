using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

internal static class Exceptions
{
    public static bool IsTransient(this ConsumeException exception) =>
        // See https://github.com/confluentinc/confluent-kafka-dotnet/issues/1424#issuecomment-705749252
        exception.Error.Code is ErrorCode.Local_KeyDeserialization or ErrorCode.Local_ValueDeserialization;
}
