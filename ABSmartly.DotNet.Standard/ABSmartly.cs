using System;
using System.Threading.Tasks;
using ABSmartly.DotNet.Time;
using ABSmartly.Json;
using ABSmartly.Temp;

namespace ABSmartly;

public class ABSmartly : IDisposable
{
    private Client _client;
    private IContextDataProvider _contextDataProvider;
    private IContextEventHandler _contextEventHandler;
    private IContextEventLogger _contextEventLogger;
    private IVariableParser _variableParser;

    private IAudienceDeserializer _audienceDeserializer;
    private ScheduledExecutorService _scheduler;


    public static ABSmartly Create(ABSmartlyConfig config)
    {
        return new ABSmartly(config);
    }

    public ABSmartly(ABSmartlyConfig config)
    {
        _contextDataProvider = config.ContextDataProvider;
        _contextEventHandler = config.ContextEventHandler;
        _contextEventLogger = config.ContextEventLogger;
        _variableParser = config.VariableParser;
        _audienceDeserializer = config.AudienceDeserializer;
        _scheduler = config.Scheduler;

        if ((_contextDataProvider == null) || (_contextEventHandler == null)) {
            _client = config.Client;
            if (_client == null) {
                throw new ArgumentNullException(nameof(_client), "Missing Client instance");
            }

            if (_contextDataProvider == null) {
                _contextDataProvider = new DefaultContextDataProvider(_client);
            }

            if (_contextEventHandler == null) {
                _contextEventHandler = new DefaultContextEventHandler(_client);
            }
        }

        if (_variableParser == null) {
            _variableParser = new DefaultVariableParser();
        }

        if (_audienceDeserializer == null) {
            _audienceDeserializer = new DefaultAudienceDeserializer();
        }

        if (_scheduler == null) {
            _scheduler = new ScheduledThreadPoolExecutor(1);
        }
    }

    public Context CreateContext(ContextConfig config)
    {
        return Context.Create(Clock.systemUTC(), config, _scheduler, _contextDataProvider.GetContextData(),
            _contextDataProvider, _contextEventHandler, _contextEventLogger, _variableParser,
            new AudienceMatcher(_audienceDeserializer));
    }

    public Context CreateContextWith(ContextConfig config, ContextData data) 
    {
        return Context.Create(Clock.systemUTC(), config, _scheduler, Task.FromResult(data),
            _contextDataProvider, _contextEventHandler, _contextEventLogger, _variableParser,
            new AudienceMatcher(_audienceDeserializer));
    }

    public Task<ContextData> GetContextDataAsync()
    {
        return _contextDataProvider.GetContextDataAsync();
    }

    #region IDisposable

    public void Dispose()
    {
        if (_client != null) {
            _client.Close();
            _client = null;
        }

        if (_scheduler != null) 
        {
            try
            {
                _scheduler.AwaitTermination(5000, TimeUnit.MILLISECONDS);
            }
            catch (InterruptedException ignored)
            {

            }

            _scheduler = null;
        }
    }

    #endregion
}