using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ContextTests
{
    private readonly Dictionary<string, string> _units = new()
    {
        ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec",
        ["user_id"] = "123456789",
        ["email"] = "bleh@absmartly.com"
    };

    private readonly Dictionary<string, int> _expectedVariants = new()
    {
        ["exp_test_ab"] = 1,
        ["exp_test_abc"] = 2,
        ["exp_test_not_eligible"] = 0,
        ["exp_test_fullon"] = 2,
        ["exp_test_new"] = 1
    };

    private readonly Dictionary<string, object> _expectedVariables = T.MapOf(
        "banner.border", 1,
        "banner.size", "large",
        "button.color", "red",
        "submit.color", "blue",
        "submit.shape", "rect",
        "show-modal", true);

    private readonly Dictionary<string, string> _variableExperiments = new()
    {
        ["banner.border"] = "exp_test_ab",
        ["banner.size"] = "exp_test_ab",
        ["button.color"] = "exp_test_abc",
        ["card.width"] = "exp_test_not_eligible",
        ["submit.color"] = "exp_test_fullon",
        ["submit.shape"] = "exp_test_fullon",
        ["show-modal"] = "exp_test_new"
    };

    private readonly Unit[] _publishUnits =
    {
        new() { Type = "session_id", Uid = "pAE3a1i5Drs5mKRNq56adA" },
        new() { Type = "user_id", Uid = "JfnnlDI7RTiF9RgfG2JNCw" },
        new() { Type = "email", Uid = "IuqYkNRfEx5yClel4j3NbA" }
    };

    private ContextData _data = null!;
    private ContextData _refreshedData = null!;
    private ContextData _audienceData = null!;
    private ContextData _audienceStrictData = null!;

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
        
        using var contextStream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = deserializer.Deserialize(contextStream);

        using var refreshedStream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.refreshed.json");
        _refreshedData = deserializer.Deserialize(refreshedStream);
        
        using var audienceStream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.audience_context.json");
        _audienceData = deserializer.Deserialize(audienceStream);
        
        using var audienceStrictStream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.audience_strict_context.json");
        _audienceStrictData = deserializer.Deserialize(audienceStrictStream);

        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _eventHandler = Mock.Of<IContextEventHandler>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    [Test]
    public void TestConstructorThrowsWithInvalidConfig()
    {
        var act = () => CreateContext(null!, _data);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Context configuration is required (Parameter 'config')");
    }
    
    [Test]
    public void TestConstructorSetsOverrides()
    {
        var overrides = new Dictionary<string, int>
        {
            ["exp_test"] = 2,
            ["exp_test_1"] = 1,
        };

        var config = new ContextConfig()
            .SetUnits(_units)
            .SetOverrides(overrides);

        var context = CreateContext(config, _data);

        foreach (var @override in overrides)
        {
            context.GetOverride(@override.Key).Should().Be(@override.Value);
        }
    }
    
    [Test]
    public void TestConstructorSetsCustomAssignments()
    {
        var customAssignments = new Dictionary<string, int>
        {
            ["exp_test"] = 2,
            ["exp_test_1"] = 1,
        };

        var config = new ContextConfig()
            .SetUnits(_units)
            .SetCustomAssignments(customAssignments);

        var context = CreateContext(config, _data);

        foreach (var customAssignment in customAssignments)
        {
            context.GetCustomAssignment(customAssignment.Key).Should().Be(customAssignment.Value);
        }
    }

    [Test]
    public void TestBecomesReadyWithCorrectData()
    {
        var context = CreateContext(_data);
        context.IsReady().Should().BeTrue();
        context.GetContextData().Should().BeSameAs(_data);
    }
    
    [Test]
    public void TestBecomesReadyAndFailedWithIncorrectData()
    {
        var context = CreateContext(null!);
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeTrue();
    }

    [Test]
    public void TestCallsEventLoggerWhenReady()
    {
        var context = CreateContext(_data);
        
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Ready, _data), Times.Once);
    }
    
    [Test]
    public void TestCallsEventLoggerWhenIncorrectData()
    {
        var context = CreateContext(null!);
        
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Error, "Context initialized with failed data."), Times.Once);
    }

    [Test]
    public void TestThrowsWhenDisposing()
    {
        var manualResetEvent = new ManualResetEventSlim();
        
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.Track("goal1", T.MapOf("amount", 123, "hours", 345));

        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(async () => await ManualResetPublish().ConfigureAwait(false));

        ThreadPool.QueueUserWorkItem(_ => context.Dispose());
        
        Thread.Sleep(1000);
        
        context.IsClosing().Should().BeTrue();
        context.IsClosed().Should().BeFalse();
        
        VerifyThrows(() => context.SetAttribute("attr1", "value1"));
        VerifyThrows(() => context.SetAttributes(new Dictionary<string, object>{ ["attr1"] = "value1" }));
        VerifyThrows(() => context.SetOverride("exp_test_ab", 2));
        VerifyThrows(() => context.SetOverrides(new Dictionary<string, int>{["exp_test_ab"] = 2}));
        VerifyThrows(() => context.SetUnit("test", "test"));
        VerifyThrows(() => context.SetUnits(new Dictionary<string, string>{["test"] = "test"}));
        VerifyThrows(() => context.SetCustomAssignment("exp_test_ab", 2));
        VerifyThrows(() => context.SetCustomAssignments(new Dictionary<string, int>{["exp_test_ab"] = 2}));
        VerifyThrows(() => context.PeekTreatment("exp_test_ab"));
        VerifyThrows(() => context.GetTreatment("exp_test_ab"));
        VerifyThrows(() => context.Track("goal1", null));
        VerifyThrows(() => context.Publish());
        VerifyThrows(() => context.Refresh());
        VerifyThrows(() => context.GetContextData());
        VerifyThrows(() => context.GetExperiments());
        VerifyThrows(() => context.GetVariableValue("banner.border", 17));
        VerifyThrows(() => context.PeekVariableValue("banner.border", 17));
        VerifyThrows(() => context.GetVariableKeys());
        
        manualResetEvent.Set();

        Task ManualResetPublish()
        {
            manualResetEvent.Wait();
            return Task.CompletedTask;
        }

        void VerifyThrows(Action act)
        {
            act.Should().Throw<InvalidOperationException>().WithMessage("ABSmartly Context is closing");
        }
    }

    [Test]
    public async Task TestThrowsWhenDisposingAsync()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.Track("goal1", T.MapOf("amount", 123, "hours", 345));

        await context.DisposeAsync();
        
        context.IsClosing().Should().BeFalse();
        context.IsClosed().Should().BeTrue();
        
        VerifyThrows(() => context.SetAttribute("attr1", "value1"));
        VerifyThrows(() => context.SetAttributes(new Dictionary<string, object>{ ["attr1"] = "value1" }));
        VerifyThrows(() => context.SetOverride("exp_test_ab", 2));
        VerifyThrows(() => context.SetOverrides(new Dictionary<string, int>{["exp_test_ab"] = 2}));
        VerifyThrows(() => context.SetUnit("test", "test"));
        VerifyThrows(() => context.SetUnits(new Dictionary<string, string>{["test"] = "test"}));
        VerifyThrows(() => context.SetCustomAssignment("exp_test_ab", 2));
        VerifyThrows(() => context.SetCustomAssignments(new Dictionary<string, int>{["exp_test_ab"] = 2}));
        VerifyThrows(() => context.PeekTreatment("exp_test_ab"));
        VerifyThrows(() => context.GetTreatment("exp_test_ab"));
        VerifyThrows(() => context.Track("goal1", null));
        VerifyThrows(() => context.Publish());
        VerifyThrows(() => context.Refresh());
        VerifyThrows(() => context.GetContextData());
        VerifyThrows(() => context.GetExperiments());
        VerifyThrows(() => context.GetVariableValue("banner.border", 17));
        VerifyThrows(() => context.PeekVariableValue("banner.border", 17));
        VerifyThrows(() => context.GetVariableKeys());
        
        void VerifyThrows(Action act)
        {
            act.Should().Throw<InvalidOperationException>().WithMessage("ABSmartly Context is closed");
        }
    }

    [Test]
    public void TestGetExperiments()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        context.GetExperiments().Should().BeEquivalentTo(_data.Experiments.Select(x => x.Name));
    }
    
    [Test]
    public void TestStartsRefreshTimerWhenReady()
    {
        Context context;
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            context = CreateContext(new ContextConfig { RefreshInterval = TimeSpan.FromMilliseconds(1) }, _data);
            context.IsReady().Should().BeTrue();
            context.IsFailed().Should().BeFalse();
        });
        
        Thread.Sleep(1000);

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.AtLeastOnce);
    }
    
    [Test]
    public void TestDoesNotStartRefreshTimerWhenFailed()
    {
        Context context;
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            context = CreateContext(new ContextConfig { RefreshInterval = TimeSpan.FromMilliseconds(1) }, null!);
            context.IsReady().Should().BeTrue();
            context.IsFailed().Should().BeTrue();
        });
        
        Thread.Sleep(1000);

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Never);
    }
    
    [Test]
    public void TestStartsPublishTimeoutWhenQueueNotEmpty()
    {
        Context context;
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            context = CreateContext(new ContextConfig { RefreshInterval = TimeSpan.FromMilliseconds(1) }, _data);
            context.IsReady().Should().BeTrue();
            context.IsFailed().Should().BeFalse();
        });
        
        Thread.Sleep(1000);

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.AtLeastOnce);
    }
    
    [Test]
    public void TestSetUnitsBeforeReady()
    {
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false)
            }
        };
        
        Context context = null!;
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var config = new ContextConfig()
                .SetUnits(_units);

            context = CreateContext(config, _data);
            context.IsReady().Should().BeTrue();
            context.IsFailed().Should().BeFalse();
            
            context.GetTreatment("exp_test_ab");
            context.Publish();
        });
        
        Thread.Sleep(1000);

        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestSetUnitEmpty()
    {
        var context = CreateReadyContext();
        
        var act = () => context.SetUnit("db_user_id", "");

        act.Should().Throw<ArgumentException>().WithMessage("Unit 'db_user_id' UID must not be blank.");
    }
    
    [Test]
    public void TestSetUnitThrowsOnAlreadySet()
    {
        var context = CreateReadyContext();
        
        var act = () => context.SetUnit("session_id", "123");

        act.Should().Throw<ArgumentException>().WithMessage("Unit 'session_id' already set.");
    }

    [Test]
    public void TestSetAttributesBeforeReady()
    {
        var config = new ContextConfig()
            .SetAttributes(new Dictionary<string, object>
            {
                ["attr1"] = "value1",
                ["attr2"] = "value2"
            });

        var context = CreateContext(config, _data);
        
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
    }
    
    [Test]
    public void TestSetOverride()
    {
        var context = CreateReadyContext();

        context.SetOverride("exp_test", 2);
        context.GetOverride("exp_test").Should().Be(2);

        context.SetOverride("exp_test", 3);
        context.GetOverride("exp_test").Should().Be(3);
        
        context.SetOverride("exp_test_2", 1);
        context.GetOverride("exp_test_2").Should().Be(1);
        
        context.SetOverrides(new Dictionary<string, int>
        {
            ["exp_test_new"] = 3,
            ["exp_test_new_2"] = 5,
        });
        
        context.GetOverride("exp_test").Should().Be(3);
        context.GetOverride("exp_test_2").Should().Be(1);
        context.GetOverride("exp_test_new").Should().Be(3);
        context.GetOverride("exp_test_new_2").Should().Be(5);
        context.GetOverride("exp_test_not_found").Should().BeNull();
    }
    
    [Test]
    public void TestSetOverrideClearsAssignmentCache()
    {
        var context = CreateReadyContext();

        var overrides = new Dictionary<string, int>
        {
            ["exp_test_new"] = 3,
            ["exp_test_new_2"] = 5,
        };
        
        context.SetOverrides(overrides);

        foreach (var (key, value) in overrides)
        {
            context.GetTreatment(key).Should().Be(value);
        }
        context.PendingCount.Should().Be(overrides.Count);
        
        // overriding again with the same variant shouldn't clear assignment cache
        foreach (var (key, value) in overrides)
        {
            context.SetOverride(key, value);
            context.GetTreatment(key).Should().Be(value);
        }
        context.PendingCount.Should().Be(overrides.Count);
        
        // overriding with the different variant should clear assignment cache
        foreach (var (key, value) in overrides)
        {
            context.SetOverride(key, value + 11);
            context.GetTreatment(key).Should().Be(value + 11);
        }
        context.PendingCount.Should().Be(overrides.Count * 2);
        
        // overriding a computed assignment should clear assignment cache
        context.GetTreatment("exp_test_ab").Should().Be(_expectedVariants["exp_test_ab"]);
        context.PendingCount.Should().Be(overrides.Count * 2 + 1);
        
        context.SetOverride("exp_test_ab", 9);
        context.GetTreatment("exp_test_ab").Should().Be(9);
        context.PendingCount.Should().Be(overrides.Count * 2 + 2);
    }
    
    [Test]
    public void TestSetOverridesBeforeReady()
    {
        var config = new ContextConfig()
            .SetOverride("exp_test", 2)
            .SetOverrides(new Dictionary<string, int>
            {
                ["exp_test_new"] = 3,
                ["exp_test_new_2"] = 5
            });

        var context = CreateContext(config, _data);
        
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.GetOverride("exp_test").Should().Be(2);
        context.GetOverride("exp_test_new").Should().Be(3);
        context.GetOverride("exp_test_new_2").Should().Be(5);
    }
    
    [Test]
    public void TestSetCustomAssignment()
    {
        var context = CreateReadyContext();

        context.SetCustomAssignment("exp_test", 2);
        context.GetCustomAssignment("exp_test").Should().Be(2);

        context.SetCustomAssignment("exp_test", 3);
        context.GetCustomAssignment("exp_test").Should().Be(3);
        
        context.SetCustomAssignment("exp_test_2", 1);
        context.GetCustomAssignment("exp_test_2").Should().Be(1);
        
        context.SetCustomAssignments(new Dictionary<string, int>
        {
            ["exp_test_new"] = 3,
            ["exp_test_new_2"] = 5,
        });
        
        context.GetCustomAssignment("exp_test").Should().Be(3);
        context.GetCustomAssignment("exp_test_2").Should().Be(1);
        context.GetCustomAssignment("exp_test_new").Should().Be(3);
        context.GetCustomAssignment("exp_test_new_2").Should().Be(5);
        context.GetCustomAssignment("exp_test_not_found").Should().BeNull();
    }

    [Test]
    public void TestSetCustomAssignmentDoesNotOverrideFullOnOrNotEligibleAssignments()
    {
        var context = CreateReadyContext();
        
        context.SetCustomAssignment("exp_test_not_eligible", 3);
        context.GetTreatment("exp_test_not_eligible").Should().Be(0);
        
        context.SetCustomAssignment("exp_test_fullon", 3);
        context.GetTreatment("exp_test_fullon").Should().Be(2);
    }
    

    [Test]
    public void TestSetCustomAssignmentClearsAssignmentCache()
    {
        var context = CreateReadyContext();

        var customAssignments = new Dictionary<string, int>
        {
            ["exp_test_ab"] = 2,
            ["exp_test_abc"] = 3,
        };
        
        foreach (var (key, _) in customAssignments)
        {
            context.GetTreatment(key).Should().Be(_expectedVariants[key]);
        }
        context.PendingCount.Should().Be(customAssignments.Count);
        
        context.SetCustomAssignments(customAssignments);
        foreach (var (key, value) in customAssignments)
        {
            context.GetTreatment(key).Should().Be(value);
        }
        context.PendingCount.Should().Be(customAssignments.Count * 2);
        
        // overriding again with the same variant shouldn't clear assignment cache
        foreach (var (key, value) in customAssignments)
        {
            context.SetCustomAssignment(key, value);
            context.GetTreatment(key).Should().Be(value);
        }
        context.PendingCount.Should().Be(customAssignments.Count * 2);
        
        // overriding with the different variant should clear assignment cache
        foreach (var (key, value) in customAssignments)
        {
            context.SetCustomAssignment(key, value + 11);
            context.GetTreatment(key).Should().Be(value + 11);
        }
        context.PendingCount.Should().Be(customAssignments.Count * 3);
    }
    
    [Test]
    public void TestSetCustomAssignmentsBeforeReady()
    {
        var config = new ContextConfig()
            .SetCustomAssignment("exp_test", 2)
            .SetCustomAssignments(new Dictionary<string, int>
            {
                ["exp_test_new"] = 3,
                ["exp_test_new_2"] = 5
            });

        var context = CreateContext(config, _data);
        
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.GetCustomAssignment("exp_test").Should().Be(2);
        context.GetCustomAssignment("exp_test_new").Should().Be(3);
        context.GetCustomAssignment("exp_test_new_2").Should().Be(5);
    }

    [Test]
    public void TestPeekTreatment()
    {
        var context = CreateReadyContext();

        foreach (var experiment in _data.Experiments)
        {
            context.PeekTreatment(experiment.Name).Should().Be(_expectedVariants[experiment.Name]);
        }
        context.PeekTreatment("not_found").Should().Be(0);
        
        foreach (var experiment in _data.Experiments)
        {
            context.PeekTreatment(experiment.Name).Should().Be(_expectedVariants[experiment.Name]);
        }
        context.PeekTreatment("not_found").Should().Be(0);

        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void TestPeekVariableValue()
    {
        var context = CreateReadyContext();

        var experiments = _data.Experiments.Select(x => x.Name).ToDictionary(x => x);
        
        foreach (var (variable, experimentName) in _variableExperiments)
        {
            var actual = context.PeekVariableValue(variable, 17);
            var eligible = experimentName != "exp_test_not_eligible";

            if (eligible && experiments.ContainsKey(experimentName))
            {
                actual.Should().Be(_expectedVariables[variable]);
            }
            else
            {
                actual.Should().Be(17);
            }
        }

        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void TestPeekVariableValueReturnsAssignedVariantOnAudienceMismatchNonStrictMode()
    {
        var context = CreateContext(_audienceData);

        context.PeekVariableValue("banner.size", "small").Should().Be("large");
    }
    
    [Test]
    public void TestPeekVariableValueReturnsControlVariantOnAudienceMismatchStrictMode()
    {
        var context = CreateContext(_audienceStrictData);

        context.PeekVariableValue("banner.size", "small").Should().Be("small");
    }

    [Test]
    public void TestGetVariableValue()
    {
        var context = CreateReadyContext();

        var experiments = _data.Experiments.Select(x => x.Name).ToDictionary(x => x);
        
        foreach (var (variable, experimentName) in _variableExperiments)
        {
            var actual = context.GetVariableValue(variable, 17);
            var eligible = experimentName != "exp_test_not_eligible";

            if (eligible && experiments.ContainsKey(experimentName))
            {
                actual.Should().Be(_expectedVariables[variable]);
            }
            else
            {
                actual.Should().Be(17);
            }
        }

        context.PendingCount.Should().Be(experiments.Count);
    }

    [Test]
    public void TestGetVariableValueQueuesExposureWithAudienceMismatchFalseOnAudienceMatch()
    {
        var context = CreateContext(_audienceData);
        context.SetAttribute("age", 21);
        
        context.GetVariableValue("banner.size", "small").Should().Be("large");
        context.PendingCount.Should().Be(1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Attributes = new []{ new Attribute{Name = "age", Value = 21, SetAt = _clock.Millis()}},
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false)
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestGetVariableValueQueuesExposureWithAudienceMismatchTrueOnAudienceMismatch()
    {
        var context = CreateContext(_audienceData);
        
        context.GetVariableValue("banner.size", "small").Should().Be("large");
        context.PendingCount.Should().Be(1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, true)
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestGetVariableValueQueuesExposureWithAudienceMismatchFalseAndControlVariantOnAudienceMismatchInStrictMode()
    {
        var context = CreateContext(_audienceStrictData);
        
        context.GetVariableValue("banner.size", "small").Should().Be("small");
        context.PendingCount.Should().Be(1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 0, _clock.Millis(), false, true, false, false, false, true)
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }

    [Test]
    public void TestGetVariableValueCallsEventLogger()
    {
        var context = CreateReadyContext();

        context.GetVariableValue("banner.border", null);
        context.GetVariableValue("banner.size", null);

        var exposures = new[]
        {
            Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false)
        };
        
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure, It.IsAny<object>()),
            Times.Exactly(exposures.Length));

        foreach (var expected in exposures)
        {
            Mock.Get(_eventLogger).Verify(x => 
                    x.HandleEvent(context, EventType.Exposure, It.Is<Exposure>(e => e.Equals(expected))),
                Times.Once);
        }
        
        // verify not called again with the same exposure
        Mock.Get(_eventLogger).Invocations.Clear();
        context.GetVariableValue("banner.border", null);
        context.GetVariableValue("banner.size", null);
        
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure, It.IsAny<object>()),
            Times.Never);
    }

    [Test]
    public void TestGetVariableKeys()
    {
        var context = CreateContext(_refreshedData);

        context.GetVariableKeys().Should().BeEquivalentTo(_variableExperiments);
    }
    
    [Test]
    public void TestPeekTreatmentReturnsOverrideVariant()
    {
        var context = CreateReadyContext();

        foreach (var experiment in _data.Experiments)
        {
            context.SetOverride(experiment.Name, _expectedVariants[experiment.Name] + 11);
        }
        context.SetOverride("not_found", 3);
        
        foreach (var experiment in _data.Experiments)
        {
            context.PeekTreatment(experiment.Name).Should().Be(_expectedVariants[experiment.Name] + 11);
        }
        context.PeekTreatment("not_found").Should().Be(3);
        
        foreach (var experiment in _data.Experiments)
        {
            context.PeekTreatment(experiment.Name).Should().Be(_expectedVariants[experiment.Name] + 11);
        }
        context.PeekTreatment("not_found").Should().Be(3);

        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void TestPeekTreatmentReturnsAssignedVariantOnAudienceMismatchNonStrictMode()
    {
        var context = CreateContext(_audienceData);

        context.PeekTreatment("exp_test_ab").Should().Be(1);
    }
    
    [Test]
    public void TestPeekTreatmentReturnsControlVariantOnAudienceMismatchStrictMode()
    {
        var context = CreateContext(_audienceStrictData);

        context.PeekTreatment("exp_test_ab").Should().Be(0);
    }

    [Test]
    public void TestGetTreatment()
    {
        var context = CreateReadyContext();
        
        foreach (var experiment in _data.Experiments)
        {
            context.GetTreatment(experiment.Name).Should().Be(_expectedVariants[experiment.Name]);
        }
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(_data.Experiments.Length + 1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false),
                Exposure(2, "exp_test_abc", "session_id", 2, _clock.Millis(), true, true, false, false, false, false),
                Exposure(3, "exp_test_not_eligible", "user_id", 0, _clock.Millis(), true, false, false, false, false, false),
                Exposure(4, "exp_test_fullon", "session_id", 2, _clock.Millis(), true, true, false, true, false, false),
                Exposure(0, "not_found", null!, 0, _clock.Millis(), false, true, false, false, false, false),
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }

    [Test]
    public void TestGetTreatmentStartsPublishTimeoutAfterExposure()
    {
        Context context;
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            context = CreateContext(new ContextConfig { PublishDelay = TimeSpan.FromMilliseconds(1) }, _data);
            context.IsReady().Should().BeTrue();
            context.IsFailed().Should().BeFalse();

            context.GetTreatment("exp_test_ab");
            context.GetTreatment("exp_test_abc");
        });
        
        Thread.Sleep(1000);

        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
    }
    
    [Test]
    public void TestGetTreatmentReturnsOverrideVariant()
    {
        var context = CreateReadyContext();
        
        foreach (var experiment in _data.Experiments)
        {
            context.SetOverride(experiment.Name, _expectedVariants[experiment.Name] + 11);
        }
        context.SetOverride("not_found", 3);
        
        foreach (var experiment in _data.Experiments)
        {
            context.GetTreatment(experiment.Name).Should().Be(_expectedVariants[experiment.Name] + 11);
        }
        context.GetTreatment("not_found").Should().Be(3);
        context.PendingCount.Should().Be(_data.Experiments.Length + 1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 12, _clock.Millis(), false, true, true, false, false, false),
                Exposure(2, "exp_test_abc", "session_id", 13, _clock.Millis(), false, true, true, false, false, false),
                Exposure(3, "exp_test_not_eligible", "user_id", 11, _clock.Millis(), false, true, true, false, false, false),
                Exposure(4, "exp_test_fullon", "session_id", 13, _clock.Millis(), false, true, true, false, false, false),
                Exposure(0, "not_found", null!, 3, _clock.Millis(), false, true, true, false, false, false),
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestGetTreatmentQueuesExposureOnce()
    {
        var context = CreateReadyContext();
        
        foreach (var experiment in _data.Experiments)
        {
            context.GetTreatment(experiment.Name);
        }
        context.GetTreatment("not_found");
        context.PendingCount.Should().Be(_data.Experiments.Length + 1);
        
        //call again
        foreach (var experiment in _data.Experiments)
        {
            context.GetTreatment(experiment.Name);
        }
        context.GetTreatment("not_found");
        context.PendingCount.Should().Be(_data.Experiments.Length + 1);
        
        context.Publish();
        
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
        
        context.PendingCount.Should().Be(0);
        
        foreach (var experiment in _data.Experiments)
        {
            context.GetTreatment(experiment.Name);
        }
        context.GetTreatment("not_found");
        context.PendingCount.Should().Be(0);
    }
    
    [Test]
    public void TestGetTreatmentQueuesExposureWithAudienceMismatchFalseOnAudienceMatch()
    {
        var context = CreateContext(_audienceData);
        context.SetAttribute("age", 21);
        
        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.PendingCount.Should().Be(1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Attributes = new []{ new Attribute{Name = "age", Value = 21, SetAt = _clock.Millis()}},
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false)
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestGetTreatmentQueuesExposureWithAudienceMismatchTrueOnAudienceMismatch()
    {
        var context = CreateContext(_audienceData);
        
        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.PendingCount.Should().Be(1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, true)
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestGetTreatmentQueuesExposureWithAudienceMismatchTrueAndControlVariantOnAudienceMismatchInStrictMode()
    {
        var context = CreateContext(_audienceStrictData);
        
        context.GetTreatment("exp_test_ab").Should().Be(0);
        context.PendingCount.Should().Be(1);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 0, _clock.Millis(), false, true, false, false, false, true)
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestGetTreatmentCallsEventLogger()
    {
        var context = CreateReadyContext();

        context.GetTreatment("exp_test_ab");
        context.GetTreatment("not_found");

        var exposures = new[]
        {
            Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false),
            Exposure(0, "not_found", null!, 0, _clock.Millis(), false, true, false, false, false, false),
        };
        
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure, It.IsAny<object>()),
            Times.Exactly(exposures.Length));

        foreach (var expected in exposures)
        {
            Mock.Get(_eventLogger).Verify(x => 
                    x.HandleEvent(context, EventType.Exposure, It.Is<Exposure>(e => e.Equals(expected))),
                Times.Once);
        }
        
        // verify not called again with the same exposure
        Mock.Get(_eventLogger).Invocations.Clear();
        context.GetTreatment("exp_test_ab");
        context.GetTreatment("not_found");
        
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure, It.IsAny<object>()),
            Times.Never);
    }
    
    [Test]
    public void TestTrack()
    {
        var context = CreateReadyContext();

        context.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        context.Track("goal2", new Dictionary<string, object>{ ["tries"] = 7 });

        context.PendingCount.Should().Be(2);
        
        context.Track("goal2", new Dictionary<string, object>{ ["tests"] = 12 });
        context.Track("goal3", null);
        
        context.PendingCount.Should().Be(4);

        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Goals = new[]
            {
                GoalAchievement("goal1", _clock.Millis(), new Dictionary<string, object> { ["amount"] = 125, ["hours"] = 245 }),
                GoalAchievement("goal2", _clock.Millis(), new Dictionary<string, object> { ["tries"] = 7 }),
                GoalAchievement("goal2", _clock.Millis(), new Dictionary<string, object> { ["tests"] = 12 }),
                GoalAchievement("goal3", _clock.Millis(), null!),
            }
        };
        
        context.Publish();
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
    }
    
    [Test]
    public void TestTrackCallsEventLogger()
    {
        var context = CreateReadyContext();

        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);

        var achievements = new[]
        {
            GoalAchievement("goal1", _clock.Millis(), properties)
        };
        
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(It.IsAny<IContext>(), EventType.Goal, It.IsAny<object>()),
            Times.Exactly(achievements.Length));

        foreach (var expected in achievements)
        {
            Mock.Get(_eventLogger).Verify(x => 
                    x.HandleEvent(context, EventType.Goal, It.Is<GoalAchievement>(e => e.Equals(expected))),
                Times.Once);
        }
        
        // verify called again with the same goal
        Mock.Get(_eventLogger).Invocations.Clear();
        context.Track("goal1", properties);
        
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(It.IsAny<IContext>(), EventType.Goal, It.IsAny<object>()),
            Times.Exactly(achievements.Length));

        foreach (var expected in achievements)
        {
            Mock.Get(_eventLogger).Verify(x => 
                    x.HandleEvent(context, EventType.Goal, It.Is<GoalAchievement>(e => e.Equals(expected))),
                Times.Once);
        }
    }
    
    [Test]
    public void TestTrackStartsPublishTimeoutAfterAchievement()
    {
        Context context;
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            context = CreateContext(new ContextConfig { PublishDelay = TimeSpan.FromMilliseconds(1) }, _data);
            context.IsReady().Should().BeTrue();
            context.IsFailed().Should().BeFalse();

            context.Track("goal1", new Dictionary<string, object> { ["amount"] = 125 });
            context.Track("goal2", new Dictionary<string, object> { ["value"] = 999.0 });
        });
        
        Thread.Sleep(1000);

        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
    }
    
    [Test]
    public void TestPublishDoesNotCallEventHandlerWhenQueueIsEmpty()
    {
        var context = CreateReadyContext();

        context.PendingCount.Should().Be(0);
        
        context.Publish();
        
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Never);
    }
    
    [Test]
    public void TestPublishCallsEventLogger()
    {
        var context = CreateReadyContext();

        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);

        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Goals = new[]
            {
                GoalAchievement("goal1", _clock.Millis(), properties),
            }
        };
        
        context.Publish();

        Mock.Get(_eventLogger).Verify(x =>
            x.HandleEvent(It.IsAny<IContext>(), EventType.Publish, It.IsAny<object>()), Times.Once);
        Mock.Get(_eventLogger).Verify(x => 
                x.HandleEvent(context, EventType.Publish, It.Is<PublishEvent>(e => e.Equals(expectedEvent))),
            Times.Once);
    }
    
    [Test]
    public void TestPublishCallsEventLoggerOnError()
    {
        var context = CreateReadyContext();

        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Throws(() => new Exception("ERROR"));
            
        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);

        var act = () => context.Publish();
        act.Should().Throw<Exception>().WithMessage("ERROR");

        Mock.Get(_eventLogger).Verify(x =>
            x.HandleEvent(It.IsAny<IContext>(), EventType.Error, It.IsAny<object>()), Times.Once);
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Error, "ERROR"), Times.Once);
    }
    
    [Test]
    public void TestPublishResetsInternalQueuesAndKeepsAttributesOverridesAndCustomAssignments()
    {
        var config = new ContextConfig()
            .SetUnits(_units)
            .SetAttributes(new Dictionary<string, object>
            {
                ["attr1"] = "value1",
                ["attr2"] = "value2",
            })
            .SetCustomAssignment("exp_test_abc", 3)
            .SetOverride("not_found", 3);
        
        var context = CreateContext(config, _data);
        
        context.PendingCount.Should().Be(0);

        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.GetTreatment("exp_test_abc").Should().Be(3);
        context.GetTreatment("not_found").Should().Be(3);
        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);
        
        context.PendingCount.Should().Be(4);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Exposures = new[]
            {
                Exposure(1, "exp_test_ab", "session_id", 1, _clock.Millis(), true, true, false, false, false, false),
                Exposure(2, "exp_test_abc", "session_id", 3, _clock.Millis(), true, true, false, false, true, false),
                Exposure(0, "not_found", null!, 3, _clock.Millis(), false, true, true, false, false, false),
            },
            Attributes = new []
            {
                new Attribute { Name = "attr1", Value = "value1", SetAt = _clock.Millis() },
                new Attribute { Name = "attr2", Value = "value2", SetAt = _clock.Millis() },
            },
            Goals = new[]
            {
                GoalAchievement("goal1", _clock.Millis(), properties)
            },
        };
        
        var manualResetEvent = new ManualResetEventSlim();
        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(async () => await ManualResetPublish(manualResetEvent).ConfigureAwait(false));

        ThreadPool.QueueUserWorkItem(_ => context.Publish());
        
        Thread.Sleep(1000);

        context.PendingCount.Should().Be(0);
        context.GetCustomAssignment("exp_test_abc").Should().Be(3);
        context.GetOverride("not_found").Should().Be(3);
        
        manualResetEvent.Set();

        context.PendingCount.Should().Be(0);
        context.GetCustomAssignment("exp_test_abc").Should().Be(3);
        context.GetOverride("not_found").Should().Be(3);
        
        VerifyPublishData(_eventHandler, context, expectedEvent);
        
        // repeat
        Mock.Get(_eventHandler).Invocations.Clear();
        
        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.GetTreatment("exp_test_abc").Should().Be(3);
        context.GetTreatment("not_found").Should().Be(3);
        context.Track("goal1", properties);
        
        var expectedNextEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Attributes = new []
            {
                new Attribute { Name = "attr1", Value = "value1", SetAt = _clock.Millis() },
                new Attribute { Name = "attr2", Value = "value2", SetAt = _clock.Millis() },
            },
            Goals = new[]
            {
                GoalAchievement("goal1", _clock.Millis(), properties)
            },
        };
        
        var manualResetNextEvent = new ManualResetEventSlim();
        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(async () => await ManualResetPublish(manualResetNextEvent).ConfigureAwait(false));

        ThreadPool.QueueUserWorkItem(_ => context.Publish());
        
        Thread.Sleep(1000);

        context.PendingCount.Should().Be(0);
        
        manualResetNextEvent.Set();

        context.PendingCount.Should().Be(0);
        
        VerifyPublishData(_eventHandler, context, expectedNextEvent);

        Task ManualResetPublish(ManualResetEventSlim eventSlim)
        {
            eventSlim.Wait();
            return Task.CompletedTask;
        }
    }
    
    [Test]
    public void TestPublishDoesNotCallEventHandlerWhenFailed()
    {
        var context = CreateContext(null!);
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeTrue();

        context.GetTreatment("exp_test_abc");
        context.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        context.PendingCount.Should().Be(2);
        
        context.Publish();
        
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Never);
    }
    
    [Test]
    public void TestPublishWithException()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        context.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        context.PendingCount.Should().Be(1);
        
        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Throws(() => new Exception("ERROR"));
        
        var act = () => context.Publish();
        act.Should().Throw<Exception>().WithMessage("ERROR");

        Mock.Get(_eventHandler).Verify(x =>
            x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
    }
    
    [Test]
    public void TestDispose()
    {
        var context = CreateReadyContext();
        context.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        
        context.Dispose();
        
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
    }
    
    [Test]
    public async Task TestDisposeAsync()
    {
        var context = CreateReadyContext();
        context.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        
        await context.DisposeAsync();
        
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
    }
    
    [Test]
    public async Task TestSecondDisposeNotThrows()
    {
        // async then sync
        var context1 = CreateReadyContext();
        context1.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        
        await context1.DisposeAsync();
        var act1 = () => context1.Dispose();

        act1.Should().NotThrow();
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
        
        // sync then async
        Mock.Get(_eventHandler).Invocations.Clear();
        var context2 = CreateReadyContext();
        context2.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });
        
        context2.Dispose();
        var act2 = async () => await context2.DisposeAsync();

        await act2.Should().NotThrowAsync();
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
        
        // concurrent dispose
        Mock.Get(_eventHandler).Invocations.Clear();
        var context3 = CreateReadyContext();
        context3.Track("goal1", new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 });

        var manualResetEvent = new ManualResetEventSlim();
        var exceptions = new List<Exception>();
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                manualResetEvent.Wait();
                context2.Dispose();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        });
        
        // ReSharper disable once AsyncVoidLambda
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                manualResetEvent.Wait();
                await context2.DisposeAsync();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        });

        manualResetEvent.Set();
        
        Thread.Sleep(1000);
        
        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);

        exceptions.Should().BeEmpty();
    }
    
    [Test]
    public void TestDisposeCallsEventLogger()
    {
        var context = CreateReadyContext();
        
        context.Dispose();
        
        Mock.Get(_eventLogger).Verify(x =>
            x.HandleEvent(It.IsAny<IContext>(), EventType.Close, It.IsAny<object>()), Times.Once);
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Close, null), Times.Once);
    }
    
    [Test]
    public void TestDisposeCallsEventLoggerWithPendingEvents()
    {
        var context = CreateReadyContext();
        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);
        
        var expectedEvent = new PublishEvent
        {
            Hashed = true,
            PublishedAt = _clock.Millis(),
            Units = _publishUnits,
            Goals = new[]
            {
                GoalAchievement("goal1", _clock.Millis(), properties)
            },
        };
        
        Mock.Get(_eventLogger).Invocations.Clear();
        context.Dispose();
        
        Mock.Get(_eventLogger).Verify(x =>
            x.HandleEvent(It.IsAny<IContext>(), It.IsAny<EventType>(), It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Publish, expectedEvent), Times.Once);
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Close, null), Times.Once);
    }
    
    [Test]
    public void TestDisposeCallsEventLoggerOnError()
    {
        var context = CreateReadyContext();
        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);
        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Throws(() => new Exception("ERROR"));
       
        Mock.Get(_eventLogger).Invocations.Clear();
        
        var act = () => context.Dispose();
        act.Should().Throw<Exception>().WithMessage("ERROR");

        Mock.Get(_eventLogger).Verify(x =>
            x.HandleEvent(It.IsAny<IContext>(), It.IsAny<EventType>(), It.IsAny<object>()), Times.Once);
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Error, "ERROR"), Times.Once);
    }
    
    [Test]
    public void TestDisposeCallsPublishOnError()
    {
        var context = CreateReadyContext();
        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);
        
        Mock.Get(_eventHandler).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Throws(() => new Exception("ERROR"));
       
        Mock.Get(_eventLogger).Invocations.Clear();
        
        var act = () => context.Dispose();
        act.Should().Throw<Exception>().WithMessage("ERROR");

        Mock.Get(_eventHandler).Verify(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
    }
    
    [Test]
    public void TestRefresh()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        SetupRefreshData();
       
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);

        var expectedExperiments = _refreshedData.Experiments.Select(x => x.Name).ToArray();

        context.GetExperiments().Should().BeEquivalentTo(expectedExperiments);
    }
    
    [Test]
    public void TestRefreshCallsEventLogger()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        SetupRefreshData();
       
        Mock.Get(_eventLogger).Invocations.Clear();
        context.Refresh();

        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Refresh, _refreshedData), Times.Once);
    }
    
    [Test]
    public void TestRefreshCallsEventLoggerOnError()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        Mock.Get(_dataProvider).Setup(x => x.GetContextDataAsync())
            .Throws(() => new Exception("ERROR"));

        Mock.Get(_eventLogger).Invocations.Clear();
        var act = () => context.Refresh();

        act.Should().Throw<Exception>().WithMessage("ERROR");
        Mock.Get(_eventLogger).Verify(x => x.HandleEvent(context, EventType.Error, "ERROR"), Times.Once);
    }
    
    [Test]
    public void TestRefreshWithException()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        var properties = new Dictionary<string, object>{ ["amount"] = 125, ["hours"] = 245 };
        context.Track("goal1", properties);
        
        Mock.Get(_dataProvider).Setup(x => x.GetContextDataAsync())
            .Throws(() => new Exception("ERROR"));

        var act = () => context.Refresh();
        act.Should().Throw<Exception>().WithMessage("ERROR");
        
        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
    }
    
    [Test]
    public async Task TestRefreshAsync()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        SetupRefreshData();
       
        await context.RefreshAsync();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);

        var expectedExperiments = _refreshedData.Experiments.Select(x => x.Name).ToArray();

        context.GetExperiments().Should().BeEquivalentTo(expectedExperiments);
    }
    
    [Test]
    public void TestRefreshKeepsAssignmentCacheWhenNotChanged()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        foreach (var experiment in _data.Experiments)
        {
            context.GetTreatment(experiment.Name);
        }
        context.GetTreatment("not_found");

        context.PendingCount.Should().Be(_data.Experiments.Length + 1);
        
        SetupRefreshData();
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        foreach (var experiment in _refreshedData.Experiments)
        {
            context.GetTreatment(experiment.Name);
        }
        context.GetTreatment("not_found");

        context.PendingCount.Should().Be(_refreshedData.Experiments.Length + 1);
    }
    
    [Test]
    public void TestRefreshKeepsAssignmentCacheWhenNotChangedOnAudienceMismatch()
    {
        var context = CreateContext(_audienceStrictData);
        context.IsReady().Should().BeTrue();

        context.GetTreatment("exp_test_ab").Should().Be(0);
        context.PendingCount.Should().Be(1);
        
        SetupRefreshData(_audienceStrictData);
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment("exp_test_ab").Should().Be(0);
        context.PendingCount.Should().Be(1);
    }
    
    [Test]
    public void TestRefreshKeepsAssignmentCacheWhenNotChangedWithOverride()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        context.SetOverride("exp_test_ab", 3);
        context.GetTreatment("exp_test_ab").Should().Be(3);
        context.PendingCount.Should().Be(1);
        
        SetupRefreshData();
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment("exp_test_ab").Should().Be(3);
        context.PendingCount.Should().Be(1);
    }
    
    [Test]
    public void TestRefreshClearAssignmentCacheForStoppedExperiment()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        var experimentName = "exp_test_abc";
        
        context.GetTreatment(experimentName).Should().Be(_expectedVariants[experimentName]);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(2);
        
        SetupRefreshData();
        _refreshedData.Experiments = _refreshedData.Experiments.Where(x => x.Name != experimentName).ToArray();
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment(experimentName).Should().Be(0);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }
    
    [Test]
    public void TestRefreshClearAssignmentCacheForStartedExperiment()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        var experimentName = "exp_test_new";
        
        context.GetTreatment(experimentName).Should().Be(0);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(2);
        
        SetupRefreshData();
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment(experimentName).Should().Be(_expectedVariants[experimentName]);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }
    
    [Test]
    public void TestRefreshClearAssignmentCacheForFullOnExperiment()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        var experimentName = "exp_test_abc";
        
        context.GetTreatment(experimentName).Should().Be(_expectedVariants[experimentName]);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(2);
        
        SetupRefreshData();
        foreach (var experiment in _refreshedData.Experiments.Where(x => x.Name == experimentName))
        {
            experiment.FullOnVariant.Should().Be(0);
            experiment.FullOnVariant = 1;
            experiment.FullOnVariant.Should().NotBe(_expectedVariants[experiment.Name]);
        }
        
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment(experimentName).Should().Be(1);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }
    
    [Test]
    public void TestRefreshClearAssignmentCacheForTrafficSplitChange()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        var experimentName = "exp_test_not_eligible";
        
        context.GetTreatment(experimentName).Should().Be(_expectedVariants[experimentName]);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(2);
        
        SetupRefreshData();
        foreach (var experiment in _refreshedData.Experiments.Where(x => x.Name == experimentName))
        {
            experiment.TrafficSplit = new[] { 0.0, 1.0 };
        }
        
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment(experimentName).Should().Be(2);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }
    
    [Test]
    public void TestRefreshClearAssignmentCacheForIterationChange()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        var experimentName = "exp_test_abc";
        
        context.GetTreatment(experimentName).Should().Be(_expectedVariants[experimentName]);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(2);
        
        SetupRefreshData();
        foreach (var experiment in _refreshedData.Experiments.Where(x => x.Name == experimentName))
        {
            experiment.Iteration = 2;
            experiment.TrafficSeedHi = 54870830;
            experiment.TrafficSeedLo = 398724581;
            experiment.SeedHi = 77498863;
            experiment.SeedLo = 34737352;
        }
        
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment(experimentName).Should().Be(2);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }
    
    [Test]
    public void TestRefreshClearAssignmentCacheForExperimentIdChange()
    {
        var context = CreateReadyContext();
        context.IsReady().Should().BeTrue();

        var experimentName = "exp_test_abc";
        
        context.GetTreatment(experimentName).Should().Be(_expectedVariants[experimentName]);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(2);
        
        SetupRefreshData();
        foreach (var experiment in _refreshedData.Experiments.Where(x => x.Name == experimentName))
        {
            experiment.Id = 11;
            experiment.TrafficSeedHi = 54870830;
            experiment.TrafficSeedLo = 398724581;
            experiment.SeedHi = 77498863;
            experiment.SeedLo = 34737352;
        }
        
        context.Refresh();

        Mock.Get(_dataProvider).Verify(x => x.GetContextDataAsync(), Times.Once);
        
        context.GetTreatment(experimentName).Should().Be(2);
        context.GetTreatment("not_found").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }
    
    
    
    
    
    
    
    
    
    #region Helpers

    private Context CreateContext(ContextConfig config, ContextData data) =>
        new(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory());
    
    private Context CreateContext(ContextData data) =>
        new(new ContextConfig().SetUnits(_units), data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory());

    private Context CreateReadyContext() => CreateContext(_data);

    private Exposure Exposure(int id, string name, string unit, int variant, long exposedAt, bool assigned,
        bool eligible, bool overriden, bool fullOn, bool custom, bool audienceMismatch) =>
        new()
        {
            Id = id,
            Name = name,
            Unit = unit,
            Variant = variant,
            ExposedAt = exposedAt,
            Assigned = assigned,
            Eligible = eligible,
            Overridden = overriden,
            FullOn = fullOn,
            Custom = custom,
            AudienceMismatch = audienceMismatch
        };

    private GoalAchievement GoalAchievement(string name, long achievedAt, IDictionary<string,object> properties) =>
        new()
        {
            Name = name,
            AchievedAt = achievedAt,
            Properties = properties
        };

    private void VerifyPublishData(IContextEventHandler handler, Context context, PublishEvent expectedEvent)
    {
        Mock.Get(handler).Verify(x => 
                x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()), Times.Once);
        
        Mock.Get(handler).Verify(x => 
                x.PublishAsync(context, It.Is<PublishEvent>(e => e.Equals(expectedEvent))),
            Times.Once);
    }

    private void SetupRefreshData(ContextData? data = null)
    {
        Mock.Get(_dataProvider).Setup(x => x.GetContextDataAsync())
            .Returns(GetContextData);

        Task<ContextData> GetContextData()
        {
            return Task.FromResult(data ?? _refreshedData);
        }
    }

    #endregion
}