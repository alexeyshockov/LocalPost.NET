using LocalPost;
using Microsoft.AspNetCore.Mvc;

namespace BackgroundQueueApp.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(IBackgroundQueue<WeatherForecast> queue) : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet(Name = "GetWeatherForecast")]
    public async ValueTask<IEnumerable<WeatherForecast>> Get()
    {
        var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToArray();

        await queue.Enqueue(forecasts[0]);

        return forecasts;
    }
}
