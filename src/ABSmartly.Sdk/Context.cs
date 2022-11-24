using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ABSmartly.Concurrency;
using ABSmartly.EqualityComparison;
using ABSmartly.Extensions;
using ABSmartly.Internal;
using ABSmartly.Internal.Hashing;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly;

public class Context : IContext, IDisposable, IAsyncDisposable
{
    private readonly Clock _clock;
    private readonly int _publishDelay;
    private readonly int _refreshInterval;
    private readonly IContextEventHandler _eventHandler;
    private readonly IContextEventLogger _eventLogger;
    private readonly IContextDataProvider _dataProvider;
    private readonly IVariableParser _variableParser;
    private readonly AudienceMatcher _audienceMatcher;
    private readonly Dictionary<string, string> _units;

    private readonly ReaderWriterLockSlim _dataLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly ReaderWriterLockSlim _contextLock = new(LockRecursionPolicy.SupportsRecursion);

    private ContextData _data;
    private Dictionary<string, ExperimentVariables> _index;
    private DictionaryLockableAdapter<string, ExperimentVariables> _indexVariables;


    private readonly DictionaryLockableAdapter<string, byte[]> _hashedUnits;
    private readonly DictionaryLockableAdapter<string, VariantAssigner> _assigners;
    private readonly Dictionary<string, Assignment> _assignmentCache = new();

    private readonly List<Exposure> _exposures = new();
    private readonly List<GoalAchievement> _achievements = new();

    private readonly ListLockableAdapter<Attribute> _attributes;
    private readonly DictionaryLockableAdapter<string, int?> _overrides;
    private readonly DictionaryLockableAdapter<string, int?> _customAssignments;

    private volatile int _pendingCount;

    public int PendingCount => _pendingCount;

    private bool _failed;
    private bool _closed;
    private int _closing;
    private int _refreshing;

    private readonly object _refreshTimerLock = new();
    private readonly object _timeoutLock = new();
    private readonly object _eventLock = new();

    private volatile CancellationTokenSource _timeout;
    private volatile CancellationTokenSource _refreshTimer;
    private readonly ILogger<Context> _logger;

    #region Constructor & Initialization

    public Context(ContextConfig config,
        ContextData data,
        Clock clock,
        IContextDataProvider dataProvider,
        IContextEventHandler eventHandler,
        IContextEventLogger eventLogger,
        IVariableParser variableParser,
        AudienceMatcher audienceMatcher, 
        ILoggerFactory loggerFactory)
    {
        if (config == null) throw new ArgumentNullException(nameof(config), "Context configuration is required");
        
        _logger = loggerFactory.CreateLogger<Context>();
        _clock = clock;
        _publishDelay = Convert.ToInt32(config.PublishDelay.TotalMilliseconds);
        _refreshInterval = Convert.ToInt32(config.RefreshInterval.TotalMilliseconds);
        _eventHandler = eventHandler;
        _eventLogger = config.ContextEventLogger ?? eventLogger;
        _dataProvider = dataProvider;
        _variableParser = variableParser;
        _audienceMatcher = audienceMatcher;

        _units = new Dictionary<string, string>();

        if (config.Units != null)
            SetUnits(config.Units);

        _assigners = new DictionaryLockableAdapter<string, VariantAssigner>(new LockableCollectionSlimLock(_contextLock), _units.Count);
        _hashedUnits = new DictionaryLockableAdapter<string, byte[]>(new LockableCollectionSlimLock(_contextLock), _units.Count);

        _attributes = new ListLockableAdapter<Attribute>(new LockableCollectionSlimLock(_contextLock));
        if (config.Attributes != null)
            SetAttributes(config.Attributes);

        _overrides = config.Overrides != null
            ? new DictionaryLockableAdapter<string, int?>(new LockableCollectionSlimLock(_contextLock), config.Overrides)
            : new DictionaryLockableAdapter<string, int?>(new LockableCollectionSlimLock(_contextLock));

        _customAssignments = config.CustomAssignments != null
            ? new DictionaryLockableAdapter<string, int?>(new LockableCollectionSlimLock(_contextLock), config.CustomAssignments)
            : new DictionaryLockableAdapter<string, int?>(new LockableCollectionSlimLock(_contextLock));

        if (data != null)
        {
            SetData(data);
            LogEvent(EventType.Ready, data);

            if (_pendingCount > 0) SetTimeout();
        }
        else
        {
            const string errorMessage = "Context initialized with failed data.";
            SetDataFailed();
            _logger.LogWarning(errorMessage);
            LogEvent(EventType.Error, errorMessage);
        }
    }

    #endregion


    #region Status

    public bool IsReady()
    {
        return _data != null;
    }

    public bool IsFailed()
    {
        return _failed;
    }

    public bool IsClosed()
    {
        return _closed;
    }

    public bool IsClosing()
    {
        return !_closed && _closing > 0;
    }

    #endregion


    public string[] GetExperiments()
    {
        CheckReady(true);

        try
        {
            _dataLock.EnterReadLock();
            return _data.Experiments.Select(x => x.Name).ToArray();
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    public ContextData GetContextData()
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


    #region Attribute

    public void SetAttribute(string name, object value)
    {
        CheckNotClosed();

        var attribute = new Attribute { Name = name, Value = value, SetAt = _clock.Millis() };
        _attributes.ConcurrentAdd(attribute);
    }

    public void SetAttributes(Dictionary<string, object> attributes)
    {
        foreach (var kvp in attributes) SetAttribute(kvp.Key, kvp.Value);
    }

    #endregion

    #region CustomAssignment

    public void SetCustomAssignment(string experimentName, int variant)
    {
        CheckNotClosed();

        _customAssignments.ConcurrentSet(experimentName, variant);
    }

    public int? GetCustomAssignment(string experimentName) => 
        _customAssignments.ConcurrentGetValueOrDefault(experimentName);

    public void SetCustomAssignments(Dictionary<string, int> customAssignments)
    {
        foreach (var kvp in customAssignments) SetCustomAssignment(kvp.Key, kvp.Value);
    }

    #endregion

    #region Override

    public void SetOverride(string experimentName, int variant)
    {
        CheckNotClosed();
        _overrides.ConcurrentSet(experimentName, variant);
    }

    public int? GetOverride(string experimentName) => 
        _overrides.ConcurrentGetValueOrDefault(experimentName);

    public void SetOverrides(Dictionary<string, int> overrides)
    {
        foreach (var kvp in overrides) SetOverride(kvp.Key, kvp.Value);
    }

    #endregion

    #region Treatment

    public int GetTreatment(string experimentName) => InternalGetTreatmentAsync(experimentName, true) ?? 0;

    public int PeekTreatment(string experimentName) => InternalGetTreatmentAsync(experimentName, false) ?? 0;

    private int? InternalGetTreatmentAsync(string experimentName, bool doExposure)
    {
        CheckReady(true);

        var assignment = GetAssignment(experimentName);
        if (doExposure && assignment.Exposed == 0)
            QueueExposure(assignment);

        return assignment.Variant;
    }

    #endregion

    #region Unit

    public void SetUnit(string unitType, string uid)
    {
        CheckNotClosed();

        var uidTrimmed = uid.Trim();
        if (string.IsNullOrEmpty(uidTrimmed))
            throw new ArgumentException($"Unit '{unitType}' UID must not be blank.");
        
        try
        {
            _contextLock.EnterWriteLock();

            var previous = _units.TryGetValue(unitType, out var u) ? u : null;
            if (previous != null && !previous.Equals(uidTrimmed))
                throw new ArgumentException($"Unit '{unitType}' already set.");

            _units.Add(unitType, uidTrimmed);
        }
        finally
        {
            _contextLock.ExitWriteLock();
        }
    }

    public void SetUnits(Dictionary<string, string> units)
    {
        foreach (var kvp in units) SetUnit(kvp.Key, kvp.Value);
    }

    #endregion

    #region Variable

    public Dictionary<string, string> GetVariableKeys()
    {
        CheckReady(true);

        var variableKeys = new Dictionary<string, string>(_indexVariables.Count);

        try
        {
            _dataLock.EnterReadLock();

            foreach (var kv in _indexVariables) variableKeys.Add(kv.Key, kv.Value.Data.Name);
        }
        finally
        {
            _dataLock.ExitReadLock();
        }

        return variableKeys;
    }

    public object GetVariableValue(string key, object defaultValue) => InternalGetVariableValueAsync(key, defaultValue, true);

    public object PeekVariableValue(string key, object defaultValue) => InternalGetVariableValueAsync(key, defaultValue, false);

    private object InternalGetVariableValueAsync(string key, object defaultValue, bool doExposure)
    {
        CheckReady(true);

        var assignment = GetVariableAssignment(key);
        if (assignment?.Variables is null) return defaultValue;

        if (doExposure && assignment.Exposed == 0) QueueExposure(assignment);

        return assignment.Variables.TryGetValue(key, out var variable) ? variable : defaultValue;
    }

    #endregion


    private void QueueExposure(Assignment assignment)
    {
        if (Interlocked.CompareExchange(ref assignment.Exposed, 1, 0) == 0)
        {
            var exposure = new Exposure{
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
                Monitor.Enter(_eventLock);
                Interlocked.Increment(ref _pendingCount);
                _exposures.Add(exposure);
            }
            finally
            {
                Monitor.Exit(_eventLock);
            }

            LogEvent(EventType.Exposure, exposure);

            SetTimeout();
        }
    }


    #region Public Control Functions - Publish / Refresh / Track

    public void Publish()
    {
        AsyncHelpers.RunSync(async () => await PublishAsync());
    }

    public async Task PublishAsync()
    {
        CheckNotClosed();

        await Flush().ConfigureUnboundContinuation();
    }

    public void Refresh()
    {
        AsyncHelpers.RunSync(async () => await RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        CheckNotClosed();

        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
        {
            try
            {
                var data = await _dataProvider.GetContextDataAsync().ConfigureUnboundContinuation();
                SetData(data);
                LogEvent(EventType.Refresh, data);
            }
            catch (Exception e)
            {
                LogError(e);
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        }
    }

    public void Track(string goalName, Dictionary<string, object> properties)
    {
        CheckNotClosed();

        var achievement = new GoalAchievement{
            AchievedAt = _clock.Millis(),
            Name = goalName,
            Properties = properties == null ? null : new SortedDictionary<string, object>(properties)
        };

        try
        {
            Monitor.Enter(_eventLock);
            Interlocked.Increment(ref _pendingCount);
            _achievements.Add(achievement);
        }
        finally
        {
            Monitor.Exit(_eventLock);
        }

        LogEvent(EventType.Goal, achievement);

        SetTimeout();
    }

    #endregion


    private async Task Flush()
    {
        ClearTimeout();

        if (!_failed)
        {
            if (_pendingCount > 0)
            {
                Exposure[] exposures = null;
                GoalAchievement[] achievements = null;
                int eventCount;

                try
                {
                    Monitor.Enter(_eventLock);

                    eventCount = _pendingCount;
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

                        _pendingCount = 0;
                    }
                }
                finally
                {
                    Monitor.Exit(_eventLock);
                }

                if (eventCount > 0)
                    try
                    {
                        var publishEvent = new PublishEvent
                        {
                            Hashed = true,
                            PublishedAt = _clock.Millis(),
                            Units = _units
                                .Select(kv => new Unit
                                {
                                    Type = kv.Key, 
                                    Uid = Encoding.ASCII.GetString(GetUnitHash(kv.Key, kv.Value))
                                })
                                .ToArray(),
                            Attributes = _attributes.Count == 0 ? null : _attributes.ToArray(),
                            Exposures = exposures,
                            Goals = achievements
                        };

                        await _eventHandler.PublishAsync(this, publishEvent).ConfigureUnboundContinuation();
                        LogEvent(EventType.Publish, publishEvent);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "{Publish}", EventType.Publish);
                        LogError(e);
                        throw;
                    }
            }
        }
        else
        {
            try
            {
                Monitor.Enter(_eventLock);

                _exposures.Clear();
                _achievements.Clear();
                Interlocked.Exchange(ref _pendingCount, 0);
            }
            finally
            {
                Monitor.Exit(_eventLock);
            }
        }
    }

    private byte[] GetUnitHash(string unitType, string unitUid)
    {
        return _hashedUnits.ConcurrentGetOrAdd(unitType, _ => Md5.HashToUtf8Bytes(unitUid));
    }


    #region Checks

    private void CheckNotClosed()
    {
        if (_closed) throw new InvalidOperationException("ABSmartly Context is closed");
        if (_closing > 0) throw new InvalidOperationException("ABSmartly Context is closing");
    }

    private void CheckReady(bool expectNotClosed)
    {
        if (!IsReady()) throw new Exception("ABSmartly Context is not yet ready");
        if (expectNotClosed) CheckNotClosed();
    }

    #endregion


    private Assignment GetAssignment(string experimentName)
    {
        try
        {
            _contextLock.EnterReadLock();

            var assignment = _assignmentCache.TryGetValue(experimentName, out var e) ? e : null;
            if (assignment != null)
            {
                var experiment = GetExperiment(experimentName);

                if (_overrides.TryGetValue(experimentName, out var @override))
                {
                    if (assignment.Overridden && assignment.Variant == @override)
                        // override is up-to-date
                        return assignment;
                }
                else if (experiment == null)
                {
                    if (!assignment.Assigned)
                        // previously not-running experiment
                        return assignment;
                }
                else if (!_customAssignments.TryGetValue(experimentName, out var custom) ||
                         custom == assignment.Variant)
                {
                    if (ExperimentMatches(experiment.Data, assignment))
                        // assignment is up-to-date
                        return assignment;
                }
            }
        }
        finally
        {
            _contextLock.ExitReadLock();
        }

        // cache miss or out-dated
        try
        {
            _contextLock.EnterWriteLock();

            var experiment = GetExperiment(experimentName);

            var assignment = new Assignment
            {
                Name = experimentName,
                Eligible = true
            };

            if (_overrides.TryGetValue(experimentName, out var @override) && @override != null)
            {
                if (experiment != null)
                {
                    assignment.Id = experiment.Data.Id;
                    assignment.UnitType = experiment.Data.UnitType;
                }

                assignment.Overridden = true;
                assignment.Variant = (int)@override;
            }
            else
            {
                if (experiment != null)
                {
                    var unitType = experiment.Data.UnitType;

                    if (!string.IsNullOrEmpty(experiment.Data.Audience))
                    {
                        var attrs = new Dictionary<string, object>(_attributes.Count);
                        foreach (var attribute in _attributes) attrs.Add(attribute.Name, attribute.Value);

                        var match = _audienceMatcher.Evaluate(experiment.Data.Audience, attrs);
                        if (match != null) assignment.AudienceMismatch = !match.Value;
                    }

                    if (experiment.Data.AudienceStrict && assignment.AudienceMismatch)
                    {
                        assignment.Variant = 0;
                    }
                    else if (experiment.Data.FullOnVariant == 0)
                    {
                        var uid = _units.TryGetValue(experiment.Data.UnitType, out var u) ? u : null;
                        if (uid != null)
                        {
                            var unitHash = GetUnitHash(unitType, uid);
                            var assigner = GetVariantAssigner(unitType, unitHash);
                            var eligible = assigner.Assign(experiment.Data.TrafficSplit, experiment.Data.TrafficSeedHi,
                                experiment.Data.TrafficSeedLo) == 1;
                            if (eligible)
                            {
                                if (_customAssignments.TryGetValue(experimentName, out var custom) && custom != null)
                                {
                                    assignment.Variant = (int)custom;
                                    assignment.Custom = true;
                                }
                                else
                                {
                                    assignment.Variant = assigner.Assign(experiment.Data.Split,
                                        experiment.Data.SeedHi,
                                        experiment.Data.SeedLo);
                                }
                            }
                            else
                            {
                                assignment.Eligible = false;
                                assignment.Variant = 0;
                            }

                            assignment.Assigned = true;
                        }
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
                assignment.Variables = experiment.Variables[assignment.Variant];

            _assignmentCache[experimentName] = assignment;

            return assignment;
        }
        finally
        {
            _contextLock.ExitWriteLock();
        }

        static bool ExperimentMatches(Experiment experiment, Assignment assignment)
        {
            return experiment.Id == assignment.Id &&
                   string.Equals(experiment.UnitType, assignment.UnitType) &&
                   experiment.Iteration == assignment.Iteration &&
                   experiment.FullOnVariant == assignment.FullOnVariant &&
                   ArrayEquality.Equals(experiment.TrafficSplit, assignment.TrafficSplit);
        }
    }

    private Assignment GetVariableAssignment(string key)
    {
        var experiment = GetVariableExperiment(key);
        return experiment != null ? GetAssignment(experiment.Data.Name) : null;
    }

    private ExperimentVariables GetExperiment(string experimentName)
    {
        try
        {
            _dataLock.EnterReadLock();
            return _index.TryGetValue(experimentName, out var variables) ? variables : null;
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    private ExperimentVariables GetVariableExperiment(string key) => 
        _indexVariables.ConcurrentGetValueOrDefault(key);

    private VariantAssigner GetVariantAssigner(string unitType, byte[] unitHash) => 
        _assigners.ConcurrentGetOrAdd(unitType, _ => new VariantAssigner(unitHash));


    #region Timeout

    private void SetTimeout()
    {
        if (!IsReady()) return;
        if (_timeout != null) return;

        try
        {
            Monitor.Enter(_timeoutLock);

            if (_timeout != null) return;

            _timeout = new CancellationTokenSource();
            var token = _timeout.Token;

            Task.Run(async () =>
            {
                await Task.Delay(_publishDelay, token).ConfigureUnboundContinuation();
                await Flush().ConfigureUnboundContinuation();
            }, token);
        }
        finally
        {
            Monitor.Exit(_timeoutLock);
        }
    }

    private void ClearTimeout()
    {
        if (_timeout == null)
            return;

        try
        {
            Monitor.Enter(_timeoutLock);
            if (_timeout == null) return;

            _timeout.Cancel();
            _timeout = null;
        }
        finally
        {
            Monitor.Exit(_timeoutLock);
        }
    }

    #endregion

    #region RefreshTimer

    private void SetRefreshTimer()
    {
        if (_refreshInterval <= 0 || _refreshTimer != null) return;

        try
        {
            Monitor.Enter(_refreshTimerLock);

            if (_refreshTimer != null) return;

            _refreshTimer = new CancellationTokenSource();
            var token = _refreshTimer.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(_publishDelay, token).ConfigureUnboundContinuation();
                    await RefreshAsync();
                }
            }, token);
        }
        finally
        {
            Monitor.Exit(_refreshTimerLock);
        }
    }

    private void ClearRefreshTimer()
    {
        if (_refreshTimer == null)
            return;

        try
        {
            Monitor.Enter(_refreshTimerLock);
            if (_refreshTimer == null) return;

            _refreshTimer.Cancel();
            _refreshTimer = null;
        }
        finally
        {
            Monitor.Exit(_refreshTimerLock);
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
                if (variant.Config != null && !string.IsNullOrWhiteSpace(variant.Config))
                {
                    var variables = _variableParser.Parse(this, experiment.Name, variant.Name, variant.Config);

                    foreach (var key in variables.Keys) indexVariables[key] = experimentVariables;

                    experimentVariables.Variables.Add(variables);
                }
                else
                {
                    experimentVariables.Variables.Add(new Dictionary<string, object>());
                }

            index[experiment.Name] = experimentVariables;
        }

        try
        {
            _dataLock.EnterWriteLock();

            _index = index;
            _indexVariables = new DictionaryLockableAdapter<string, ExperimentVariables>(new LockableCollectionSlimLock(_dataLock), indexVariables);
            _data = data;

            SetRefreshTimer();
        }
        finally
        {
            _dataLock.ExitWriteLock();
        }
    }

    private void SetDataFailed()
    {
        try
        {
            _dataLock.EnterWriteLock();
            _index = new Dictionary<string, ExperimentVariables>();
            _indexVariables = new DictionaryLockableAdapter<string, ExperimentVariables>(new LockableCollectionSlimLock(_dataLock));
            _data = new ContextData();
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
        _eventLogger?.HandleEvent(this, eventType, data);
    }

    private void LogError(Exception error)
    {
        _eventLogger?.HandleEvent(this, EventType.Error, error.Message);
    }

    #endregion


    #region Close & Dispose

    private async Task CloseAsync()
    {
        if (_closed) return;

        if (Interlocked.CompareExchange(ref _closing, 1, 0) == 0)
        {
            ClearRefreshTimer();

            try
            {
                if (_pendingCount > 0) await Flush().ConfigureUnboundContinuation();

                LogEvent(EventType.Close, null);
            }
            finally
            {
                _closed = true;
                Interlocked.Exchange(ref _closing, 0);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing)
    {
        if (disposing)
        {
            AsyncHelpers.RunSync(async () => await CloseAsync());

            _dataLock?.Dispose();
            _contextLock?.Dispose();
            _timeout?.Dispose();
            _refreshTimer?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureUnboundContinuation();

        _dataLock?.Dispose();
        _contextLock?.Dispose();
        _timeout?.Dispose();
        _refreshTimer?.Dispose();

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    #endregion


    #region Helper classes

    public class ExperimentVariables
    {
        public Experiment Data { get; set; }
        public List<Dictionary<string, object>> Variables { get; set; }
    }

    public class Assignment
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

        public Dictionary<string, object> Variables = new();

        public int Exposed;
    }

    #endregion
}