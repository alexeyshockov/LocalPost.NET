using Amazon.SQS;
using JetBrains.Annotations;
using LocalPost;
using LocalPost.SqsConsumer;
using LocalPost.SqsConsumer.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.FingersCrossed;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSerilog() // See https://nblumhardt.com/2024/04/serilog-net8-0-minimal/#hooking-up-aspnet-core-and-iloggert
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAWSService<IAmazonSQS>();

builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

#region OpenTelemetry

// See also: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example

// To use full potential of Serilog, it's better to use Serilog.Sinks.OpenTelemetry,
// see https://github.com/Blind-Striker/dotnet-otel-aspire-localstack-demo as an example
// builder.Logging.AddOpenTelemetry(logging =>
// {
//     logging.IncludeFormattedMessage = true;
//     logging.IncludeScopes = true;
// });

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAWSInstrumentation())
    .WithTracing(tracing => tracing
        .AddSource("LocalPost.*")
        .AddAWSInstrumentation())
    .UseOtlpExporter();

#endregion

builder.Services
    .AddScoped<MessageHandler>()
    .AddSqsConsumers(sqs =>
    {
        sqs.Defaults.Configure(options => options.MaxNumberOfMessages = 1);
        sqs.AddConsumer("weather-forecasts", // Also acts as a queue name
            HandlerStack.From<MessageHandler, WeatherForecast>()
                .Scoped()
                .UseSqsPayload()
                .DeserializeJson()
                .Trace()
                .Acknowledge() // Do not include DeleteMessage call in the OpenTelemetry root span (transaction)
                .LogFingersCrossed()
                .LogExceptions()
        );
    });

await builder.Build().RunAsync();


[UsedImplicitly]
public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);

public class MessageHandler : IHandler<WeatherForecast>
{
    public async ValueTask InvokeAsync(WeatherForecast payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload);

        // To show the failure handling
        if (payload.TemperatureC > 35)
            throw new InvalidOperationException("Too hot");
    }
}

public static class HandlerStackEx
{
    public static HandlerManagerFactory<T> LogFingersCrossed<T>(this HandlerManagerFactory<T> hmf) =>
        hmf.TouchHandler(next => async (context, ct) =>
        {
            using var logBuffer = LogBuffer.BeginScope();
            try
            {
                await next(context, ct);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == ct)
            {
                throw; // Do not treat cancellation as an error
            }
            catch (Exception)
            {
                logBuffer.Flush();
                throw;
            }
        });
}
