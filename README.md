# LocalPost

Simple .NET in-memory background queue ([System.Threading.Channels](https://learn.microsoft.com/de-de/dotnet/api/system.threading.channels?view=net-6.0) based).

## Alternatives

- [Coravel queue](https://docs.coravel.net/Queuing/)/event broadcasting — only invocable queueing, event broadcasting is different from consuming a queue
- [Hangfire](https://www.hangfire.io/) — this one is for persistent queues, LocalPost is completely about in-memory ones (without any persistency support)
