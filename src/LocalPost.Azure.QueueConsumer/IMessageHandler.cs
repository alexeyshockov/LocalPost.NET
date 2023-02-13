using Azure.Storage.Queues.Models;

namespace LocalPost.Azure.QueueConsumer;

public interface IMessageHandler : IHandler<QueueMessage>
{
}
