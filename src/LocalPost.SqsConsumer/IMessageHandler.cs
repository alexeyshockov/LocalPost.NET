using Amazon.SQS.Model;
using LocalPost;

namespace LocalPost.SqsConsumer;

public interface IMessageHandler : IHandler<Message>
{
}
