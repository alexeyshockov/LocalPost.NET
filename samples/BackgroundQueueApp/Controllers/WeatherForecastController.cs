using LocalPost;
using Microsoft.AspNetCore.Mvc;

namespace BackgroundQueueApp.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly IBackgroundQueue<WeatherForecast> _queue;

    public WeatherForecastController(IBackgroundQueue<WeatherForecast> queue)
    {
        _queue = queue;
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

        return forecasts;
    }
}
