# LocalPost

[![NuGet package](https://img.shields.io/nuget/dt/LocalPost)](https://www.nuget.org/packages/LocalPost/)
[![Code coverage](https://img.shields.io/sonar/coverage/alexeyshockov_LocalPost.NET?server=https%3A%2F%2Fsonarcloud.io)](https://sonarcloud.io/project/overview?id=alexeyshockov_LocalPost.NET)

Minimal APIs for your Kafka/SQS/... consumers and

Simple .NET in-memory background queue ([System.Threading.Channels](https://learn.microsoft.com/de-de/dotnet/api/system.threading.channels?view=net-6.0) based).

## Installation

For the core library:

```shell
dotnet add package LocalPost
```

AWS SQS, Kafka and other integrations are provided as separate packages, like:

```shell
dotnet add package LocalPost.SqsConsumer
dotnet add package LocalPost.KafkaConsumer
```

## Usage

### .NET 8 asynchronous background services handling

Before version 8, the .NET runtime handled the start and stop of services only synchronously. However, it is now
possible to enable concurrent handling of services by setting the `ConcurrentServiceExecution` property of
`HostOptions` to `true`.

See for details:
- [Microsoft.Extensions.Hosting/../Host.cs](https://github.com/dotnet/runtime/blob/v8.0.14/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs)
- [Microsoft.Extensions.Hosting/../HostOptions.cs](https://github.com/dotnet/runtime/blob/v8.0.14/src/libraries/Microsoft.Extensions.Hosting/src/HostOptions.cs)

## Motivation

TBD

### Similar projects

- [Coravel queue](https://docs.coravel.net/Queuing/) — a simple job queue

More complex jobs management / scheduling:
- [Hangfire](https://www.hangfire.io/) — background job scheduler. Supports advanced scheduling, persistence and jobs distribution across multiple workers.

Service bus (for bigger solutions):
- [JustSaying](https://github.com/justeattakeaway/JustSaying)
- [NServiceBus](https://docs.particular.net/nservicebus/)
- [MassTransit](https://masstransit.io/)

### Inspiration

- [FastStream](https://github.com/airtai/faststream)
