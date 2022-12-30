using Amazon.SimpleNotificationService;
using Amazon.SQS;
using LocalPost.SnsPublisher.DependencyInjection;
using LocalPost.DependencyInjection;
using SampleWebApp;
using LocalPost.SqsConsumer.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);



// A background queue with an inline handler
builder.Services.AddBackgroundQueue<WeatherForecast>(_ => async (w, ct) =>
{
    await Task.Delay(TimeSpan.FromSeconds(2), ct);
    Console.WriteLine(w.Summary);
});



// An async Amazon SNS sender, buffers messages and sends them in batches in the background
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
builder.Services.AddAmazonSnsBatchPublisher();



// An Amazon SQS consumer
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAmazonSqsMinimalConsumer("test", async (context, ct) =>
{
    await Task.Delay(1_000, ct);
    Console.WriteLine(context.Body);
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
