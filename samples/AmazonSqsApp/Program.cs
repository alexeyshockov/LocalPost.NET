using Amazon.SQS;
using AmazonSqsApp;
using LocalPost;
using LocalPost.DependencyInjection;
using LocalPost.SqsConsumer;
using LocalPost.SqsConsumer.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// A background queue with an inline handler
builder.Services.AddBackgroundQueue<WeatherForecast>(
    // TODO Automatically add the health checks?..
    async (weather, ct) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Console.WriteLine(weather.Summary);
    });


// An Amazon SQS consumer
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddScoped<SqsHandler>();
builder.Services.AddAmazonSqsConsumer<SqsHandler>("test");


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



class SqsHandler : IHandler<ConsumeContext>
{
    public async Task InvokeAsync(ConsumeContext payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload.Message.Body);
    }
}
