using Amazon.SimpleNotificationService.Model;
using LocalPost;
using LocalPost.SnsPublisher;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly IBackgroundQueue<WeatherForecast> _queue;
    private readonly ISnsPublisher _sns;

    public WeatherForecastController(IBackgroundQueue<WeatherForecast> queue, ISnsPublisher sns)
    {
        _queue = queue;
        _sns = sns;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public async ValueTask<IEnumerable<WeatherForecast>> Get()
    {
        var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToArray();

        await _queue.Enqueue(forecasts[0]);

        await _sns.ForTopic("arn:aws:sns:eu-central-1:703886664977:test").Enqueue(new PublishBatchRequestEntry
        {
            Message = forecasts[0].Summary
        });

        return forecasts;
    }
}
