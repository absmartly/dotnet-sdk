using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ErrorRecoveryTests
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
        ["user_id"] = "123456789",
        ["email"] = "bleh@absmartly.com"
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
    public async Task TestNetworkRetryWithBackoff()
    {
        var callCount = 0;
        var callTimes = new List<DateTime>();

        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .Returns(() =>
            {
                callCount++;
                callTimes.Add(DateTime.UtcNow);
                if (callCount < 3)
                {
                    throw new HttpRequestException("Network error");
                }
                return Task.FromResult(_refreshedData);
            });

        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await context.RefreshAsync();
                break;
            }
            catch (HttpRequestException)
            {
                if (attempt < 2)
                {
                    await Task.Delay(100 * (int)Math.Pow(2, attempt));
                }
            }
        }

        callCount.Should().Be(3);
        context.GetExperiments().Should().BeEquivalentTo(_refreshedData.Experiments.Select(x => x.Name));
    }

    [Test]
    public void TestGracefulDegradationNoNetwork()
    {
        var context = CreateContext(null!);

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeTrue();

        context.GetTreatment("exp_test_ab").Should().Be(0);
        context.GetTreatment("exp_test_abc").Should().Be(0);
        context.GetTreatment("non_existent_experiment").Should().Be(0);

        context.PeekTreatment("exp_test_ab").Should().Be(0);
        context.PeekVariableValue("banner.size", "default").Should().Be("default");
        context.GetVariableValue("button.color", "gray").Should().Be("gray");

        context.Track("goal1", new Dictionary<string, object> { ["amount"] = 100 });
        context.PendingCount.Should().Be(4);

        context.Publish();

        Mock.Get(_eventHandler)
            .Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Never);
    }

    [Test]
    public void TestRecoveryAfterTransientError()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        context.GetTreatment("exp_test_ab");
        context.Track("goal1", new Dictionary<string, object> { ["amount"] = 100 });
        context.PendingCount.Should().Be(2);

        var failureCount = 0;
        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(() =>
            {
                failureCount++;
                if (failureCount < 2)
                {
                    throw new Exception("Transient network error");
                }
                return Task.CompletedTask;
            });

        var firstPublishAct = () => context.Publish();
        firstPublishAct.Should().Throw<Exception>().WithMessage("Transient network error");

        context.Track("goal2", new Dictionary<string, object> { ["value"] = 50 });
        context.PendingCount.Should().Be(1);

        context.Publish();

        Mock.Get(_eventHandler)
            .Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Exactly(2));
    }

    [Test]
    public async Task TestCircuitBreakerPattern()
    {
        var failureThreshold = 3;
        var failureCount = 0;
        var isCircuitOpen = false;

        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .Returns(() =>
            {
                if (isCircuitOpen)
                {
                    throw new InvalidOperationException("Circuit breaker is open");
                }

                failureCount++;
                if (failureCount >= failureThreshold)
                {
                    isCircuitOpen = true;
                }
                throw new HttpRequestException("Service unavailable");
            });

        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        for (var i = 0; i < failureThreshold; i++)
        {
            var act = () => context.Refresh();
            act.Should().Throw<HttpRequestException>().WithMessage("Service unavailable");
        }

        failureCount.Should().Be(failureThreshold);
        isCircuitOpen.Should().BeTrue();

        var circuitOpenAct = () => context.Refresh();
        circuitOpenAct.Should().Throw<InvalidOperationException>().WithMessage("Circuit breaker is open");

        isCircuitOpen = false;
        failureCount = 0;
        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .ReturnsAsync(_refreshedData);

        await context.RefreshAsync();

        context.GetExperiments().Should().BeEquivalentTo(_refreshedData.Experiments.Select(x => x.Name));
    }

    [Test]
    public void TestContextRemainsUsableAfterRefreshError()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.GetVariableValue("banner.size", "small").Should().Be("large");

        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .Throws(() => new Exception("Refresh failed"));

        var refreshAct = () => context.Refresh();
        refreshAct.Should().Throw<Exception>().WithMessage("Refresh failed");

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.GetVariableValue("banner.size", "small").Should().Be("large");

        context.Track("goal1", null);
        context.PendingCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void TestPublishErrorDoesNotCorruptState()
    {
        var context = CreateReadyContext();

        context.GetTreatment("exp_test_ab");
        context.Track("goal1", new Dictionary<string, object> { ["amount"] = 100 });
        context.Track("goal2", new Dictionary<string, object> { ["value"] = 50 });

        var initialPendingCount = context.PendingCount;
        initialPendingCount.Should().Be(3);

        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Throws(() => new Exception("Publish failed"));

        var publishAct = () => context.Publish();
        publishAct.Should().Throw<Exception>().WithMessage("Publish failed");

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.GetTreatment("exp_test_abc").Should().Be(2);
    }

    [Test]
    public void TestMultipleConcurrentRefreshRequests()
    {
        var context = CreateReadyContext();
        var refreshCount = 0;
        var lockObj = new object();

        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .Returns(async () =>
            {
                lock (lockObj)
                {
                    refreshCount++;
                }
                await Task.Delay(100);
                return _refreshedData;
            });

        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() => context.RefreshAsync()));
        }

        Task.WaitAll(tasks.ToArray());

        refreshCount.Should().Be(1);

        context.GetExperiments().Should().BeEquivalentTo(_refreshedData.Experiments.Select(x => x.Name));
    }

    [Test]
    public void TestEventLoggerReceivesErrorEvents()
    {
        var context = CreateReadyContext();

        context.Track("goal1", null);

        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Throws(() => new Exception("ERROR_MESSAGE"));

        var act = () => context.Publish();
        act.Should().Throw<Exception>();

        Mock.Get(_eventLogger)
            .Verify(x => x.HandleEvent(context, EventType.Error, "ERROR_MESSAGE"), Times.Once);
    }

    private Context CreateContext(ContextConfig config, ContextData data) =>
        new(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory());

    private Context CreateContext(ContextData data) =>
        new(new ContextConfig().SetUnits(_units), data, _clock, _dataProvider, _eventHandler, _eventLogger,
            _variableParser, _audienceMatcher, new LoggerFactory());

    private Context CreateReadyContext() => CreateContext(_data);
}
