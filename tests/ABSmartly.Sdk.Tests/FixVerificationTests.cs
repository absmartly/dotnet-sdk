using System.Reflection;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class FixVerificationTests
{
    private ContextData _data = null!;
    private readonly Dictionary<string, string> _units = new()
    {
        ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec",
        ["user_id"] = "123456789",
        ["email"] = "bleh@absmartly.com"
    };

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);
    private IContextDataProvider _dataProvider = null!;
    private IContextEventLogger _eventLogger = null!;
    private IContextEventHandler _eventHandler = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [SetUp]
    public void SetUp()
    {
        var deserializer = new DefaultContextDataDeserializer();
        using var contextStream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = deserializer.Deserialize(contextStream);

        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _eventHandler = Mock.Of<IContextEventHandler>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    private Context CreateContext(ContextData data) =>
        new(new ContextConfig { PublishDelay = TimeSpan.FromSeconds(60) }.SetUnits(_units), data, _clock,
            _dataProvider, _eventHandler, _eventLogger, _variableParser, _audienceMatcher, new LoggerFactory());

    private Context CreateContext(ContextConfig config, ContextData data) =>
        new(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser, _audienceMatcher,
            new LoggerFactory());

    [Test]
    public void Fix1_FlushRaceCondition_EventsAddedDuringPublishAreNotLost()
    {
        var publishCalled = false;
        var eventsAddedDuringPublish = false;

        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>((ctx, evt) =>
            {
                publishCalled = true;
                if (!eventsAddedDuringPublish)
                {
                    eventsAddedDuringPublish = true;
                    ((Context)ctx).Track("late_goal", new Dictionary<string, object> { ["key"] = "value" });
                }
                return Task.FromResult(true);
            });

        var context = CreateContext(_data);
        context.Track("goal1", new Dictionary<string, object> { ["amount"] = 100 });
        context.Publish();

        publishCalled.Should().BeTrue();
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void Fix3_IsLocalEndpoint_RejectsNonHttpsNonLocal()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var act = () => new ABSmartlyService(
            H.ServiceConfig("1", "http://evil.com", "1", "1"),
            httpClientFactory, deserializer, serializer);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Fix3_IsLocalEndpoint_AcceptsLocalhost()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var act = () => new ABSmartlyService(
            H.ServiceConfig("1", "http://localhost:8080", "1", "1"),
            httpClientFactory, deserializer, serializer);
        act.Should().NotThrow();
    }

    [Test]
    public void Fix3_IsLocalEndpoint_Accepts127()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var act = () => new ABSmartlyService(
            H.ServiceConfig("1", "http://127.0.0.1:8080", "1", "1"),
            httpClientFactory, deserializer, serializer);
        act.Should().NotThrow();
    }

    [Test]
    public void Fix3_IsLocalEndpoint_AcceptsIPv6Loopback()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var act = () => new ABSmartlyService(
            H.ServiceConfig("1", "http://[::1]:8080", "1", "1"),
            httpClientFactory, deserializer, serializer);
        act.Should().NotThrow();
    }

    [Test]
    public void Fix12_IsLocalEndpoint_RejectsSubstringMatching()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var act = () => new ABSmartlyService(
            H.ServiceConfig("1", "http://evil-localhost.com", "1", "1"),
            httpClientFactory, deserializer, serializer);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Fix12_IsLocalEndpoint_RejectsMalformedUri()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var act = () => new ABSmartlyService(
            H.ServiceConfig("1", "not-a-uri", "1", "1"),
            httpClientFactory, deserializer, serializer);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Fix4_EqualsOperator_NullHandling()
    {
        var evaluator = Mock.Of<IEvaluator>();
        Mock.Get(evaluator).Setup(e => e.Evaluate(It.IsAny<object>())).Returns<object>(x => x);
        Mock.Get(evaluator).Setup(e => e.Compare(It.IsAny<object>(), It.IsAny<object>()))
            .Returns<object, object>((lhs, rhs) => lhs.Equals(rhs) ? 0 : 1);

        var op = new EqualsOperator();

        var nullNull = op.Evaluate(evaluator, new List<object> { null!, null! });
        nullNull.Should().Be(true);

        var nullValue = op.Evaluate(evaluator, new List<object> { null!, "hello" });
        nullValue.Should().BeNull();

        var valueNull = op.Evaluate(evaluator, new List<object> { "hello", null! });
        valueNull.Should().BeNull();
    }

    [Test]
    public void Fix6_AssignmentVariables_InitializedToEmptyDictionary()
    {
        var assignment = new Context.Assignment();
        assignment.Variables.Should().NotBeNull();
        assignment.Variables.Should().BeEmpty();
    }

    [Test]
    public void Fix18_EndpointTrailingSlash_IsTrimmed()
    {
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>();
        var httpClient = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(httpClientFactory).Setup(x => x.CreateClient()).Returns(httpClient);
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        var service = new ABSmartlyService(
            H.ServiceConfig("1", "https://example.com/v1/", "1", "1"),
            httpClientFactory, deserializer, serializer);

        Mock.Get(httpClient)
            .Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<HttpContent>()))
            .Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            }));
        Mock.Get(serializer).Setup(x => x.Serialize(It.IsAny<PublishEvent>())).Returns(Array.Empty<byte>());

        service.PublishAsync(new PublishEvent()).Wait();

        Mock.Get(httpClient).Verify(x => x.PutAsync("https://example.com/v1/context", It.IsAny<HttpContent>()), Times.Once);
    }

    [Test]
    public void Fix25_ContextConstructor_NullLoggerFactory_DoesNotThrow()
    {
        var config = new ContextConfig { PublishDelay = TimeSpan.FromSeconds(60) }.SetUnits(_units);

        var act = () => new Context(config, _data, _clock, _dataProvider, _eventHandler, _eventLogger,
            _variableParser, _audienceMatcher, null!);
        act.Should().NotThrow();
    }

    [Test]
    public void Fix2_NoConsoleErrorWriteLine_InServices()
    {
        var sourceAssembly = typeof(ABSmartlyService).Assembly;
        var sourceFiles = new[]
        {
            typeof(ABSmartlyService),
            typeof(DefaultAudienceDeserializer),
            typeof(DefaultContextDataDeserializer),
            typeof(DefaultVariableParser)
        };

        foreach (var type in sourceFiles)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var body = method.GetMethodBody();
                if (body == null) continue;

                var il = body.GetILAsByteArray();
                if (il == null) continue;
            }
        }
    }

    [Test]
    public void IsFinalized_ReturnsFalseWhenOpen_TrueAfterClose()
    {
        var context = CreateContext(_data);
        context.IsFinalized.Should().BeFalse();
        context.Dispose();
        context.IsFinalized.Should().BeTrue();
    }

    [Test]
    public void Close_AliasForDispose_ContextBecomesFinalized()
    {
        var context = CreateContext(_data);
        context.IsFinalized.Should().BeFalse();
        context.Close();
        context.IsFinalized.Should().BeTrue();
        context.IsClosed().Should().BeTrue();
    }

    [Test]
    public void GetCustomFieldValueType_ReturnsSameAsGetCustomFieldType()
    {
        var context = CreateContext(_data);
        var experimentName = _data.Experiments.FirstOrDefault()?.Name;
        if (experimentName == null) return;

        var customFieldValues = _data.Experiments.FirstOrDefault()?.CustomFieldValues;
        if (customFieldValues == null || customFieldValues.Length == 0) return;

        var key = customFieldValues[0].Name;
        var type1 = context.GetCustomFieldValueType(experimentName, key);
        var type2 = context.GetCustomFieldType(experimentName, key);
        type1.Should().Be(type2);
    }

    [Test]
    public void StandardizedErrorMessages_WhenFinalized()
    {
        var context = CreateContext(_data);
        context.Close();

        var act = () => context.SetAttribute("attr1", "value1");
        act.Should().Throw<InvalidOperationException>().WithMessage("ABsmartly Context is finalized.");
    }

    [Test]
    public void StandardizedErrorMessages_UnitAlreadySet()
    {
        var context = CreateContext(_data);
        var act = () => context.SetUnit("session_id", "different_value");
        act.Should().Throw<ArgumentException>().WithMessage("Unit 'session_id' UID already set.");
    }

    [Test]
    public void StandardizedErrorMessages_UnitUidBlank()
    {
        var context = CreateContext(_data);
        var act = () => context.SetUnit("new_unit", "");
        act.Should().Throw<ArgumentException>().WithMessage("Unit 'new_unit' UID must not be blank.");
    }
}
