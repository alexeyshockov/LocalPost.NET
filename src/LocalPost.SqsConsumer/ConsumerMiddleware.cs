using Amazon.SQS;

namespace LocalPost.SqsConsumer;

public static partial class MessageSource
{
    internal sealed class Middleware : IMiddleware<ConsumeContext> // TODO Rename
    {
        private readonly IAmazonSQS _sqs;

        public Middleware(IAmazonSQS sqs)
        {
            _sqs = sqs;
        }

        public Handler<ConsumeContext> Invoke(Handler<ConsumeContext> next) => async (context, ct) =>
        {
            if (context.IsStale)
                return;

            // TODO Processing timeout from the visibility timeout
            await next(context, ct); // Extend message's VisibilityTimeout in case of long processing?..

            // Won't be deleted in case of an exception in the handler
            await _sqs.DeleteMessageAsync(context.QueueUrl, context.Message.ReceiptHandle, ct);
        };
    }
}
