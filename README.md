# LocalPost

Simple .NET in-memory background queue ([System.Threading.Channels](https://learn.microsoft.com/de-de/dotnet/api/system.threading.channels?view=net-6.0) based).

## Background tasks

There are multiple ways to run background tasks in .NET. The most common are:


## Amazon SQS Consumer

### Permissions

To operate on a queue below [permissions](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-api-permissions-reference.html) are required:
- sqs:GetQueueUrl
- sqs:GetQueueAttributes
- sqs:ReceiveMessage
- sqs:ChangeMessageVisibility

## Usage

### Installation

### .NET 8 asynchronous background services handling

Before version 8 .NET runtime handled start/stop of the services only synchronously, but now it is possible to enable concurrent handling of the services. This is done by setting `HostOptions` property `ConcurrentServiceExecution` to `true`:

See https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs
See https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostOptions.cs

## Similar projects

- [Coravel queue](https://docs.coravel.net/Queuing/)/event broadcasting — only invocable queueing, event broadcasting is different from consuming a queue
- [Hangfire](https://www.hangfire.io/) — for persistent queues (means payload serialisation), LocalPost is completely about in-memory ones
