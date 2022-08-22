using System;
using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.DotNet.Time;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Temp;

namespace ABSmartlySdk;

public class ABSmartly : IDisposable
{
    private Client _client;
    private readonly IContextDataProvider _contextDataProvider;
    private readonly IContextEventHandler _contextEventHandler;
    private readonly IContextEventLogger _contextEventLogger;
    private readonly IVariableParser _variableParser;

    private readonly IAudienceDeserializer _audienceDeserializer;
    private IScheduledExecutorService _scheduler;

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

        if (_contextDataProvider == null || _contextEventHandler == null)
        {
            _client = config.Client;
            if (_client is null) 
            {
                throw new ArgumentNullException(nameof(_client), "Missing Client instance");
            }

            _contextDataProvider ??= new DefaultContextDataProvider(_client);
            _contextEventHandler ??= new DefaultContextEventHandler(_client);
        }

        _variableParser ??= new DefaultVariableParser(config.LoggerFactory);

        _audienceDeserializer ??= new DefaultAudienceDeserializer(config.LoggerFactory);

        _scheduler ??= new ScheduledThreadPoolExecutor(1);
    }

    public Context CreateContext(ContextConfig config)
    {
        var context = new Context(
            clock: Clock.SystemUTC(),
            config: config,
            scheduledExecutorService: _scheduler,
            dataTask: _contextDataProvider.GetContextDataAsync(),
            dataProvider: _contextDataProvider,
            eventHandler: _contextEventHandler,
            eventLogger: _contextEventLogger,
            variableParser: _variableParser,
            audienceMatcher: new AudienceMatcher(_audienceDeserializer)
            );

        return context;
        //return Context.Create(Clock.SystemUTC(), config, _scheduler, _contextDataProvider.GetContextDataAsync(),
        //    _contextDataProvider, _contextEventHandler, _contextEventLogger, _variableParser,
        //    new AudienceMatcher(_audienceDeserializer));
    }

    //public Context CreateContext(ContextConfig config, ContextData data) 
    //{
    //    var context = new Context(
    //        clock: Clock.SystemUTC(),
    //        config: config,
    //        scheduledExecutorService: _scheduler,
    //        dataTask: Task.FromResult(data),
    //        dataProvider: _contextDataProvider,
    //        eventHandler: _contextEventHandler,
    //        eventLogger: _contextEventLogger,
    //        variableParser: _variableParser,
    //        audienceMatcher: new AudienceMatcher(_audienceDeserializer)
    //    );

    //    return context;
    //    //return Context.Create(Clock.SystemUTC(), config, _scheduler, Task.FromResult(data),
    //    //    _contextDataProvider, _contextEventHandler, _contextEventLogger, _variableParser,
    //    //    new AudienceMatcher(_audienceDeserializer));
    //}

    //public Task<ContextData> GetContextDataAsync()
    //{
    //    return _contextDataProvider.GetContextDataAsync();
    //}

    #region IDisposable

    public void Close()
    {
        if (_client is not null)
        {
            //_client.Close();
            _client = null;
        }

        if (_scheduler is not null) 
        {
            try
            {
                //_scheduler.AwaitTermination(5000, TimeUnit.MILLISECONDS);
            }
            catch (Exception ignored)
            {

            }

            _scheduler = null;
        }
    }

    public void Dispose()
    {
        Close();
    }

    #endregion
}