using Amazon.SQS;

namespace LocalPost.SqsConsumer;

internal sealed class ProcessedMessageHandler : IMiddleware<ConsumeContext>
{
    private readonly IAmazonSQS _sqs;

    public ProcessedMessageHandler(IAmazonSQS sqs)
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
