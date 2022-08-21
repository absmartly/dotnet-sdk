using System.Text.Json;
using ABSmartly.DefaultServiceImplementations;
using ABSmartly.DotNet.Time;
using ABSmartly.Interfaces;
using ABSmartly.Json;
using ABSmartly.Temp;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests;

[TestFixture]
public partial class ContextTests
{
    public static class ContextFactory
    {
        static Mock<ILoggerFactory> loggetFactory = new();

        private static ContextData data;
        private static ContextData refreshData;
        private static ContextData audienceData;
        private static ContextData audienceStrictData;

        private static Task<ContextData> dataTask;
        private static Task<ContextData> dataTaskReady;
        private static Task<ContextData> dataTaskFailed;

        private static Task<ContextData> refreshDataFutureReady;
        private static Task<ContextData> refreshDataFuture;

        private static Task<ContextData> audienceDataFutureReady;
        private static Task<ContextData> audienceStrictDataFutureReady;

        private static IContextDataProvider dataProvider;
        private static IContextEventLogger eventLogger;
        private static IContextEventHandler eventHandler;
        private static IVariableParser variableParser;
        private static AudienceMatcher audienceMatcher;
        private static IScheduledExecutorService scheduler;
        private static DefaultContextDataDeserializer deser = new(null);
        private static Clock clock = Clock.Fixed(1_620_000_000_000L);


        public static Context Create()
        {
            data = JsonSerializer.Deserialize<ContextData>(TestData.Json.Context);

            var context = new Context(
                clock: clock,
                config: new ContextConfig(null),
                scheduler: scheduler,
                dataTask: dataTask,
                dataProvider: dataProvider,
                eventHandler: eventHandler,
                eventLogger: eventLogger,
                variableParser: variableParser,
                audienceMatcher: audienceMatcher
            );

            return context;
        }
    }



    [Test]
    public void Constructor_CustomOverrides_Returns_Overrides()
    {
        var overrides = new Dictionary<string, int>()
        {
            { "exp_test", 2 },
            { "exp_test_1", 1 }
        };

        var config = new ContextConfig();
        var context = ContextFactory.Create();

        //context.GetOverride()
    }
}