using Amazon.SQS.Model;
using LocalPost;

namespace LocalPost.SqsConsumer;

public interface ISqsMessageHandler : IMessageHandler<Message>
{
}
