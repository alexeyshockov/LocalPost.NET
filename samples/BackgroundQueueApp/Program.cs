using BackgroundQueueApp;
using LocalPost;
using LocalPost.BackgroundQueue;
using LocalPost.BackgroundQueue.DependencyInjection;
using LocalPost.Polly;
using Polly;
using Polly.Retry;

var builder = WebApplication.CreateBuilder(args);

// See https://github.com/App-vNext/Polly/blob/main/docs/migration-v8.md
var resiliencePipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Constant,
        ShouldHandle = new PredicateBuilder().Handle<Exception>()
    })
    .AddTimeout(TimeSpan.FromSeconds(3))
    .Build();

// A background queue with an inline handler
builder.Services.AddBackgroundQueues(bq =>
    bq.AddQueue(HandlerStack.For<WeatherForecast>(async (weather, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            Console.WriteLine(weather.Summary);
        })
        .Scoped()
        .UsePayload()
        .Trace()
        .UsePollyPipeline(resiliencePipeline)
    )
);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
