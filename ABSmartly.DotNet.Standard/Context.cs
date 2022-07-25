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
    private readonly int _pendingCount;
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

    #region Builder

    public static Context Create(Clock clock, 
                                 ContextConfig config,
                                 ScheduledExecutorService scheduler,
                                 Task<ContextData> dataFuture, 
                                 IContextDataProvider dataProvider,
                                 IContextEventHandler eventHandler, 
                                 IContextEventLogger? eventLogger,
                                 IVariableParser variableParser, 
                                 AudienceMatcher audienceMatcher) 
    {
        return new Context(clock, config, scheduler, dataFuture, dataProvider, eventHandler, eventLogger,
            variableParser, audienceMatcher);
    }    

    #endregion





    public bool IsReady() {
        return _data != null;
    }

    public bool IsFailed() {
        return _failed;
    }

    public bool IsClosed() {
        return _closed.Get();
    }

    public bool IsClosing() {
        return !_closed.Get() && _closing.Get();
    }

    public Task<Context> WaitUntilReadyAsync() {
        if (_data != null) 
        {
            return Task.FromResult(this);
        }
        else 
        {
            //return readyFuture_.thenApply(new Function<Void, Context>() {
            //    @Override
            //    public Context apply(Void k) {
            //    return Context.this;
            //}

            //}
            //    );

            // Todo: finish..
            throw new NotImplementedException();
        }
    }

    public Context WaitUntilReady() 
    {
        if (_data == null) 
        {
            Task future = _readyFuture; // cache here to avoid locking
            if (future != null && !future.IsCompleted) 
            {
                //future.join();

                // Todo: finish..
                throw new NotImplementedException();
            }
        }
        return this;
    }

    public string[] GetExperiments() {
        CheckReady(true);

        try 
        {
            _dataLock.EnterReadLock();
            //dataLock_.readLock().lock();
            string[] experimentNames = new string[_data.Experiments.Length];

            int index = 0;

            foreach (var dataExperiment in _data.Experiments)
            {
                experimentNames[index] = dataExperiment.Name;
                index++;
            }

            return experimentNames;
        } 
        finally 
        {
            _dataLock.ExitReadLock();
        }
    }

    public ContextData GetData() 
    {
        CheckReady(true);

        try 
        {
            _dataLock.EnterReadLock();
            return _data;
        } 
        finally 
        {
            _dataLock.ExitReadLock();
        }
    }

    #region Override

    public void SetOverride(string experimentName, int variant) 
    {
        CheckNotClosed();

        Concurrency.PutRW(_contextLock, _overrides, experimentName, variant);
    }

    public int GetOverride(string experimentName) 
    {
        return Concurrency.GetRW(_contextLock, _overrides, experimentName);
    }

    public void SetOverrides(Dictionary<string, int> overrides) 
    {
        foreach (var kvp in overrides)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            SetOverride(key, value);
        }
    }    

    #endregion

    #region CustomAssignment

    public void SetCustomAssignment(string experimentName, int variant) 
    {
        CheckNotClosed();

        Concurrency.PutRW(_contextLock, _cassignments, experimentName, variant);
    }

    public int GetCustomAssignment(string experimentName) 
    {
        return Concurrency.GetRW(_contextLock, _cassignments, experimentName);
    }

    public void SetCustomAssignments(Dictionary<string, int> customAssignments) 
    {
        foreach (var kvp in customAssignments)
        {
            SetCustomAssignment(kvp.Key, kvp.Value);
        }
    }

    #endregion

    #region Unit

    public void SetUnit(string unitType, string uid) 
    {
        CheckNotClosed();

        try 
        {
            _contextLock.EnterWriteLock();

            string previous = _units[unitType];
            if ((previous != null) && !previous.Equals(uid))
            {
                throw new Exception(string.Format("Unit '%s' already set.", unitType));
            }

            string trimmed = uid.Trim();
            if (string.IsNullOrEmpty(trimmed)) 
            {
                throw new Exception(string.Format("Unit '%s' UID must not be blank.", unitType));
            }

            _units.Add(unitType, trimmed);
        } 
        finally 
        {
            _contextLock.ExitWriteLock();
        }
    }

    public void setUnits(Dictionary<string, string> units) 
    {
        foreach (var kvp in units)
        {
            SetUnit(kvp.Key, kvp.Value);
        }
    }

    #endregion

    #region Attribute

    public void SetAttribute(string name, object value) 
    {
        CheckNotClosed();

        Concurrency.AddRW(_contextLock, _attributes, new Attribute(name, value, _clock.Millis()));
    }

    public void SetAttributes(Dictionary<string, object> attributes) 
    {
        foreach (var kvp in attributes)
        {
            SetAttribute(kvp.Key, kvp.Value);
        }
    }

    #endregion


    public int GetTreatment(string experimentName) 
    {
        CheckReady(true);

        Assignment assignment = GetAssignment(experimentName);
        if (!assignment.exposed.get()) 
        {
            QueueExposure(assignment);
        }

        return assignment.Variant;
    }

    private void QueueExposure(Assignment assignment) 
    {
        if (assignment.Exposed.CompareAndSet(false, true)) 
        {
            Exposure exposure = new Exposure
            {
                Id = assignment.Id,
                Name = assignment.Name,
                Unit = assignment.UnitType,
                Variant = assignment.Variant,
                ExposedAt = _clock.Millis(),
                Assigned = assignment.Assigned,
                Eligible = assignment.Eligible,
                Overridden = assignment.Overridden,
                FullOn = assignment.FullOn,
                Custom = assignment.Custom,
                AudienceMismatch = assignment.AudienceMismatch
            };

            try 
            {
                // Todo: add lock
                //eventLock_.lock();
                _pendingCount.IncrementAndGet();
                _exposures.Add(exposure);
            } 
            finally 
            {
                //eventLock_.unlock();
            }

            LogEvent(ContextEventLogger.EventType.Exposure, exposure);

            SetTimeout();
        }
    }

    public int PeekTreatment(string experimentName) 
    {
        CheckReady(true);

        return GetAssignment(experimentName).variant;
    }

    public Dictionary<string, string> GetVariableKeys() {
        CheckReady(true);

        var variableKeys = new Dictionary<string, string>(_indexVariables.Count);

        try 
        {
            _dataLock.EnterReadLock();

            foreach (var kvp in _indexVariables)
            {
                variableKeys.Add(kvp.Key, kvp.Value.Data.Name);
            }
        } 
        finally 
        {
            _dataLock.ExitReadLock();
        }
        
        return variableKeys;
    }

    public object GetVariableValue(string key, object defaultValue) 
    {
        CheckReady(true);

        Assignment assignment = GetVariableAssignment(key);
        if (assignment != null) 
        {
            if (assignment.Variables != null) 
            {
                if (!assignment.Exposed.Get()) 
                {
                    QueueExposure(assignment);
                }

                if (assignment.Variables.ContainsKey(key)) 
                {
                    return assignment.Variables[key];
                }
            }
        }

        return defaultValue;
    }

    public object PeekVariableValue(string key, object defaultValue) 
    {
        CheckReady(true);

        Assignment assignment = GetVariableAssignment(key);
        if (assignment != null) 
        {
            if (assignment.Variables != null) 
            {
                if (assignment.Variables.ContainsKey(key)) {
                    return assignment.Variables[key];
                }
            }
        }

        return defaultValue;
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
    public int Id { get; set; }
    public int Iteration { get; set; }
    public int FullOnVariant { get; set; }
    public string Name { get; set; }
    public string UnitType { get; set; }
    public double[] TrafficSplit { get; set; }
    public int Variant { get; set; }
    public bool Assigned { get; set; }
    public bool Overridden { get; set; }
    public bool Eligible { get; set; }
    public bool FullOn { get; set; }
    public bool Custom { get; set; }

    public bool AudienceMismatch { get; set; }

    public Dictionary<string, object> Variables = new Dictionary<string, object>();

    //final AtomicBoolean exposed = new AtomicBoolean(false);
}

internal class ExperimentVariables 
{
    public Experiment Data { get; set; }
    public List<Dictionary<string, object>> Variables { get; set; }
}