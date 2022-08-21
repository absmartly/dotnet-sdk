using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ABSmartly.DefaultServiceImplementations;
using ABSmartly.Definitions;
using ABSmartly.DotNet.Time;
using ABSmartly.Interfaces;
using ABSmartly.Internal;
using ABSmartly.Internal.Hashing;
using ABSmartly.Json;
using ABSmartly.Temp;
using ABSmartly.Utils;
using Microsoft.Extensions.Logging;
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
	private readonly IScheduledExecutorService _scheduler;
	private readonly Dictionary<string, string> _units;
	private bool _failed;

	private readonly ABLock _dataLock = new();
	private ContextData _data;
	private Dictionary<string, ExperimentVariables> _index;
	private Dictionary<string, ExperimentVariables> _indexVariables;

    private readonly ABLock _contextLock = new();

	private readonly Dictionary<string, byte[]> _hashedUnits;
	private readonly Dictionary<string, VariantAssigner> _assigners;
	private readonly Dictionary<string, Assignment> _assignmentCache = new();

    //private readonly Monitor _eventLock; => replaced with Monitor
	private readonly List<Exposure> _exposures = new();
	private readonly List<GoalAchievement> _achievements = new();

	private readonly List<Attribute> _attributes = new();
	private readonly Dictionary<string, int> _overrides;
	private readonly Dictionary<string, int> _cassignments;

	// AtomicInteger
    public int PendingCount { get; private set; }
	// AtomicBoolean
    private readonly bool _closing;
    // AtomicBoolean
    private readonly bool _closed;
    // AtomicBoolean
    private readonly bool _refreshing;

	private volatile Task _readyTask;
	private volatile Task _closingTask;
	private volatile Task _refreshTask;

	private readonly ReaderWriterLockSlim _timeoutLock = new();

	// ScheduledFuture<?>
    private volatile Task<object> _timeout;
    // ScheduledFuture<?>
    private volatile Task<object> _refreshTimer;

    #region Constructor & Initialization

    /// <summary>
    /// Only pass valid service implementations at this point!!!
    /// </summary>
    public Context(
        //IHttpClientFactory httpClientFactory = null, 
        //ILoggerFactory loggerFactory = null,
        Clock clock = null,
        ContextConfig config = null, 
        //IClient client = null,
        Task<ContextData> dataTask = null,
        IScheduledExecutorService scheduler = null, 
        IContextDataProvider dataProvider = null,
        IContextEventHandler eventHandler = null, 
        IContextEventLogger eventLogger = null, 
        IVariableParser variableParser = null,
        AudienceMatcher audienceMatcher = null)
    {
        #region Property Assignment

        //clock ??= Clock.SystemUTC();
        //config ??= new ContextConfig();
        //client ??= new Client(config, httpClientFactory, loggerFactory);
        //scheduler ??= new ScheduledThreadPoolExecutor(10);
        //dataProvider ??= new DefaultContextDataProvider(null);
        //eventHandler ??= new DefaultContextEventHandler(null);
        //eventLogger ??= new DefaultContextEventLogger();
        //variableParser ??= new DefaultVariableParser(loggerFactory);
        //audienceMatcher ??= new AudienceMatcher(new DefaultAudienceDeserializer(loggerFactory));

        _clock = clock;
		_publishDelay = config.GetPublishDelay();
		_refreshInterval = config.GetRefreshInterval();
		_eventHandler = eventHandler;
		_eventLogger = config.EventLogger ?? eventLogger;
		_dataProvider = dataProvider;
		_variableParser = variableParser;
		_audienceMatcher = audienceMatcher;
		_scheduler = scheduler;        

        #endregion

        #region Initialization

        _units = new Dictionary<string, string>();

        var units = config.GetUnits();
		if (units != null) 
        {
			SetUnits(units);
		}

		_assigners = new Dictionary<string, VariantAssigner>(_units.Count);
		_hashedUnits = new Dictionary<string, byte[]>(_units.Count);

		var attributes = config.GetAttributes();
		if (attributes != null) {
			SetAttributes(attributes);
		}

        var overrides = config.GetOverrides();
		_overrides = overrides != null ? new Dictionary<string, int>(overrides) : new Dictionary<string, int>();

        var cassignments = config.GetCustomAssignments();
		_cassignments = cassignments != null ? new Dictionary<string, int>(cassignments) : new Dictionary<string, int>();

        if (dataTask.IsCompleted)
        {

        }
        else
        {
            
        }

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

        #endregion
    }

    #endregion

    #region Builder

    //public static Context Create(Clock clock, 
    //                             ContextConfig config,
    //                             IScheduledExecutorService scheduler,
    //                             Task<ContextData> dataFuture, 
    //                             IContextDataProvider dataProvider,
    //                             IContextEventHandler eventHandler, 
    //                             IContextEventLogger eventLogger,
    //                             IVariableParser variableParser, 
    //                             AudienceMatcher audienceMatcher)
    //{
    //    return new Context(clock, config, dataFuture, scheduler, dataProvider, eventHandler, eventLogger,
    //        variableParser, audienceMatcher);
    //}    

    #endregion


    #region Status

    public bool IsReady() {
        return _data != null;
    }

    public bool IsFailed() {
        return _failed;
    }

    public bool IsClosed() {
        return _closed;
    }

    public bool IsClosing() {
        return !_closed && _closing;
    }

    #endregion





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
            var future = _readyTask; // cache here to avoid locking
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
            var experimentNames = new string[_data.Experiments.Length];

            var index = 0;

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

            var previous = _units[unitType];
            if (previous != null && !previous.Equals(uid))
            {
                throw new Exception(string.Format("Unit '%s' already set.", unitType));
            }

            var trimmed = uid.Trim();
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

    public void SetUnits(Dictionary<string, string> units) 
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

    #region Treatment

    public int GetTreatment(string experimentName) 
    {
        CheckReady(true);

        var assignment = GetAssignment(experimentName);
        if (!assignment.Exposed) 
            QueueExposure(assignment);

        return assignment.Variant;
    }    

    public int PeekTreatment(string experimentName) 
    {
        CheckReady(true);

        return GetAssignment(experimentName).Variant;
    }

    #endregion



    private void QueueExposure(Assignment assignment)
    {
        // Todo: review
        //if (assignment.Exposed.CompareAndSet(false, true)) 
        if (assignment.Exposed == false) 
        {
            assignment.Exposed = true;

            var exposure = new Exposure(
                id: assignment.Id,
                name: assignment.Name,
                unit: assignment.UnitType,
                variant: assignment.Variant,
                exposedAt: _clock.Millis(),
                assigned: assignment.Assigned,
                eligible: assignment.Eligible,
                overridden: assignment.Overridden,
                fullOn: assignment.FullOn,
                custom: assignment.Custom,
                audienceMismatch: assignment.AudienceMismatch
            );

            try 
            {
                Monitor.Enter(this);
                PendingCount++;
                _exposures.Add(exposure);
            } 
            finally 
            {
                Monitor.Exit(this);
            }

            LogEvent(EventType.Exposure, exposure);

            SetTimeout();
        }
    }

    #region Variable

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

        var assignment = GetVariableAssignment(key);
        if (assignment != null) 
        {
            if (assignment.Variables != null) 
            {
                if (!assignment.Exposed) 
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

        var assignment = GetVariableAssignment(key);
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

    #endregion



    public void Track(string goalName, Dictionary<string, object> properties) 
    {
        CheckNotClosed();

        var achievement = new GoalAchievement(
            achievedAt: _clock.Millis(),
            name: goalName,
            properties: properties == null ? null : new SortedDictionary<string, object>(properties)
        );

        try 
        {
            Monitor.Enter(achievement);
            PendingCount++;
            _achievements.Add(achievement);
        } 
        finally 
        {
            Monitor.Exit(achievement);
        }

        LogEvent(EventType.Goal, achievement);

        SetTimeout();
    }

    public Task PublishAsync() 
    {
        CheckNotClosed();

        return Flush();
    }

    public void Publish() {
        //PublishAsync().join();
        PublishAsync();
    }

    #region Refresh

    public Task RefreshAsync() 
    {
        CheckNotClosed();

        //if (_refreshing.CompareAndSet(false, true)) 
        //{
        //    _refreshFuture = new Task();

        //    _dataProvider.GetContextData().thenAccept(new Consumer<ContextData>() 
        //    { 
        //        public void accept(ContextData data) 
        //        {
        //            Context.this.setData(data);
        //            refreshing_.set(false);
        //            refreshFuture_.complete(null);

        //            Context.this.logEvent(ContextEventLogger.EventType.Refresh, data);
        //        }
        //    }).exceptionally(new Function<Throwable, Void>() {
        //        @Override
        //        public Void apply(Throwable exception) {
        //        refreshing_.set(false);
        //        refreshFuture_.completeExceptionally(exception);

        //        Context.this.logError(exception);
        //        return null;
        //    }
        //    });
        //}

        var future = _refreshTask;
        if (future != null) {
            return future;
        }

        return Task.CompletedTask;
        //return CompletableFuture.completedFuture(null);
    }

    public void Refresh()
    {
        RefreshAsync();
    }

    #endregion

    #region Close

    public Task CloseAsync() 
    {
        if (!_closed) 
        {
            //if (closing_.compareAndSet(false, true)) 
            //{
            //    clearRefreshTimer();

            //    if (pendingCount_.get() > 0) 
            //    {
            //        closingFuture_ = new CompletableFuture<Void>();

            //        flush().thenAccept(new Consumer<Void>() {
            //            @Override
            //            public void accept(Void x) {
            //            closed_.set(true);
            //            closing_.set(false);
            //            closingFuture_.complete(null);

            //            Context.this.logEvent(ContextEventLogger.EventType.Close, null);
            //        }
            //        }).exceptionally(new Function<Throwable, Void>() {
            //            @Override
            //            public Void apply(Throwable exception) {
            //            closed_.set(true);
            //            closing_.set(false);
            //            closingFuture_.completeExceptionally(exception);
            //            // event logger gets this error during publish

            //            return null;
            //        }
            //        });

            //        return closingFuture_;
            //    } else {
            //        closed_.set(true);
            //        closing_.set(false);

            //        Context.this.logEvent(ContextEventLogger.EventType.Close, null);
            //    }
            //}

            var future = _closingTask;
            if (future != null) 
            {
                return future;
            }
        }

        return Task.CompletedTask;
    }

    public void Close()
    {
        CloseAsync();
    }

    #endregion

    private Task Flush() 
    {
		ClearTimeout();

		if (!_failed)
        {
			if (PendingCount > 0) 
            {
				Exposure[] exposures = null;
				GoalAchievement[] achievements = null;
				int eventCount;

				try 
                {
                    Monitor.Enter(this);
			
					eventCount = PendingCount;

					if (eventCount > 0) 
                    {
						if (_exposures.Count > 0) 
                        {
							exposures = _exposures.ToArray();
							_exposures.Clear();
						}

						if (_achievements.Count > 0) 
                        {
							achievements = _achievements.ToArray();
							_achievements.Clear();
						}

						PendingCount = 0;
					}
				} 
                finally 
                {
					Monitor.Exit(this);
				}

				//if (eventCount > 0) {
				//	PublishEvent publishevent = new PublishEvent();
    //                publishevent.Hashed = true;
    //                publishevent.PublishedAt = _clock.Millis();
    //                publishevent.Units = Algorithm.mapSetToArray(units_.entrySet(), new Unit[0],
				//			new Function<Map.Entry<String, String>, Unit>() {
				//				@Override
				//				public Unit apply(Map.Entry<String, String> entry) {
				//					return new Unit(entry.getKey(),
				//							new String(getUnitHash(entry.getKey(), entry.getValue()),
				//									StandardCharsets.US_ASCII));
				//				}
				//			});
    //                        publishevent.attributes = attributes_.isEmpty() ? null : attributes_.toArray(new Attribute[0]);
    //                        publishevent.exposures = exposures;
    //                        publishevent.goals = achievements;

				//	final CompletableFuture<Void> result = new CompletableFuture<Void>();

				//	eventHandler_.publish(this, event).thenRunAsync(new Runnable() {
				//		@Override
				//		public void run() {
				//			Context.this.logEvent(ContextEventLogger.EventType.Publish, event);
				//			result.complete(null);
				//		}
				//	}).exceptionally(new Function<Throwable, Void>() {
				//		@Override
				//		public Void apply(Throwable throwable) {
				//			Context.this.logError(throwable);

				//			result.completeExceptionally(throwable);
				//			return null;
				//		}
				//	});

				//	return result;
				//}
			}
		} 
        else 
        {
			try 
            {
                Monitor.Enter(this);
			
				_exposures.Clear();
				_achievements.Clear();
                PendingCount = 0;
            } 
            finally 
            {
                Monitor.Exit(this);
            }
		}

		return Task.CompletedTask;
	}

    #region Checks

    private void CheckNotClosed() 
    {
        if (_closed) 
        {
            throw new Exception("ABSmartly Context is closed");
        } 
        if (_closing) 
        {
            throw new Exception("ABSmartly Context is closing");
        }
    }    

    private void CheckReady(bool expectNotClosed) 
    {
        if (!IsReady())
        {
            throw new Exception("ABSmartly Context is not yet ready");
        } 
        if (expectNotClosed) 
        {
            CheckNotClosed();
        }
    }

    #endregion

    private bool ExperimentMatches(Experiment experiment, Assignment assignment) 
    {
        return experiment.Id == assignment.Id &&
               experiment.UnitType.Equals(assignment.UnitType) &&
               experiment.Iteration == assignment.Iteration &&
               experiment.FullOnVariant == assignment.FullOnVariant &&
               Equals(experiment.TrafficSplit, assignment.TrafficSplit);
    }


    #region Assignment

    // Todo: rework...
    private Assignment GetAssignment(string experimentName) 
    {
        var readLock = new ReaderWriterLockSlim();

		try
        {
            readLock.EnterReadLock();

            var assignment = _assignmentCache[experimentName];

			if (assignment != null) 
            {
				var custom = _cassignments[experimentName];
				var overrideppp = _overrides[experimentName];
				var experiment = GetExperiment(experimentName);

				if (overrideppp != null) 
                {
					if (assignment.Overridden && assignment.Variant == overrideppp) 
                    {
						// override up-to-date
						return assignment;
					}
				} 
                else if (experiment == null) 
                {
					if (!assignment.Assigned) 
                    {
						// previously not-running experiment
						return assignment;
					}
				} 
                else if (custom == null || custom == assignment.Variant) 
                {
					if (ExperimentMatches(experiment.Data, assignment)) 
                    {
						// assignment up-to-date
						return assignment;
					}
				}
			}
		} 
        finally 
        {
            readLock.ExitReadLock();
        }

		// cache miss or out-dated
        try 
        {
            _contextLock.EnterWriteLock();

            var custom = _cassignments[experimentName];
			var overrideppp = _overrides[experimentName];
			var experiment = GetExperiment(experimentName);

			var assignment = new Assignment
            {
                Name = experimentName,
                Eligible = true
            };

            if (overrideppp != null) 
            {
				if (experiment != null) 
                {
					assignment.Id = experiment.Data.Id;
					assignment.UnitType = experiment.Data.UnitType;
				}

				assignment.Overridden = true;
				assignment.Variant = overrideppp;
			} 
            else 
            {
				if (experiment != null) 
                {
                    var unitType = experiment.Data.UnitType;

					if (experiment.Data.Audience != null && experiment.Data.Audience.Length > 0) 
                    {
						var attrs = new Dictionary<string, object>(_attributes.Count);
                        foreach (var attribute in _attributes)
                        {
                            attrs.Add(attribute.Name, attribute.Value);
                        }

                        var match = _audienceMatcher.Evaluate(experiment.Data.Audience, attrs);
                        if (match != null) 
                        {
							assignment.AudienceMismatch = !match.Value;
						}
					}

					if (experiment.Data.AudienceStrict && assignment.AudienceMismatch) 
                    {
						assignment.Variant = 0;
					} 
                    else if (experiment.Data.FullOnVariant == 0) 
                    {
						//var uid = _units[experiment.Data.UnitType];
						//if (uid != null) 
      //                  {
						//	var unitHash = GetUnitHash(unitType, uid);

						//	var assigner = GetVariantAssigner(unitType, unitHash);
						//	var eligible = assigner.Assign(experiment.Data.TrafficSplit, experiment.Data.TrafficSeedHi, experiment.Data.TrafficSeedLo) == 1;
						//	if (eligible) 
      //                      {
						//		if (custom != null) 
      //                          {
						//			assignment.Variant = custom;
						//			assignment.Custom = true;
						//		} 
      //                          else 
      //                          {
						//			assignment.Variant = assigner.Assign(experiment.Data.Split,
						//					experiment.Data.SeedHi,
						//					experiment.Data.SeedLo);
						//		}
						//	} 
      //                      else
      //                      {
						//		assignment.Eligible = false;
						//		assignment.Variant = 0;
						//	}

						//	assignment.Assigned = true;
						//}
					} 
                    else 
                    {
						assignment.Assigned = true;
						assignment.Variant = experiment.Data.FullOnVariant;
						assignment.FullOn = true;
					}

					assignment.UnitType = unitType;
					assignment.Id = experiment.Data.Id;
					assignment.Iteration = experiment.Data.Iteration;
					assignment.TrafficSplit = experiment.Data.TrafficSplit;
					assignment.FullOnVariant = experiment.Data.FullOnVariant;
				}
			}

			if (experiment != null && assignment.Variant < experiment.Data.Variants.Length) 
            {
                // Todo: resolve.. ??
				//assignment.Variables = experiment.Variables.Get(assignment.Variant);
            }

			_assignmentCache.Add(experimentName, assignment);

			return assignment;
		} 
        finally 
        {
            _contextLock.ExitWriteLock();
        }
	}

    #endregion


    private Assignment GetVariableAssignment(string key) 
    {
        var experiment = GetVariableExperiment(key);

        if (experiment != null) 
        {
            return GetAssignment(experiment.Data.Name);
        }
        return null;
    }

    private ExperimentVariables GetExperiment(string experimentName) 
    {
        try 
        {
            _dataLock.EnterReadLock();
            return _index[experimentName];
        } 
        finally 
        {
            _dataLock.ExitReadLock();
        }
    }

    private ExperimentVariables GetVariableExperiment(string key) 
    {
        return Concurrency.GetRW(_dataLock, _indexVariables, key);
    }

    private VariantAssigner GetVariantAssigner(string unitType, byte[] unitHash) 
    {
        return Concurrency.ComputeIfAbsentRW(_contextLock, _assigners, unitType, (_ => new VariantAssigner(unitHash)));
    }

    private byte[] GetUnitHash(string unitType, string unitUID)
    {
        return Concurrency.ComputeIfAbsentRW(_contextLock, _hashedUnits, unitType, _ => MD5.HashToByte(unitUID));
    }



    #region Timeout

    private void SetTimeout()
    {
        if (!IsReady()) 
            return;

        if (_timeout != null) 
            return;
        
        try 
        {
            // Todo: Monitor??
            _timeoutLock.EnterWriteLock();
              
            if (_timeout == null) 
            {
                //_scheduler
                //_timeout = _scheduler.Schedule(new Runnable() 
                //{
                //    @Override
                //    public void run() {
                //    Context.this.flush();
                //}
                //}, _publishDelay, TimeUnit.MILLISECONDS);
            }
        } 
        finally 
        {
            _timeoutLock.ExitWriteLock();
        }
    }

    private void ClearTimeout()
    {
        if (_timeout == null) 
            return;
        
        try 
        {
            _timeoutLock.EnterWriteLock();
            if (_timeout != null) 
            {
                // Todo: Task.Cancel..
                //_timeout.Cancel(false);
                _timeout = null;
            }
        } 
        finally
        {
            _timeoutLock.ExitWriteLock();
        }
    }    

    #endregion

    #region RefreshTimer

    private void SetRefreshTimer() 
    {
        if (_refreshInterval > 0 && _refreshTimer == null) 
        {
            //_refreshTimer = _scheduler.ScheduleWithFixedDelay(
            //    new Runnable() {
            //    @Override
            //    public void run() {
            //    Context.this.refreshAsync();
            //}
            //}, refreshInterval_, refreshInterval_, TimeUnit.MILLISECONDS);
        }
    }

    private void ClearRefreshTimer()
    {
        if (_refreshTimer != null) 
        {
            // Todo: Task.Cancel
            //_refreshTimer.Cancel(false);
            _refreshTimer = null;
        }
    }


    #endregion

    #region Data

    private void SetData(ContextData data) 
    {
        var index = new Dictionary<string, ExperimentVariables>();
        var indexVariables = new Dictionary<string, ExperimentVariables>();

        foreach (var experiment in data.Experiments)
        {
            var experimentVariables = new ExperimentVariables
            {
                Data = experiment,
                Variables = new List<Dictionary<string, object>>(experiment.Variants.Length)
            };

            foreach (var variant in experiment.Variants)
            {
                if (variant.Config != null && !string.IsNullOrWhiteSpace(variant.Config)) 
                {
                    var variables = _variableParser.Parse(this, experiment.Name, variant.Name, variant.Config);

                    foreach (var key in variables.Keys)
                    {
                        indexVariables.Add(key, experimentVariables);
                    }

                    experimentVariables.Variables.Add(variables);
                } 
                else 
                {
                    experimentVariables.Variables.Add(new Dictionary<string, object>());
                }                
            }

            index.Add(experiment.Name, experimentVariables);            
        }

        try 
        {
            _dataLock.EnterWriteLock();

            _index = index;
            _indexVariables = indexVariables;
            _data = data;

            SetRefreshTimer();
        } 
        finally 
        {
            _dataLock.ExitWriteLock();
        }
    }

    private void SetDataFailed(Exception exception) 
    {
        try 
        {
            _dataLock.EnterWriteLock();
            _index = new Dictionary<string, ExperimentVariables>();
            _indexVariables = new Dictionary<string, ExperimentVariables>();
            _data = new ContextData(Array.Empty<Experiment>());
            _failed = true;
        } 
        finally 
        {
            _dataLock.ExitWriteLock();
        }
    }

    #endregion

    #region Log

    private void LogEvent(EventType eventType, object data) 
    {
        if (_eventLogger != null) 
        {
            _eventLogger.HandleEvent(this, eventType, data);
        }
    }

    private void LogError(Exception error) 
    {
        if (_eventLogger != null) 
        {
            //while (error instanceof CompletionException) 
            //{
            //    error = error.getCause();
            //}
            _eventLogger.HandleEvent(this, EventType.Error, error.Message);
        }
    }

    #endregion


    #region IDisposable

    public void Dispose()
    {
        _dataLock?.Dispose();
        _contextLock?.Dispose();
        //_eventLock?.Dispose();
        _readyTask?.Dispose();
        _closingTask?.Dispose();
        _refreshTask?.Dispose();
        _timeoutLock?.Dispose();
        _timeout?.Dispose();
        _refreshTimer?.Dispose();
    }

    #endregion
}