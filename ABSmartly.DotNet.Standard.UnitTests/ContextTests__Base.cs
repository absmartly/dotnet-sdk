using System.Text.Json;
using ABSmartly.DefaultServiceImplementations;
using ABSmartly.DotNet.Time;
using ABSmartly.Interfaces;
using ABSmartly.Json;
using ABSmartly.Temp;
using Microsoft.Extensions.Logging;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests;

[TestFixture]
public partial class ContextTests : TestCases
{
    public static class ContextFactory
    {
        static readonly Mock<ILoggerFactory> loggerFactory = new();
        static readonly Mock<IHttpClientFactory> httpclientFactory = new();

        private static ContextData contextData;
        private static ContextData contextRefreshData;
        private static ContextData contextAudienceData;
        private static ContextData contextAudienceStrictData;

        private static Task<ContextData> dataTask;
        private static Task<ContextData> dataTaskReady;
        private static Task<ContextData> dataTaskFailed;

        private static Task<ContextData> refreshDataFutureReady;
        private static Task<ContextData> refreshDataFuture;

        private static Task<ContextData> audienceDataFutureReady;
        private static Task<ContextData> audienceStrictDataFutureReady;


        private static Clock clock;

        private static IClient client;
        private static ClientConfig clientConfig;

        private static ContextConfig contextConfig;
        private static IContextDataProvider contextDataProvider;
        private static IContextEventLogger contextEventLogger;
        private static IContextEventHandler contextEventHandler;
        private static IVariableParser variableParser;
        private static AudienceMatcher audienceMatcher;
        private static IScheduledExecutorService scheduledExecutorService;

        private static IContextDataDeserializer contextDataDeserializer;
        private static IContextEventSerializer contextEventSerializer;
        private static IExecutor executor;

        public static Context CreateDefault()
        {
            clock = Clock.Fixed(1_620_000_000_000L);

            contextData = JsonSerializer.Deserialize<ContextData>(TestData.Json.Context);
            dataTask = Task.FromResult(contextData);

            contextDataDeserializer = new DefaultContextDataDeserializer(loggerFactory.Object);
            contextEventSerializer = new DefaultContextEventSerializer(loggerFactory.Object);
            executor = new DefaultExecutor();

            clientConfig = new ClientConfig(
                endpoint: "testendpoint",
                apiKey: "testkex",
                environment: "unittest",
                application: "unittest",
                contextDataDeserializer: contextDataDeserializer,
                contextEventSerializer: contextEventSerializer,
                executor: executor
            );
            client = new Client(clientConfig, httpclientFactory.Object, loggerFactory.Object);
            
            contextConfig = new ContextConfig();

            contextDataProvider = new DefaultContextDataProvider(client);
            contextEventLogger = new DefaultContextEventLogger();
            contextEventHandler = new DefaultContextEventHandler(client);
            variableParser = new DefaultVariableParser(loggerFactory.Object);
            audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(loggerFactory.Object));
            scheduledExecutorService = new ScheduledThreadPoolExecutor(1);

            var context = new Context(
                config: contextConfig,
                clock: clock,
                dataTask: dataTask,

                scheduledExecutorService: scheduledExecutorService,
                dataProvider: contextDataProvider,
                eventHandler: contextEventHandler,
                eventLogger: contextEventLogger,
                variableParser: variableParser,
                audienceMatcher: audienceMatcher
            );

            return context;
        }
    }




}