using System.Text.Json;
using ABSmartly.DotNet.Standard.UnitTests._TestUtils;
using ABSmartly.Internal;
using ABSmartly.Time;
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

        private static IABSmartlyServiceClient client;
        private static ClientConfig clientConfig;

        private static ContextConfig contextConfig;
        private static IContextDataProvider contextDataProvider;
        private static IContextEventLogger contextEventLogger;
        private static IContextEventHandler contextEventHandler;
        private static IVariableParser variableParser;
        private static AudienceMatcher audienceMatcher;

        private static IContextDataDeserializer contextDataDeserializer;
        private static IContextEventSerializer contextEventSerializer;

        public static Context CreateDefault()
        {
            clock = Clock.Fixed(1_620_000_000_000L);

            contextData = JsonSerializer.Deserialize<ContextData>(TestData.Json.Context);
            dataTask = Task.FromResult(contextData);

            contextDataDeserializer = new DefaultContextDataDeserializer(loggerFactory.Object);
            contextEventSerializer = new DefaultContextEventSerializer(loggerFactory.Object);

            var clientConfiguration = new ABSmartlyServiceConfiguration
            {
                Environment = "unittest",
                Application = "unittest",
                Endpoint = "testendpoint",
                ApiKey = "testkex"
            };
            clientConfig = new ClientConfig(
                clientConfiguration,
                contextDataDeserializer: contextDataDeserializer,
                contextEventSerializer: contextEventSerializer
            );
            client = new Client(clientConfig, httpclientFactory.Object, loggerFactory.Object);
            
            contextConfig = new ContextConfig();

            contextDataProvider = new DefaultContextDataProvider(client);
            contextEventLogger = new DefaultContextEventLogger();
            contextEventHandler = new DefaultContextEventHandler(client);
            variableParser = new DefaultVariableParser(loggerFactory.Object);
            audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(loggerFactory.Object));

            var context = new Context(
                config: contextConfig,
                clock: clock,
                dataTask: dataTask,

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