# LocalPost Amazon SQS Consumer

[![NuGet package](https://img.shields.io/nuget/dt/LocalPost.SqsConsumer)](https://www.nuget.org/packages/LocalPost.SqsConsumer/)

## IAM Permissions

Only `sqs:ReceiveMessage` is required to run a queue consumer. To use additional features also require:
- `sqs:GetQueueUrl` (to use queue names instead of the full URLs)
- `sqs:GetQueueAttributes` (to fetch queue attributes on startup)

## Batching

The first thing to note when using batch processing with SQS: make sure that the queue
[visibility timeout](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html)
is greater than the batch window. Otherwise, the messages will be re-queued before the batch is processed.

Most of other [AWS Lambda recommendations](https://docs.aws.amazon.com/lambda/latest/dg/services-sqs-configure.html)
also apply.
