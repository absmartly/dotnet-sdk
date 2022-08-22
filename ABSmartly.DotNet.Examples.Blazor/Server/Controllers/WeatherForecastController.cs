using ABSmartlyDotNetExamples.Blazor.Shared;
using ABSmartlySdk;
using Microsoft.AspNetCore.Mvc;

namespace ABSmartlyDotNetExamples.Blazor.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };


    private readonly ILogger<WeatherForecastController> _logger;
    private readonly ABSmartly _abSmartly;
    private readonly Context _context;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, ABSmartly abSmartly)
    {
        _logger = logger;
        _abSmartly = abSmartly;
        _context = _abSmartly.CreateContext()
    }

    [HttpGet]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }
}