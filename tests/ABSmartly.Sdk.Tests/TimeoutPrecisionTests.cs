using System.Diagnostics;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class TimeoutPrecisionTests
{
    [SetUp]
    public void SetUp()
    {
        var deserializer = new DefaultContextDataDeserializer();

        using var contextStream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = deserializer.Deserialize(contextStream);

        using var refreshedStream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.refreshed.json");
        _refreshedData = deserializer.Deserialize(refreshedStream);

        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _eventHandler = Mock.Of<IContextEventHandler>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    private readonly Dictionary<string, string> _units = new()
    {
        ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec",
        ["user_id"] = "123456789"
    };

    private ContextData _data = null!;
    private ContextData _refreshedData = null!;

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);

    private IContextDataProvider _dataProvider = null!;
    private IContextEventLogger _eventLogger = null!;
    private IContextEventHandler _eventHandler = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [Test]
    public void TestPublishDelayAccuracy()
    {
        var publishDelay = TimeSpan.FromMilliseconds(200);
        var publishedAt = DateTime.MinValue;
        var eventTriggered = new ManualResetEventSlim(false);

        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(() =>
            {
                publishedAt = DateTime.UtcNow;
                eventTriggered.Set();
                return Task.CompletedTask;
            });

        var config = new ContextConfig { PublishDelay = publishDelay }.SetUnits(_units);
        var context = CreateContext(config, _data);

        context.IsReady().Should().BeTrue();

        var startTime = DateTime.UtcNow;
        context.GetTreatment("exp_test_ab");

        eventTriggered.Wait(TimeSpan.FromSeconds(5));

        var elapsed = publishedAt - startTime;
        elapsed.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(publishDelay.TotalMilliseconds - 50);
        elapsed.TotalMilliseconds.Should().BeLessThan(publishDelay.TotalMilliseconds + 500);
    }

    [Test]
    public void TestRefreshIntervalAccuracy()
    {
        var refreshInterval = TimeSpan.FromMilliseconds(200);
        var refreshTimes = new List<DateTime>();
        var refreshCount = 0;
        var targetRefreshes = 3;
        var allRefreshesDone = new ManualResetEventSlim(false);

        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .Returns(() =>
            {
                refreshTimes.Add(DateTime.UtcNow);
                refreshCount++;
                if (refreshCount >= targetRefreshes)
                {
                    allRefreshesDone.Set();
                }
                return Task.FromResult(_refreshedData);
            });

        var startTime = DateTime.UtcNow;
        var config = new ContextConfig { RefreshInterval = refreshInterval }.SetUnits(_units);
        var context = CreateContext(config, _data);

        context.IsReady().Should().BeTrue();

        allRefreshesDone.Wait(TimeSpan.FromSeconds(5));

        refreshTimes.Count.Should().BeGreaterThanOrEqualTo(targetRefreshes);

        for (var i = 1; i < refreshTimes.Count; i++)
        {
            var interval = refreshTimes[i] - refreshTimes[i - 1];
            interval.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(refreshInterval.TotalMilliseconds - 50);
            interval.TotalMilliseconds.Should().BeLessThan(refreshInterval.TotalMilliseconds + 500);
        }

        context.Dispose();
    }

    [Test]
    public void TestTimeoutEdgeCases()
    {
        var publishTriggered = new ManualResetEventSlim(false);

        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(() =>
            {
                publishTriggered.Set();
                return Task.CompletedTask;
            });

        var veryShortDelay = TimeSpan.FromMilliseconds(1);
        var shortConfig = new ContextConfig { PublishDelay = veryShortDelay }.SetUnits(_units);
        var shortContext = CreateContext(shortConfig, _data);

        shortContext.IsReady().Should().BeTrue();
        shortContext.GetTreatment("exp_test_ab");

        publishTriggered.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        shortContext.Dispose();

        publishTriggered.Reset();
        Mock.Get(_eventHandler).Invocations.Clear();

        var zeroDelayConfig = new ContextConfig { PublishDelay = TimeSpan.Zero }.SetUnits(_units);
        var zeroContext = CreateContext(zeroDelayConfig, _data);

        zeroContext.IsReady().Should().BeTrue();
        zeroContext.GetTreatment("exp_test_ab");

        publishTriggered.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        zeroContext.Dispose();
    }

    [Test]
    public void TestPublishDelayResetOnNewEvents()
    {
        var publishDelay = TimeSpan.FromMilliseconds(300);
        var publishEventCapture = new List<PublishEvent>();
        var publishTriggered = new ManualResetEventSlim(false);

        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) =>
            {
                publishEventCapture.Add(e);
                publishTriggered.Set();
            })
            .Returns(Task.CompletedTask);

        var config = new ContextConfig { PublishDelay = publishDelay }.SetUnits(_units);
        var context = CreateContext(config, _data);

        context.IsReady().Should().BeTrue();

        context.GetTreatment("exp_test_ab");
        Thread.Sleep(100);
        context.Track("goal1", null);

        publishTriggered.Wait(TimeSpan.FromSeconds(5));

        publishEventCapture.Should().HaveCount(1);
        var capturedEvent = publishEventCapture[0];
        capturedEvent.Exposures.Should().HaveCount(1);
        capturedEvent.Goals.Should().HaveCount(1);

        context.Dispose();
    }

    [Test]
    public void TestNoRefreshWhenIntervalIsZero()
    {
        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .ReturnsAsync(_refreshedData);

        var config = new ContextConfig { RefreshInterval = TimeSpan.Zero }.SetUnits(_units);
        var context = CreateContext(config, _data);

        context.IsReady().Should().BeTrue();

        Thread.Sleep(500);

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Never);

        context.Dispose();
    }

    [Test]
    public void TestNegativeRefreshIntervalTreatedAsZero()
    {
        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .ReturnsAsync(_refreshedData);

        var config = new ContextConfig { RefreshInterval = TimeSpan.FromMilliseconds(-100) }.SetUnits(_units);
        var context = CreateContext(config, _data);

        context.IsReady().Should().BeTrue();

        Thread.Sleep(500);

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Never);

        context.Dispose();
    }

    private Context CreateContext(ContextConfig config, ContextData data) =>
        new(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory());
}
