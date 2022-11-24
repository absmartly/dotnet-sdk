using ABSmartly;
using ABSmartly.Models;
using ABSmartlyExamples.SkiResortApp.Data;
using Microsoft.AspNetCore.Mvc;

namespace ABSmartlyExamples.SkiResortApp.Controllers;

[ApiController]
[Route("[controller]")]
public class Test : ControllerBase
{
    private static readonly Func<int, string?>[] WinterSummaries =
    {
        t => t < -16 ? "Freezing" : null,
        t => t < -12 ? "Bracing" : null,
        t => t < -8 ? "Chilly" : null,
        t => t < -4 ? "Cool" : null,
        _ => "Mild"
    };

    private static readonly Func<int, string?>[] SummerSummaries =
    {
        t => t < 12 ? "Warm" : null,
        t => t < 24 ? "Balmy" : null,
        t => t < 35 ? "Hot" : null,
        t => t < 42 ? "Sweltering" : null,
        _ => "Scorching"
    };

    private readonly ABSdk _abSdk;

    public Test(ABSdk abSdk)
    {
        _abSdk = abSdk;
    }

    [HttpGet]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        var userId = new Random().Next(0, 2) == 0 ? "12345" : "abcdef";

        var getWinter = new Func<int, WeatherForecast>(i =>
        {
            var temperatureC = Random.Shared.Next(-20, 1);
            return new WeatherForecast
            {
                UserId = userId,
                Date = DateTime.Now.AddDays(i),
                TemperatureC = temperatureC,
                Summary = WinterSummaries.Select(s => s(temperatureC)).FirstOrDefault(x => x != null)
            };
        });

        var getSummer = new Func<int, WeatherForecast>(i =>
        {
            var temperatureC = Random.Shared.Next(1, 55);
            return new WeatherForecast
            {
                UserId = userId,
                Date = DateTime.Now.AddDays(i),
                TemperatureC = temperatureC,
                Summary = SummerSummaries.Select(s => s(temperatureC)).FirstOrDefault(x => x != null)
            };
        });

        var config = new ContextConfig().SetUnit("user_id", userId);
        var context = await _abSdk.CreateContextAsync(config);

        var treatment = context.GetTreatment("net_seasons");

        // show winter forecast for control group (treatment == 0,
        // and summer forecast for experiment group
        var temps = Enumerable.Range(1, 5).Select(treatment == 0 ? getWinter : getSummer).ToArray();

        return temps;
    }

    [HttpPost]
    [Route("book/{userId}")]
    public async Task Book(string userId)
    {
        var config = new ContextConfig().SetUnit("user_id", userId);
        config.RefreshInterval = TimeSpan.FromHours(4);
        var context = await _abSdk.CreateContextAsync(config);

        context.Track("booking", new Dictionary<string, object>
        {
            { "bookingTime", DateTime.Now }
        });

        await context.PublishAsync();
    }

    private class CustomEventLogger : IContextEventLogger
    {
        public void HandleEvent(IContext context, EventType eventType, object data)
        {
            switch (eventType)
            {
                case EventType.Exposure when data is Exposure exposure:
                    Console.WriteLine($"exposed to experiment: {exposure.Name}");
                    break;
                case EventType.Goal when data is GoalAchievement goal:
                    Console.WriteLine($"goal tracked: {goal.Name}");
                    break;
                case EventType.Error:
                    Console.WriteLine($"error: {data}");
                    break;
                case EventType.Close:
                case EventType.Publish:
                case EventType.Ready:
                case EventType.Refresh:
                    break;
            }
        }
    }
}