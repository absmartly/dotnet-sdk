using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ABSmartly.DotNet.Time;
using ABSmartly.Internal;
using ABSmartly.Json;
using ABSmartly.Temp;
using Attribute = ABSmartly.Json.Attribute;

namespace ABSmartly;

public class Context : IDisposable
{
    private readonly Clock _clock;
	private readonly long _publishDelay;
	private readonly long _refreshInterval;
	private readonly IContextEventHandler _eventHandler;
	private readonly IContextEventLogger _eventLogger;
	private readonly IContextDataProvider _dataProvider;
	private readonly IVariableParser _variableParser;
	private readonly AudienceMatcher _audienceMatcher;
	private readonly ScheduledExecutorService _scheduler;
	private readonly Dictionary<string, string> _units;
	private bool _failed;

	private readonly ReaderWriterLockSlim _dataLock = new ReaderWriterLockSlim();
	private ContextData _data;
	private Dictionary<string, ExperimentVariables> _index;
	private Dictionary<string, ExperimentVariables> _indexVariables;

    private readonly ReaderWriterLockSlim _contextLock = new ReaderWriterLockSlim();

	private readonly Dictionary<string, byte[]> _hashedUnits;
	private readonly Dictionary<string, VariantAssigner> _assigners;
	private readonly Dictionary<string, Assignment> _assignmentCache = new();

	private readonly ReaderWriterLockSlim _eventLock = new ReaderWriterLockSlim();
	private readonly List<Exposure> _exposures = new List<Exposure>();
	private readonly List<GoalAchievement> _achievements = new List<GoalAchievement>();

	private readonly List<Attribute> _attributes = new List<Attribute>();
	private readonly Dictionary<string, int> _overrides;
	private readonly Dictionary<string, int> _cassignments;

	// AtomicInteger
    private readonly int pendingCount_;
	// AtomicBoolean
    private readonly bool _closing;
    // AtomicBoolean
    private readonly bool _closed;
    // AtomicBoolean
    private readonly bool _refreshing;

	private volatile Task _readyFuture;
	private volatile Task _closingFuture;
	private volatile Task _refreshFuture;

	private readonly ReaderWriterLockSlim _timeoutLock = new ReaderWriterLockSlim();

	// ScheduledFuture<?>
    private volatile Task<object> _timeout = null;
    // ScheduledFuture<?>
    private volatile Task<object> _refreshTimer = null;

    #region Constructor

    	private Context(Clock clock, ContextConfig config, ScheduledExecutorService scheduler,
			Task<ContextData> dataFuture, IContextDataProvider dataProvider,
			IContextEventHandler eventHandler, IContextEventLogger eventLogger, IVariableParser variableParser,
			AudienceMatcher audienceMatcher) 
        {
		_clock = clock;
		_publishDelay = config.GetPublishDelay();
		_refreshInterval = config.GetRefreshInterval();
		_eventHandler = eventHandler;
		_eventLogger = config.GetEventLogger() != null ? config.GetEventLogger() : eventLogger;
		_dataProvider = dataProvider;
		_variableParser = variableParser;
		_audienceMatcher = audienceMatcher;
		_scheduler = scheduler;

		_units = new Dictionary<String, String>();

		Dictionary<String, String> units = config.GetUnits();
		if (units != null) {
			SetUnits(units);
		}

		_assigners = new Dictionary<String, VariantAssigner>(_units.Count);
		_hashedUnits = new Dictionary<String, byte[]>(_units.Count);

		Dictionary<String, Object> attributes = config.GetAttributes();
		if (attributes != null) {
			SetAttributes(attributes);
		}

        Dictionary<String, int> overrides = config.GetOverrides();
		_overrides = (overrides != null) ? new Dictionary<String, int>(overrides) : new Dictionary<String, int>();

        Dictionary<String, int> cassignments = config.GetCustomAssignments();
		_cassignments = (cassignments != null) ? new Dictionary<String, int>(cassignments)
				: new Dictionary<String, int>();

		// Todo: simplify it..
		//if (dataFuture.IsCompleted) {
		//	dataFuture.thenAccept(new Consumer<ContextData>() 
  //          {
		//		@Override
		//		public void accept(ContextData data) {
		//			Context.this.setData(data);
		//			Context.this.logEvent(ContextEventLogger.EventType.Ready, data);
		//		}
		//	}).exceptionally(new Function<Throwable, Void>() {
		//		@Override
		//		public Void apply(Throwable exception) {
		//			Context.this.setDataFailed(exception);
		//			Context.this.logError(exception);
		//			return null;
		//		}
		//	});
		//} 
  //      else 
  //      {
		//	_readyFuture = new Task();
		//	dataFuture.thenAccept(new Consumer<ContextData>() 
  //          {
		//		@Override
		//		public void accept(ContextData data) {
		//			Context.this.setData(data);
		//			readyFuture_.complete(null);
		//			readyFuture_ = null;

		//			Context.this.logEvent(ContextEventLogger.EventType.Ready, data);

		//			if (Context.this.getPendingCount() > 0) {
		//				Context.this.setTimeout();
		//			}
		//		}
		//	}).exceptionally(new Function<Throwable, Void>() 
  //          {
		//		@Override
		//		public Void apply(Throwable exception) {
		//			Context.this.setDataFailed(exception);
		//			readyFuture_.complete(null);
		//			readyFuture_ = null;

		//			Context.this.logError(exception);

		//			return null;
		//		}
		//	});
		//}
	}

    #endregion


    public static Context Create(Clock clock, ContextConfig config,
    ScheduledExecutorService scheduler,
        Task<ContextData> dataFuture, IContextDataProvider dataProvider,
        IContextEventHandler eventHandler, IContextEventLogger? eventLogger,
        IVariableParser variableParser, AudienceMatcher audienceMatcher) 
    {
        return new Context(clock, config, scheduler, dataFuture, dataProvider, eventHandler, eventLogger,
            variableParser, audienceMatcher);
    }



    public bool IsReady() {
        return _data != null;
    }

    public bool isFailed() {
        return _failed;
    }

    public bool isClosed() {
        return _closed.Get();
    }

    public bool isClosing() {
        return !_closed.get() && _closing.get();
    }




    #region IDisposable

    public void Dispose()
    {
        _dataLock?.Dispose();
        _contextLock?.Dispose();
        _eventLock?.Dispose();
        _readyFuture?.Dispose();
        _closingFuture?.Dispose();
        _refreshFuture?.Dispose();
        _timeoutLock?.Dispose();
        _timeout?.Dispose();
        _refreshTimer?.Dispose();
    }

    #endregion
}


internal class Assignment 
{
    int id;
    int iteration;
    int fullOnVariant;
    String name;
    String unitType;
    double[] trafficSplit;
    int variant;
    bool assigned;
    bool overridden;
    bool eligible;
    bool fullOn;
    bool custom;

    bool audienceMismatch;
    private Dictionary<String, Object> variables = new Dictionary<string, object>();

    //final AtomicBoolean exposed = new AtomicBoolean(false);
}

internal class ExperimentVariables {
    Experiment data;
    List<Dictionary<String, Object>> variables;
}