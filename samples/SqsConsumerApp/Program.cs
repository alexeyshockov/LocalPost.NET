using Amazon.SQS;
using LocalPost;
using LocalPost.SqsConsumer;
using LocalPost.SqsConsumer.DependencyInjection;
using Serilog;
using Serilog.Sinks.FingersCrossed;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSerilog()
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAWSService<IAmazonSQS>();
builder.Services
    .AddScoped<MessageHandler>()
    .AddSqsConsumers(sqs =>
    {
        sqs.Defaults.Configure(options => options.MaxConcurrency = 100);
        sqs.AddConsumer("weather-forecasts",
            HandlerStack.From<MessageHandler, WeatherForecast>()
                .UseSqsPayload()
                .DeserializeJson()
                .Acknowledge()
                .Scoped()
                .Touch(next => async (context, ct) =>
                {
                    using var logBuffer = LogBuffer.BeginScope();
                    try
                    {
                        await next(context, ct);
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == ct)
                    {
                        throw; // Not a real error
                    }
                    catch (Exception)
                    {
                        logBuffer.Flush();
                        throw;
                    }
                })
                .Trace());
    });

await builder.Build().RunAsync();



public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);

public class MessageHandler : IHandler<WeatherForecast>
{
    public async ValueTask InvokeAsync(WeatherForecast payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload);
    }
}
