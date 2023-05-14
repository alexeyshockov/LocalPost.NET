using Amazon.SimpleNotificationService;
using Amazon.SQS;
using LocalPost.SnsPublisher.DependencyInjection;
using LocalPost.DependencyInjection;
using AmazonSqsApp;
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


// An async Amazon SNS sender, buffers messages and sends them in batches in the background
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
builder.Services.AddAmazonSnsBatchPublisher();


// An Amazon SQS consumer
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAmazonSqsConsumer("test",
    async (context, ct) =>
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(context.Message.Body);
    });


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
