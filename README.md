# LocalPost

[![NuGet package](https://img.shields.io/nuget/dt/LocalPost)](https://www.nuget.org/packages/LocalPost/)
[![Code coverage](https://img.shields.io/sonar/coverage/alexeyshockov_LocalPost.NET?server=https%3A%2F%2Fsonarcloud.io)](https://sonarcloud.io/project/overview?id=alexeyshockov_LocalPost.NET)

Simple .NET in-memory background queue ([System.Threading.Channels](https://learn.microsoft.com/de-de/dotnet/api/system.threading.channels?view=net-6.0) based).

## Background tasks

There are multiple ways to run background tasks in .NET. The most common are:

## Usage

### Installation

For the core library:

```shell
dotnet add package LocalPost
```

AWS SQS, Kafka and other integrations are provided as separate packages, like:

```shell
dotnet add package LocalPost.SqsConsumer
dotnet add package LocalPost.KafkaConsumer
```

### .NET 8 asynchronous background services handling

Before version 8 .NET runtime handled start/stop of the services only synchronously, but now it is possible to enable
concurrent handling of the services. This is done by setting `HostOptions` property `ConcurrentServiceExecution`
to `true`:

See for details:
- https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs
- https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostOptions.cs

## Similar projects

- [Coravel queue](https://docs.coravel.net/Queuing/) — a simple job queue

More complex jobs management / scheduling:
- [Hangfire](https://www.hangfire.io/) — background job scheduler. Supports advanced scheduling, persistence and jobs distribution across multiple workers.

Service bus (for bigger solutions):
- [JustSaying](https://github.com/justeattakeaway/JustSaying)
- [NServiceBus](https://docs.particular.net/nservicebus/)
- [MassTransit](https://masstransit.io/)

## Inspiration

- [FastStream](https://github.com/airtai/faststream)
