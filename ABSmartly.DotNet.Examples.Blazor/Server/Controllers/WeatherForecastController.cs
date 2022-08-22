using ABSmartlyDotNetExamples.Blazor.Shared;
using ABSmartlySdk;
using Microsoft.AspNetCore.Mvc;

namespace ABSmartlyDotNetExamples.Blazor.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = 
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ABSmartly _abSmartly;
    private readonly Context _context;

    public WeatherForecastController(ABSmartly abSmartly)
    {
        _abSmartly = abSmartly;
        _context = _abSmartly.CreateContext();
    }

    [HttpGet]
    public IEnumerable<WeatherForecast> Get()
    {
        var temps = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

        // Bad, do it manually, null issue...
        //var properties = temps.ToDictionary<WeatherForecast, string, object>(forecast => forecast.Summary, forecast => forecast.TemperatureC);
        //_context.Track("WeatherForecastCount", properties);

        return temps;
    }
}