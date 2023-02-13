# LocalPost

Simple .NET in-memory background queue ([System.Threading.Channels](https://learn.microsoft.com/de-de/dotnet/api/system.threading.channels?view=net-6.0) based).

## Alternatives

- [Coravel queue](https://docs.coravel.net/Queuing/)/event broadcasting — only invocable queueing, event broadcasting is different from consuming a queue
- [Hangfire](https://www.hangfire.io/) — for persistent queues (means payload serialisation), LocalPost is completely about in-memory ones

## Amazon SQS Consumer

### Permissions

To operate on a queue below [permissions](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-api-permissions-reference.html) are required:
- sqs:GetQueueUrl
- sqs:GetQueueAttributes
- sqs:ReceiveMessage
- sqs:ChangeMessageVisibility
