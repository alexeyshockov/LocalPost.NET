using Azure.Storage.Queues;

namespace LocalPost.Azure.QueueConsumer;

internal interface IAzureQueues
{
    QueueClient Get(string name);
}

internal sealed class AzureQueues : IAzureQueues
{
    private readonly QueueServiceClient _client;

    public AzureQueues(QueueServiceClient client)
    {
        _client = client;
    }

    public QueueClient Get(string name) => _client.GetQueueClient(name);
}
