using System;
using System.Collections.Concurrent;
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
    private readonly List<GoalAchievement> _achievements = new();
    private readonly DictionaryLockableAdapter<string, VariantAssigner> _assigners;
    private readonly Dictionary<string, Assignment> _assignmentCache = new();

    private readonly ListLockableAdapter<Attribute> _attributes;
    private readonly AudienceMatcher _audienceMatcher;
    private readonly Clock _clock;
    private readonly ReaderWriterLockSlim _contextLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly DictionaryLockableAdapter<string, int?> _customAssignments;

    private readonly ReaderWriterLockSlim _dataLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly IContextDataProvider _dataProvider;
    private readonly IContextPublisher _eventHandler;
    private readonly object _eventLock = new();
    // Serializes Flush() so concurrent flushes (publish-delay timer, explicit
    // PublishAsync, Close/Dispose) cannot overlap. _eventLock cannot be held
    // across the await on PublishAsync, so a separate async-capable gate is used.
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly IContextEventLogger _eventLogger;

    private readonly List<Exposure> _exposures = new();


    private readonly DictionaryLockableAdapter<string, byte[]> _hashedUnits;
    private readonly ILogger<Context> _logger;
    private readonly ConcurrentDictionary<string, int?> _overrides;
    private readonly int _publishDelay;
    private readonly int _refreshInterval;

    private readonly object _refreshTimerLock = new();
    private readonly object _timeoutLock = new();
    private readonly Dictionary<string, string> _units;
    private readonly IVariableParser _variableParser;
    private volatile bool _closed;
    private int _closing;

    private volatile ContextData _data;

    private volatile bool _failed;
    private volatile Exception _failedError;
    private Dictionary<string, ExperimentVariables> _index;
    private Dictionary<string, Dictionary<string, ContextCustomFieldValue>> _contextCustomFields;
    
    private DictionaryLockableAdapter<string, List<ExperimentVariables>> _indexVariables;

    private volatile int _pendingCount;
    private int _refreshing;
    private volatile int _attrsSeq;
    private volatile CancellationTokenSource _refreshTimer;

    private volatile CancellationTokenSource _timeout;

    #region Constructor & Initialization

    public Context(ContextConfig config,
        ContextData data,
        Clock clock,
        IContextDataProvider dataProvider,
        IContextPublisher eventHandler,
        IContextEventLogger eventLogger,
        IVariableParser variableParser,
        AudienceMatcher audienceMatcher,
        ILoggerFactory loggerFactory)
    {
        if (config == null) throw new ArgumentNullException(nameof(config), "Context configuration is required");

        _logger = loggerFactory?.CreateLogger<Context>();
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

        _assigners =
            new DictionaryLockableAdapter<string, VariantAssigner>(new LockableCollectionSlimLock(_contextLock),
                _units.Count);
        _hashedUnits =
            new DictionaryLockableAdapter<string, byte[]>(new LockableCollectionSlimLock(_contextLock), _units.Count);

        _attributes = new ListLockableAdapter<Attribute>(new LockableCollectionSlimLock(_contextLock));
        if (config.Attributes != null)
            SetAttributes(config.Attributes);

        _overrides = config.Overrides != null
            ? new ConcurrentDictionary<string, int?>(config.Overrides.Select(kv => new KeyValuePair<string, int?>(kv.Key, kv.Value)))
            : new ConcurrentDictionary<string, int?>();

        _customAssignments = config.CustomAssignments != null
            ? new DictionaryLockableAdapter<string, int?>(new LockableCollectionSlimLock(_contextLock),
                config.CustomAssignments)
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

    public int PendingCount => _pendingCount;


    public string[] Experiments
    {
        get
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
    }

    [Obsolete("Use the Experiments property instead.")]
    public string[] GetExperiments() => Experiments;

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


    private void QueueExposure(Assignment assignment)
    {
        if (Interlocked.CompareExchange(ref assignment.Exposed, 1, 0) == 0)
        {
            var exposure = new Exposure
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


    private async Task Flush()
    {
        ClearTimeout();

        // Serialize flushes so the publish-delay timer, an explicit PublishAsync,
        // and Close/Dispose cannot run Flush concurrently. Without this, two
        // flushes both snapshot the event lists, then each calls RemoveRange with
        // its (now stale) snapshot count, throwing ArgumentException, or the
        // publish is duplicated/skipped.
        await _flushLock.WaitAsync().ConfigureUnboundContinuation();
        try
        {
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
                            }

                            if (_achievements.Count > 0)
                            {
                                achievements = _achievements.ToArray();
                            }
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

                            try
                            {
                                Monitor.Enter(_eventLock);
                                // Clamp to the current count: items were snapshotted by
                                // value, and the list size is authoritative.
                                var exposureCount = Math.Min(exposures?.Length ?? 0, _exposures.Count);
                                var achievementCount = Math.Min(achievements?.Length ?? 0, _achievements.Count);
                                if (exposureCount > 0)
                                    _exposures.RemoveRange(0, exposureCount);
                                if (achievementCount > 0)
                                    _achievements.RemoveRange(0, achievementCount);
                                Interlocked.Add(ref _pendingCount, -(exposureCount + achievementCount));
                            }
                            finally
                            {
                                Monitor.Exit(_eventLock);
                            }
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
        finally
        {
            _flushLock.Release();
        }
    }

    private byte[] GetUnitHash(string unitType, string unitUid)
    {
        return _hashedUnits.ConcurrentGetOrAdd(unitType, _ => Md5.HashToUtf8Bytes(unitUid));
    }

    private Dictionary<string, object> BuildAttributesDictionary()
    {
        var attrs = new Dictionary<string, object>(_attributes.Count);
        foreach (var attribute in _attributes)
        {
            attrs[attribute.Name] = attribute.Value;
        }
        return attrs;
    }

    private bool? EvaluateAudience(string audience)
    {
        if (string.IsNullOrEmpty(audience))
        {
            return null;
        }

        var attrs = BuildAttributesDictionary();
        return _audienceMatcher.Evaluate(audience, attrs);
    }

    private bool AudienceMatches(Experiment experiment, Assignment assignment)
    {
        if (!string.IsNullOrEmpty(experiment.Audience))
        {
            if (_attrsSeq > assignment.AttrsSeq)
            {
                var match = EvaluateAudience(experiment.Audience);
                var newAudienceMismatch = match != null ? !match.Value : false;
                if (newAudienceMismatch != assignment.AudienceMismatch)
                {
                    return false;
                }
            }
        }
        return true;
    }

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
                    if (ExperimentMatches(experiment.Data, assignment) &&
                        AudienceMatches(experiment.Data, assignment))
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
                        var match = EvaluateAudience(experiment.Data.Audience);
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

            if (experiment != null && assignment.Variant >= 0 && assignment.Variant < experiment.Data.Variants.Length)
                assignment.Variables = experiment.Variables[assignment.Variant];

            assignment.AttrsSeq = _attrsSeq;
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
        var experiments = GetVariableExperiments(key);
        if (experiments == null) return null;
        foreach (var experiment in experiments)
        {
            var assignment = GetAssignment(experiment.Data.Name);
            if (assignment.Assigned || assignment.Overridden)
                return assignment;
        }
        return experiments.Count > 0 ? GetAssignment(experiments[0].Data.Name) : null;
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
    
    public List<string> GetCustomFieldKeys()
    {
        try
        {
            _dataLock.EnterReadLock();

            var keys = new List<string>();
            
            foreach (var experiment in _data.Experiments)
            {
                var customFieldValues = experiment.CustomFieldValues;
                if (customFieldValues != null)
                {
                    foreach (var customFieldValue in customFieldValues)
                    {
                        keys.Add(customFieldValue.Name);
                    }
                }
            }
            
            return keys.OrderBy(q => q).Distinct().ToList();
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }
    
    private ContextCustomFieldValue GetCustomField(string experimentName, string key)
    {
        try
        {
            _dataLock.EnterReadLock();

            if (_contextCustomFields != null &&
                _contextCustomFields.TryGetValue(experimentName, out var customFieldValues) &&
                customFieldValues != null &&
                customFieldValues.TryGetValue(key, out var field))
            {
                return field;
            }

            return null;
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    public object GetCustomFieldValue(string experimentName, string key)
    {
        var field = GetCustomField(experimentName, key);
        return field?.Value;
    }

    public object GetCustomFieldValueType(string experimentName, string key)
    {
        var field = GetCustomField(experimentName, key);
        return field?.Type;
    }

    public object GetCustomFieldType(string experimentName, string key)
    {
        return GetCustomFieldValueType(experimentName, key);
    }

    private List<ExperimentVariables> GetVariableExperiments(string key)
    {
        return _indexVariables.ConcurrentGetValueOrDefault(key);
    }

    private VariantAssigner GetVariantAssigner(string unitType, byte[] unitHash)
    {
        return _assigners.ConcurrentGetOrAdd(unitType, _ => new VariantAssigner(unitHash));
    }


    #region Status

    public bool IsReady()
    {
        return _data != null;
    }

    public bool IsFailed()
    {
        return _failed;
    }

    public Exception ReadyError => _failedError;

    public bool IsClosed()
    {
        return _closed;
    }

    public bool IsClosing()
    {
        return !_closed && _closing > 0;
    }

    public bool IsFinalized => IsClosed();

    public bool IsFinalizing => IsClosing();

    #endregion


    #region Attribute

    public void SetAttribute(string name, object value)
    {
        CheckNotClosed();

        var attribute = new Attribute { Name = name, Value = value, SetAt = _clock.Millis() };
        _attributes.ConcurrentAdd(attribute);
        Interlocked.Increment(ref _attrsSeq);
    }

    public void SetAttributes(Dictionary<string, object> attributes)
    {
        foreach (var kvp in attributes) SetAttribute(kvp.Key, kvp.Value);
    }

    public object GetAttribute(string name)
    {
        object result = null;
        foreach (var attribute in _attributes)
        {
            if (attribute.Name == name)
                result = attribute.Value;
        }
        return result;
    }

    public Dictionary<string, object> Attributes
    {
        get
        {
            var result = new Dictionary<string, object>();
            foreach (var attribute in _attributes)
            {
                result[attribute.Name] = attribute.Value;
            }
            return result;
        }
    }

    [Obsolete("Use the Attributes property instead.")]
    public Dictionary<string, object> GetAttributes() => Attributes;

    #endregion

    #region CustomAssignment

    public void SetCustomAssignment(string experimentName, int variant)
    {
        CheckNotClosed();

        _customAssignments.ConcurrentSet(experimentName, variant);
    }

    public int? GetCustomAssignment(string experimentName)
    {
        return _customAssignments.ConcurrentGetValueOrDefault(experimentName);
    }

    public void SetCustomAssignments(Dictionary<string, int> customAssignments)
    {
        foreach (var kvp in customAssignments) SetCustomAssignment(kvp.Key, kvp.Value);
    }

    #endregion

    #region Override

    public void SetOverride(string experimentName, int variant)
    {
        _overrides[experimentName] = variant;
    }

    public int? GetOverride(string experimentName)
    {
        return _overrides.TryGetValue(experimentName, out var v) ? v : null;
    }

    public void SetOverrides(Dictionary<string, int> overrides)
    {
        foreach (var kvp in overrides) SetOverride(kvp.Key, kvp.Value);
    }

    #endregion

    #region Treatment

    public int GetTreatment(string experimentName)
    {
        return InternalGetTreatmentAsync(experimentName, true) ?? 0;
    }

    public int PeekTreatment(string experimentName)
    {
        return InternalGetTreatmentAsync(experimentName, false) ?? 0;
    }

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
            if (previous != null)
            {
                if (!previous.Equals(uidTrimmed))
                    throw new ArgumentException($"Unit '{unitType}' UID already set.");
                // Same value, no-op
                return;
            }

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

    public Dictionary<string, string> Units
    {
        get
        {
            try
            {
                _contextLock.EnterReadLock();
                return new Dictionary<string, string>(_units);
            }
            finally
            {
                _contextLock.ExitReadLock();
            }
        }
    }

    [Obsolete("Use the Units property instead.")]
    public Dictionary<string, string> GetUnits() => Units;

    #endregion

    #region Variable

    public Dictionary<string, List<string>> VariableKeys
    {
        get
        {
            CheckReady(true);

            var variableKeys = new Dictionary<string, List<string>>(_indexVariables.Count);

            try
            {
                _dataLock.EnterReadLock();

                foreach (var kv in _indexVariables)
                {
                    var names = new List<string>(kv.Value.Count);
                    foreach (var ev in kv.Value) names.Add(ev.Data.Name);
                    variableKeys.Add(kv.Key, names);
                }
            }
            finally
            {
                _dataLock.ExitReadLock();
            }

            return variableKeys;
        }
    }

    [Obsolete("Use the VariableKeys property instead.")]
    public Dictionary<string, List<string>> GetVariableExperimentKeys() => VariableKeys;

    [Obsolete("Use the VariableKeys property instead.")]
    public Dictionary<string, string> GetVariableKeys()
    {
        var full = VariableKeys;
        var result = new Dictionary<string, string>(full.Count);
        foreach (var kv in full)
            if (kv.Value.Count > 0) result[kv.Key] = kv.Value[0];
        return result;
    }

    public object GetVariableValue(string key, object defaultValue)
    {
        return InternalGetVariableValueAsync(key, defaultValue, true);
    }

    public object PeekVariableValue(string key, object defaultValue)
    {
        return InternalGetVariableValueAsync(key, defaultValue, false);
    }

    private object InternalGetVariableValueAsync(string key, object defaultValue, bool doExposure)
    {
        CheckReady(true);

        var assignment = GetVariableAssignment(key);
        if (assignment?.Variables is null) return defaultValue;

        if (doExposure && assignment.Exposed == 0) QueueExposure(assignment);

        return assignment.Variables.TryGetValue(key, out var variable) ? variable : defaultValue;
    }

    #endregion


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

    public void Track(string goalName, Dictionary<string, object> properties)
    {
        CheckNotClosed();

        var achievement = new GoalAchievement
        {
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


    #region Checks

    private void CheckNotClosed()
    {
        if (_closed) throw new InvalidOperationException("ABsmartly Context is finalized.");
        if (_closing > 0) throw new InvalidOperationException("ABsmartly Context is finalizing.");
    }

    private void CheckReady(bool expectNotClosed)
    {
        if (!IsReady())
        {
            throw new InvalidOperationException("ABsmartly Context is not yet ready.");
        }
        if (expectNotClosed)
        {
            CheckNotClosed();
        }
    }

    #endregion


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

            var task = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_publishDelay, token).ConfigureUnboundContinuation();
                    await Flush().ConfigureUnboundContinuation();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Error during flush timeout");
                }
            }, token);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger?.LogError(t.Exception, "Unhandled exception in flush timeout task");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
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
            _timeout.Dispose();
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

            var task = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_refreshInterval, token).ConfigureUnboundContinuation();
                        await RefreshAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError(e, "Error during refresh timer");
                    }
                }
            }, token);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger?.LogError(t.Exception, "Unhandled exception in refresh timer task");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
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
            _refreshTimer.Dispose();
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
        var indexVariables = new Dictionary<string, List<ExperimentVariables>>();
        var contextCustomFields = new Dictionary<string, Dictionary<string, ContextCustomFieldValue>>();

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
                    try
                    {
                        var variables = _variableParser.Parse(this, experiment.Name, variant.Name, variant.Config);

                        if (variables != null)
                        {
                            foreach (var key in variables.Keys)
                            {
                                if (!indexVariables.TryGetValue(key, out var list))
                                {
                                    list = new List<ExperimentVariables>();
                                    indexVariables[key] = list;
                                }
                                if (list.Find(ev => ev.Data.Id == experiment.Id) == null)
                                {
                                    var insertAt = list.FindIndex(ev => ev.Data.Id > experiment.Id);
                                    if (insertAt < 0) list.Add(experimentVariables);
                                    else list.Insert(insertAt, experimentVariables);
                                }
                            }
                            experimentVariables.Variables.Add(variables);
                        }
                        else
                        {
                            _logger?.LogWarning("Variable parser returned null for experiment '{ExperimentName}', variant '{VariantName}'", experiment.Name, variant.Name);
                            experimentVariables.Variables.Add(new Dictionary<string, object>());
                        }
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError(e, "Failed to parse variables for experiment '{ExperimentName}', variant '{VariantName}'", experiment.Name, variant.Name);
                        experimentVariables.Variables.Add(new Dictionary<string, object>());
                    }
                }
                else
                {
                    experimentVariables.Variables.Add(new Dictionary<string, object>());
                }

            index[experiment.Name] = experimentVariables;

            if (experiment.CustomFieldValues == null) continue;
            
            var experimentCustomFields = new Dictionary<string, ContextCustomFieldValue>();
            foreach (var customFieldValue in experiment.CustomFieldValues)
            {
                var value = new ContextCustomFieldValue
                {
                    Type = customFieldValue.Type
                };

                if (customFieldValue.Value != null)
                {
                    try
                    {
                        var customValue = customFieldValue.Value;

                        if (customFieldValue.Type.StartsWith("json"))
                        {
                            value.Value = DefaultVariableParser.ParseValue(customValue);
                        }
                        else if(customFieldValue.Type.StartsWith("boolean"))
                        {
                            value.Value = Convert.ToBoolean(customValue);
                        }
                        else if(customFieldValue.Type.StartsWith("number"))
                        {
                            value.Value = long.TryParse(customValue, out var longVal)
                                ? (object)longVal
                                : Convert.ToDouble(customValue);
                        }
                        else
                        {
                            value.Value = customValue;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger?.LogWarning(e, "Failed to convert custom field '{FieldName}' of type '{FieldType}' for experiment '{ExperimentName}'. Value: '{Value}'",
                            customFieldValue.Name, customFieldValue.Type, experiment.Name, customFieldValue.Value);
                        value.Value = null;
                    }
                }

                experimentCustomFields[customFieldValue.Name] = value;
            }

            contextCustomFields[experiment.Name] = experimentCustomFields;
        }

        try
        {
            _dataLock.EnterWriteLock();

            _index = index;
            _contextCustomFields = contextCustomFields;
            _indexVariables =
                new DictionaryLockableAdapter<string, List<ExperimentVariables>>(new LockableCollectionSlimLock(_dataLock),
                    indexVariables);
            _data = data;

            SetRefreshTimer();
        }
        finally
        {
            _dataLock.ExitWriteLock();
        }
    }

    private void SetDataFailed(Exception error = null)
    {
        try
        {
            _dataLock.EnterWriteLock();
            _index = new Dictionary<string, ExperimentVariables>();
            _indexVariables =
                new DictionaryLockableAdapter<string, List<ExperimentVariables>>(new LockableCollectionSlimLock(_dataLock));
            _data = new ContextData();
            _failed = true;
            _failedError = error;
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

    public void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
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
    
    public class ContextCustomFieldValue
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public object Value { get; set; }
    }

    public class Assignment
    {
        public int Exposed;

        public Dictionary<string, object> Variables = new();
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
        public int AttrsSeq { get; set; }
    }

    #endregion
}