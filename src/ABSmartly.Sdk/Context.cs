using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
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

public class Context : IContextV2, IDisposable, IAsyncDisposable
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

    // application/environment from the service config, seen by audiences and rules but never
    // published (the collector adds both). Empty for contexts built with the public constructors.
    private static readonly IReadOnlyDictionary<string, object> EmptySystemAttributes =
        new Dictionary<string, object>(0);
    private IReadOnlyDictionary<string, object> _systemAttributes = EmptySystemAttributes;

    internal IReadOnlyDictionary<string, object> SystemAttributes
    {
        set => _systemAttributes = value ?? EmptySystemAttributes;
    }

    // Test seam: runs once between an assignment lookup and its exposure, to force a race there.
    internal Action AfterAssignmentLookup;

    private volatile bool _closed;
    private int _closing;
    private readonly object _closeLock = new();
    private TaskCompletionSource<bool> _closeAttempt;
    // Set in the async flow of a close attempt's final flush only, so a publisher
    // that calls Close/FinalizeAsync from that flush can tell it is nested.
    private readonly AsyncLocal<bool> _inCloseFlush = new();
    // The context whose close attempt is notifying its event logger on this thread,
    // so a synchronous Close() from that callback is a no-op instead of a recursive
    // retry. Thread-scoped (unlike AsyncLocal, it does not flow to a thread the logger
    // dispatches to), so a Close() handed to another thread runs normally.
    [ThreadStatic] private static Context t_notifyingClose;
    // Publications handed to the publisher and not yet settled (guarded by _eventLock),
    // so a close can wait for them instead of sealing the context while one may still
    // fail and restore its events.
    private readonly List<Task> _inFlightPublishes = new();
    // The publications whose publisher callback the current async flow is running in,
    // so a close called back from a publisher does not wait for its own publication.
    private readonly AsyncLocal<PublishScope> _publishScope = new();
    // Guarded by _closeLock: an initial load being applied defers disposal of the
    // locks it uses until it has settled.
    private bool _settlingLoad;
    private bool _locksDisposePending;
    // Guarded by _closeLock: a Dispose that joined a close in progress without waiting
    // for it asks the close's owner to dispose the locks once the context is closed.
    private bool _disposeAfterClose;
    private bool _locksDisposed;

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

    // Completes (never faults) once the initial data load of a pending context has
    // settled as ready or failed. Null when the context was constructed with data.
    private TaskCompletionSource<bool> _readyTcs;
    private TaskCompletionSource<bool> _loadSettledTcs;

    #region Constructor & Initialization

    /// <summary>
    /// 1.3.0 constructor, kept for source and binary compatibility.
    /// </summary>
    [Obsolete("Use the constructor taking an IContextPublisher instead.")]
    public Context(ContextConfig config,
        ContextData data,
        Clock clock,
        IContextDataProvider dataProvider,
        IContextEventHandler eventHandler,
        IContextEventLogger eventLogger,
        IVariableParser variableParser,
        AudienceMatcher audienceMatcher,
        ILoggerFactory loggerFactory)
        : this(config, data, clock, dataProvider, LegacyContextEventHandlerAdapter.Wrap(eventHandler), eventLogger,
            variableParser, audienceMatcher, loggerFactory)
    {
    }

    public Context(ContextConfig config,
        ContextData data,
        Clock clock,
        IContextDataProvider dataProvider,
        IContextPublisher eventHandler,
        IContextEventLogger eventLogger,
        IVariableParser variableParser,
        AudienceMatcher audienceMatcher,
        ILoggerFactory loggerFactory)
        : this(config, clock, dataProvider, eventHandler, eventLogger, variableParser, audienceMatcher,
            loggerFactory)
    {
        SetInitialData(data, null);
    }

    // Installs the system attributes before the data, so a Ready event logger that reads a
    // treatment already sees them (an object initializer on a public constructor runs too late).
    internal static Context CreateWithData(ContextConfig config,
        ContextData data,
        Clock clock,
        IContextDataProvider dataProvider,
        IContextPublisher eventHandler,
        IContextEventLogger eventLogger,
        IVariableParser variableParser,
        AudienceMatcher audienceMatcher,
        ILoggerFactory loggerFactory,
        IReadOnlyDictionary<string, object> systemAttributes = null)
    {
        var context = new Context(config, clock, dataProvider, eventHandler, eventLogger, variableParser,
            audienceMatcher, loggerFactory)
        {
            SystemAttributes = systemAttributes
        };
        context.SetInitialData(data, null);
        return context;
    }

    // Creates a context that is not ready until dataTask settles: the load runs in
    // the background, so calls made in the meantime see the not-ready state.
    internal static Context CreatePending(ContextConfig config,
        Task<ContextData> dataTask,
        Clock clock,
        IContextDataProvider dataProvider,
        IContextPublisher eventHandler,
        IContextEventLogger eventLogger,
        IVariableParser variableParser,
        AudienceMatcher audienceMatcher,
        ILoggerFactory loggerFactory,
        IReadOnlyDictionary<string, object> systemAttributes = null)
    {
        if (dataTask == null) throw new ArgumentNullException(nameof(dataTask));

        var context = new Context(config, clock, dataProvider, eventHandler, eventLogger, variableParser,
            audienceMatcher, loggerFactory)
        {
            SystemAttributes = systemAttributes
        };
        context._readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context._loadSettledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = context.LoadAsync(dataTask);
        return context;
    }

    private Context(ContextConfig config,
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
    }

    // Settles the initial load: ready with data, or failed. Readiness completes before
    // the Ready/Error event, so a callback may wait for it. A failed load keeps the
    // error that caused it as ReadyError; null data with no error is the legacy
    // "initialized with failed data" path.
    private void SetInitialData(ContextData data, Exception error)
    {
        if (error == null && data != null)
        {
            SetData(data);
            _readyTcs?.TrySetResult(true);
            try
            {
                LogEvent(EventType.Ready, data);
            }
            finally
            {
                if (_pendingCount > 0 && !IsDisposing()) SetTimeout();
            }
        }
        else if (error != null)
        {
            SetDataFailed(error);
            _readyTcs?.TrySetResult(true);
            _logger?.LogWarning(error, "Context data failed to load: {Message}", error.Message);
            LogError(error);
        }
        else
        {
            const string errorMessage = "Context initialized with failed data.";
            SetDataFailed();
            _readyTcs?.TrySetResult(true);
            _logger?.LogWarning(errorMessage);
            LogEvent(EventType.Error, errorMessage);
        }
    }

    private async Task LoadAsync(Task<ContextData> dataTask)
    {
        try
        {
            Exception loadError = null;
            ContextData data = null;
            try
            {
                data = await dataTask.ConfigureUnboundContinuation();
            }
            catch (Exception e)
            {
                loadError = e;
            }

            // Closed before the load settled: the context is sealed, so the late data
            // is dropped. During a close still in progress the data is applied, since
            // the close may fail and leave the context open.
            lock (_closeLock)
            {
                if (_closed) return;
                _settlingLoad = true;
            }

            try
            {
                SetInitialData(data, loadError);
            }
            catch (Exception e) when (!IsReady())
            {
                // A malformed payload must settle readiness as failed rather than hang.
                SetInitialData(null, e);
            }
            finally
            {
                bool disposeLocks;
                lock (_closeLock)
                {
                    _settlingLoad = false;
                    disposeLocks = _locksDisposePending;
                }

                if (disposeLocks) DisposeLocks();
            }
        }
        catch (Exception e)
        {
            // The load has already settled; only a Ready/Error event logger callback
            // gets here. Readiness is unaffected, but CreateContext/CreateContextAsync
            // rethrow it, as 1.3.0 did when the logger threw during creation.
            _logger?.LogError(e, "Error while settling the context data load");
            _readyTcs.TrySetResult(true);
            _loadSettledTcs.TrySetException(e);
            // CreatePendingContext callers never observe the settle signal; the error
            // must not surface as an UnobservedTaskException.
            _ = _loadSettledTcs.Task.Exception;
        }
        finally
        {
            _readyTcs.TrySetResult(true);
            _loadSettledTcs.TrySetResult(true);
        }
    }

    // The SDK's own default provider (exactly that type) fetches through a path that throws
    // the real cause of a failure. Any other provider, including a subclass of the default
    // one, which may reimplement GetContextDataAsync (for example to serve cached data),
    // is called through its public IContextDataProvider.GetContextDataAsync, as in 1.3.0.
    internal static Task<ContextData> FetchContextData(IContextDataProvider provider) =>
        provider != null && provider.GetType() == typeof(Services.DefaultContextDataProvider)
            ? ((Services.IThrowingContextDataProvider)provider).FetchContextDataAsync()
            : provider.GetContextDataAsync();

    // Waits until the load has settled and its Ready/Error event was logged, so a
    // caller that returns afterwards has observed that event (as the JavaScript SDK
    // logs "ready" before resolving ready()). Throws the exception of an event logger
    // that threw on that Ready/Error event, as context creation did in 1.3.0.
    internal async Task WaitUntilLoadSettledAsync()
    {
        var settled = _loadSettledTcs;
        if (settled != null) await settled.Task.ConfigureUnboundContinuation();
    }

    private bool IsDisposing() => _closed || _closing > 0;

    #endregion

    public int PendingCount => _pendingCount;


    public string[] Experiments
    {
        get
        {
            CheckReady(false);

            if (IsClosed()) return _data.Experiments.Select(x => x.Name).ToArray();

            _dataLock.EnterReadLock();
            try
            {
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
                AudienceMismatch = assignment.AudienceMismatch,
                RuleOverride = assignment.RuleOverride
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
        var outcome = await FlushCore().ConfigureUnboundContinuation();
        ReportFlush(outcome);
    }

    private readonly struct FlushOutcome
    {
        public FlushOutcome(PublishEvent published, Exception error)
        {
            Published = published;
            Error = error;
        }

        public PublishEvent Published { get; }
        public Exception Error { get; }
    }

    // Publishes the pending events without notifying the event logger, so a caller
    // (CloseAsync) can settle its own state before observers run.
    //
    // As the Java SDK and 1.3.0, the batch is detached from the queue (and PendingCount
    // reset to 0) under _eventLock before the publisher is called, and no lock is held
    // across the publisher callback: a publisher that calls PublishAsync/FinalizeAsync
    // on this context re-enters without deadlocking, and concurrent flushes each
    // publish a disjoint batch. On failure the batch is put back at the front of the
    // queue and PendingCount restored, so the events stay retryable.
    private async Task<FlushOutcome> FlushCore()
    {
        ClearTimeout();

        if (_failed)
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

            return default;
        }

        if (_pendingCount <= 0) return default;

        Exposure[] exposures = null;
        GoalAchievement[] achievements = null;
        TaskCompletionSource<bool> inFlight = null;
        int eventCount;

        try
        {
            Monitor.Enter(_eventLock);

            // While a close is in progress only its own flush takes the batch: a flush that
            // detached it after the close looked for outstanding publications could fail
            // after the context is sealed and strand its events. The close publishes them.
            eventCount = _closing > 0 && !_inCloseFlush.Value ? 0 : _pendingCount;
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

                Interlocked.Exchange(ref _pendingCount, 0);

                inFlight = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _inFlightPublishes.Add(inFlight.Task);
            }
        }
        finally
        {
            Monitor.Exit(_eventLock);
        }

        if (eventCount <= 0) return default;

        try
        {
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

                _publishScope.Value = new PublishScope(inFlight.Task, _publishScope.Value);
                var publishTask = _eventHandler.PublishAsync(this, publishEvent) ??
                                  throw new InvalidOperationException("Context publisher returned no task.");
                await publishTask.ConfigureUnboundContinuation();
                return new FlushOutcome(publishEvent, null);
            }
            catch (Exception e)
            {
                try
                {
                    Monitor.Enter(_eventLock);

                    if (exposures != null) _exposures.InsertRange(0, exposures);
                    if (achievements != null) _achievements.InsertRange(0, achievements);
                    Interlocked.Add(ref _pendingCount, eventCount);
                }
                finally
                {
                    Monitor.Exit(_eventLock);
                }

                return new FlushOutcome(null, e);
            }
        }
        finally
        {
            // Settled only after a failed batch is restored, so a close waiting for this
            // publication sees the restored events and can retry them.
            lock (_eventLock) _inFlightPublishes.Remove(inFlight.Task);
            inFlight.TrySetResult(true);
        }
    }

    private sealed class PublishScope
    {
        public PublishScope(Task publication, PublishScope parent)
        {
            Publication = publication;
            Parent = parent;
        }

        public Task Publication { get; }
        public PublishScope Parent { get; }
    }

    // Waits until every publication already handed to the publisher has settled, except
    // those whose publisher callback is the caller (a publisher that closes this context
    // cannot wait for its own publication without deadlocking).
    private async Task WaitForOutstandingPublishes()
    {
        while (true)
        {
            Task[] outstanding;
            lock (_eventLock)
            {
                outstanding = _inFlightPublishes.Where(t => !IsOwnPublication(t)).ToArray();
            }

            if (outstanding.Length == 0) return;

            await Task.WhenAll(outstanding).ConfigureUnboundContinuation();
        }
    }

    private bool HasOwnOutstandingPublication()
    {
        if (_publishScope.Value == null) return false;

        lock (_eventLock)
        {
            return _inFlightPublishes.Any(IsOwnPublication);
        }
    }

    // A synchronous Close()/Dispose() blocks the calling thread. Waiting there for a
    // publication that resumes on this thread's SynchronizationContext (a UI thread, or
    // classic ASP.NET) would deadlock, so it waits only when the thread has none; use
    // FinalizeAsync/DisposeAsync to wait on such threads.
    private static bool CanBlockOnOutstandingPublishes()
    {
        var context = SynchronizationContext.Current;
        return (context == null || context.GetType() == typeof(SynchronizationContext)) &&
               TaskScheduler.Current == TaskScheduler.Default;
    }

    private bool IsOwnPublication(Task publication)
    {
        for (var scope = _publishScope.Value; scope != null; scope = scope.Parent)
        {
            if (ReferenceEquals(scope.Publication, publication)) return true;
        }

        return false;
    }

    // Notifies the event logger of a flush outcome, then rethrows a publish error.
    // A throwing logger cannot turn a delivered publish into a failure (or mask the
    // publish error), as the Java SDK.
    private void ReportFlush(FlushOutcome outcome)
    {
        if (outcome.Error != null)
        {
            _logger?.LogError(outcome.Error, "{Publish}", EventType.Publish);
            LogEventSafely(EventType.Error, outcome.Error.Message);
            ExceptionDispatchInfo.Capture(outcome.Error).Throw();
        }

        if (outcome.Published != null) LogEventSafely(EventType.Publish, outcome.Published);
    }

    private byte[] GetUnitHash(string unitType, string unitUid)
    {
        return _hashedUnits.ConcurrentGetOrAdd(unitType, _ => Md5.HashToUtf8Bytes(unitUid));
    }

    // The attribute map audience filters evaluate against: system values (application,
    // environment) first, then user-set attributes overlaid on top, so a user-set value
    // always wins, including one set after an assignment was cached (SetAttribute bumps
    // _attrsSeq, which invalidates it). System attributes are not published; the collector
    // attaches them server-side.
    private Dictionary<string, object> BuildAttributesDictionary()
    {
        var attrs = new Dictionary<string, object>(_systemAttributes.Count + _attributes.Count);
        foreach (var systemAttribute in _systemAttributes)
        {
            attrs[systemAttribute.Key] = systemAttribute.Value;
        }
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

    private bool AudienceMatches(Experiment experiment, Assignment assignment, int attrsSeq)
    {
        // A rule-won assignment never consulted the audience, so an audience change cannot make
        // it stale.
        if (assignment.RuleOverride) return true;

        if (!string.IsNullOrEmpty(experiment.Audience) && attrsSeq > assignment.AttrsSeq)
        {
            var match = EvaluateAudience(experiment.Audience);
            var newAudienceMismatch = match != null ? !match.Value : false;
            if (newAudienceMismatch != assignment.AudienceMismatch)
            {
                return false;
            }
        }
        return true;
    }

    // Called with the read lock held, so it does not write any cache: a changed rule outcome is
    // reported as stale for the write path to rebuild. Rules are re-resolved only when they
    // changed (a refresh) or an attribute was set since the assignment was built.
    private bool RuleMatches(ExperimentVariables experiment, Assignment assignment, int attrsSeq)
    {
        var rulesChanged = !string.Equals(experiment.Data.AssignmentRules, assignment.RuleKey, StringComparison.Ordinal);
        if (!rulesChanged && attrsSeq <= assignment.AttrsSeq) return true;

        // An exposed split resolved without its unit keeps resolving as if the unit were still
        // missing, so a unit set afterwards cannot switch the variant the user already saw.
        var unitPinnedMissing = assignment.RuleMissingUnit && assignment.Exposed != 0;
        var rule = ResolveRule(experiment, unitPinnedMissing, false);
        int? cachedRuleVariant = assignment.RuleOverride ? assignment.Variant : null;
        if (rule?.Variant != cachedRuleVariant) return false;

        // Same variant, but now resolved by a split without its unit (or no longer so): stale, so
        // the write path rebuilds an unexposed assignment (letting SetUnit evict and re-randomize
        // it later) and updates an exposed one in place (see ReuseExposedRuleAssignment).
        return (rule?.MissingUnit ?? false) == assignment.RuleMissingUnit;
    }

    private readonly struct RuleResolution
    {
        public RuleResolution(int variant, bool missingUnit)
        {
            Variant = variant;
            MissingUnit = missingUnit;
        }

        public int Variant { get; }
        public bool MissingUnit { get; }
    }

    // Resolves the experiment's assignment rules to a variant, or null when no rule applies. As the
    // JavaScript and Java SDKs, the first matching rule decides: an assign variant out of range, or
    // a split without one percentage per variant, falls back to normal assignment without trying
    // later rules. A split is hashed with the experiment's own seed; with no unit to hash it gives
    // the experiment's no-unit result (the full-on variant, or 0) and is flagged MissingUnit.
    // Called with the context lock held. cacheAssigner is false on the read path, where the
    // hashed-unit and assigner caches must not be written (that would need the write lock).
    private RuleResolution? ResolveRule(ExperimentVariables experiment, bool unitPinnedMissing, bool cacheAssigner)
    {
        if (experiment.AssignmentRules == null || _audienceMatcher == null) return null;

        var data = experiment.Data;
        var action = _audienceMatcher.EvaluateRules(experiment.AssignmentRules, EnvironmentName,
            BuildAttributesDictionary(), data.Name, _logger);
        if (action == null) return null;

        var variantCount = data.Variants?.Length ?? 0;
        if (!action.IsSplit)
            return action.Variant >= 0 && action.Variant < variantCount
                ? new RuleResolution(action.Variant, false)
                : null;

        var split = action.Split;
        if (split.Length != variantCount) return null;

        var unitType = data.UnitType;
        var uid = unitType != null && !unitPinnedMissing && _units.TryGetValue(unitType, out var u) ? u : null;
        if (uid == null) return new RuleResolution(data.FullOnVariant, true);

        VariantAssigner assigner;
        if (cacheAssigner)
            assigner = GetVariantAssigner(unitType, GetUnitHash(unitType, uid));
        else if (!_assigners.TryGetValue(unitType, out assigner))
            assigner = new VariantAssigner(_hashedUnits.TryGetValue(unitType, out var hash)
                ? hash
                : Md5.HashToUtf8Bytes(uid));

        return new RuleResolution(assigner.Assign(split, data.SeedHi, data.SeedLo), false);
    }

    // Called with the write lock held. When the freshly resolved assignment is a rule win with
    // the same variant as an already-exposed cached one for the same experiment run, the cached
    // object is kept and only its rule bookkeeping refreshed: a new Assignment would reset its
    // exposure and publish the same decision a second time.
    private static Assignment ReuseExposedRuleAssignment(ExperimentVariables experiment, Assignment previous,
        Assignment fresh)
    {
        if (previous == null || experiment == null || !fresh.RuleOverride || !previous.RuleOverride ||
            previous.Overridden || previous.Variant != fresh.Variant || previous.Exposed == 0 ||
            !ExperimentMatches(experiment.Data, previous))
            return null;

        previous.RuleKey = fresh.RuleKey;
        previous.RuleMissingUnit = fresh.RuleMissingUnit;
        previous.AttrsSeq = fresh.AttrsSeq;
        return previous;
    }

    private static bool ExperimentMatches(Experiment experiment, Assignment assignment)
    {
        return experiment.Id == assignment.Id &&
               string.Equals(experiment.UnitType, assignment.UnitType) &&
               experiment.Iteration == assignment.Iteration &&
               experiment.FullOnVariant == assignment.FullOnVariant &&
               ArrayEquality.Equals(experiment.TrafficSplit, assignment.TrafficSplit);
    }

    // The environment assignment rules are scoped to: the one configured on the service, the same
    // value the system attributes carry. Null for contexts created without one.
    private string EnvironmentName =>
        _systemAttributes.TryGetValue("environment", out var environment) ? environment as string : null;

    private AudienceMatcher.ParsedRules ParseAssignmentRules(Experiment experiment)
    {
        if (string.IsNullOrEmpty(experiment.AssignmentRules) || _audienceMatcher == null) return null;

        var parsed = _audienceMatcher.ParseRules(experiment.AssignmentRules);
        if (parsed is { IsInvalid: true })
            _logger?.LogWarning("Invalid assignmentRules JSON for experiment '{ExperimentName}'; its rules are ignored",
                experiment.Name);
        return parsed;
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
                    // A rule-won assignment is not "assigned" either, but its variant came from
                    // the experiment's rules, so it is rebuilt once the experiment is gone.
                    if (!assignment.Assigned && !assignment.RuleOverride)
                        // previously not-running experiment
                        return assignment;
                }
                else if (assignment.RuleOverride ||
                         !_customAssignments.TryGetValue(experimentName, out var custom) ||
                         custom == assignment.Variant)
                {
                    // A matching rule wins over a custom assignment, so a custom mismatch is not
                    // staleness for a rule-won assignment.
                    // Read before evaluating: an attribute set meanwhile must trigger a new check.
                    var attrsSeq = _attrsSeq;
                    if (ExperimentMatches(experiment.Data, assignment) &&
                        RuleMatches(experiment, assignment, attrsSeq) &&
                        AudienceMatches(experiment.Data, assignment, attrsSeq))
                    {
                        // Still valid for these attributes: record it, so rules and audience are
                        // not re-evaluated on every call until the next SetAttribute (as the JS
                        // SDK). A racing reader can only store an older sequence, which merely
                        // causes one more evaluation.
                        if (attrsSeq > assignment.AttrsSeq) assignment.AttrsSeq = attrsSeq;
                        // assignment is up-to-date
                        return assignment;
                    }
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
            var attrsSeqAtBuild = _attrsSeq;

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

                    // Recorded for every assignment, including those no rule matched, so RuleMatches
                    // only re-resolves after a refresh changes the rules or an attribute is set.
                    assignment.RuleKey = experiment.Data.AssignmentRules;
                    var rule = ResolveRule(experiment, false, true);

                    if (rule == null && !string.IsNullOrEmpty(experiment.Data.Audience))
                    {
                        var match = EvaluateAudience(experiment.Data.Audience);
                        if (match != null) assignment.AudienceMismatch = !match.Value;
                    }

                    if (rule is { } ruleResolution)
                    {
                        // A matching rule is an explicit, author-specified assignment, like an
                        // override: it wins over the audience, traffic, full-on and custom
                        // assignments. Assigned stays false, so the exposure is not analysed.
                        assignment.RuleOverride = true;
                        assignment.RuleMissingUnit = ruleResolution.MissingUnit;
                        assignment.Variant = ruleResolution.Variant;
                    }
                    else if (experiment.Data.AudienceStrict && assignment.AudienceMismatch)
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

            assignment.AttrsSeq = attrsSeqAtBuild;

            var reused = ReuseExposedRuleAssignment(experiment,
                _assignmentCache.TryGetValue(experimentName, out var previous) ? previous : null, assignment);
            if (reused != null) return reused;

            _assignmentCache[experimentName] = assignment;

            return assignment;
        }
        finally
        {
            _contextLock.ExitWriteLock();
        }
    }

    private Assignment GetVariableAssignment(string key)
    {
        var experiments = GetVariableExperiments(key);
        if (experiments == null) return null;
        foreach (var experiment in experiments)
        {
            var assignment = GetAssignment(experiment.Data.Name);
            // As the Java SDK: the first assigned (or overridden) experiment by id wins, even
            // if its selected variant does not define the key (the caller then returns the
            // default). It is the experiment exposed, so units in its control arm are
            // counted too.
            if (assignment.Assigned || assignment.Overridden || assignment.RuleOverride)
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
    
    // Custom-field reads stay available after close, as in 1.3.0 and the Java SDK
    // (checkReady(false)).
    public List<string> GetCustomFieldKeys()
    {
        CheckReady(false);

        // Data locks are disposed once the context is disposed; no writer runs after close.
        if (IsClosed()) return CollectCustomFieldKeys();

        try
        {
            _dataLock.EnterReadLock();
            return CollectCustomFieldKeys();
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    private List<string> CollectCustomFieldKeys()
    {
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

    private ContextCustomFieldValue GetCustomField(string experimentName, string key)
    {
        if (IsClosed()) return FindCustomField(experimentName, key);

        try
        {
            _dataLock.EnterReadLock();
            return FindCustomField(experimentName, key);
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    private ContextCustomFieldValue FindCustomField(string experimentName, string key)
    {
        if (_contextCustomFields != null &&
            _contextCustomFields.TryGetValue(experimentName, out var customFieldValues) &&
            customFieldValues != null &&
            customFieldValues.TryGetValue(key, out var field))
        {
            return field;
        }

        return null;
    }

    // The parameter is an experiment name; it keeps its 1.3.0 name for named-argument callers.
    public object GetCustomFieldValue(string environmentName, string key)
    {
        CheckReady(false);

        var field = GetCustomField(environmentName, key);
        return field?.Value;
    }

    public object GetCustomFieldValueType(string experimentName, string key)
    {
        CheckReady(false);

        var field = GetCustomField(experimentName, key);
        return field?.Type;
    }

    // The parameter is an experiment name; it keeps its 1.3.0 name for named-argument callers.
    public object GetCustomFieldType(string environmentName, string key)
    {
        return GetCustomFieldValueType(environmentName, key);
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

    public async Task<IContextV2> WaitUntilReadyAsync()
    {
        var readyTcs = _readyTcs;
        if (readyTcs != null) await readyTcs.Task.ConfigureUnboundContinuation();
        return this;
    }

    public IContextV2 WaitUntilReady()
    {
        // The load continuation never needs the caller's synchronization context,
        // so blocking on it here cannot deadlock.
        _readyTcs?.Task.GetAwaiter().GetResult();
        return this;
    }

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

    // Read under the context read lock, as Context.java getAttribute(s): SetAttribute
    // appends under the write lock, and enumerating a List<T> while it is appended
    // to throws InvalidOperationException.
    public object GetAttribute(string name)
    {
        return ReadAttributes(attributes =>
        {
            for (var i = attributes.Count - 1; i >= 0; i--)
                if (attributes[i].Name == name)
                    return attributes[i].Value;
            return null;
        });
    }

    public Dictionary<string, object> Attributes
    {
        get
        {
            return ReadAttributes(attributes =>
            {
                var result = new Dictionary<string, object>(attributes.Count);
                foreach (var attribute in attributes)
                    result[attribute.Name] = attribute.Value;
                return result;
            });
        }
    }

    private T ReadAttributes<T>(Func<List<Attribute>, T> read)
    {
        // Once closed no writer can run (SetAttribute throws), and Dispose may have
        // released the lock, so read without it.
        if (IsClosed()) return read(_attributes);

        _contextLock.EnterReadLock();
        try
        {
            return read(_attributes);
        }
        finally
        {
            _contextLock.ExitReadLock();
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

        // A concurrent SetUnit may retire the assignment (Exposed 2) after it is read; it is then
        // re-resolved rather than returned without its exposure.
        Assignment assignment;
        do
        {
            assignment = GetAssignment(experimentName);
            Interlocked.Exchange(ref AfterAssignmentLookup, null)?.Invoke();
            if (doExposure && assignment.Exposed == 0)
                QueueExposure(assignment);
        } while (Volatile.Read(ref assignment.Exposed) == 2);

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
                return;
            }

            _units.Add(unitType, uidTrimmed);
            InvalidateRuleAssignmentsWithMissingUnit(unitType);
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

    // Called with the write lock held. A split rule resolved without its unit gave the no-unit
    // variant; with the unit now set it is evicted so the next call randomizes it, but only while
    // that variant is still unseen. Retiring it (Exposed 0 -> 2) makes a racing GetTreatment that
    // already holds it skip the exposure, so the user never gets two different variants exposed.
    private void InvalidateRuleAssignmentsWithMissingUnit(string unitType)
    {
        List<string> evicted = null;
        foreach (var entry in _assignmentCache)
        {
            var assignment = entry.Value;
            if (assignment.RuleOverride && assignment.RuleMissingUnit && unitType == assignment.UnitType &&
                Interlocked.CompareExchange(ref assignment.Exposed, 2, 0) == 0)
                (evicted ??= new List<string>()).Add(entry.Key);
        }

        if (evicted == null) return;
        foreach (var name in evicted) _assignmentCache.Remove(name);
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

        Assignment assignment;
        do
        {
            assignment = GetVariableAssignment(key);
            Interlocked.Exchange(ref AfterAssignmentLookup, null)?.Invoke();
            if (assignment?.Variables is null) return defaultValue;

            if (doExposure && assignment.Exposed == 0) QueueExposure(assignment);
        } while (Volatile.Read(ref assignment.Exposed) == 2);

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

    // Like the JavaScript SDK: throws when the context is not ready or is finalized,
    // and is a successful no-op on a failed context (a failed context is not recovered).
    public async Task RefreshAsync()
    {
        CheckReady(true);

        if (_failed) return;

        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
            try
            {
                var data = await FetchContextData(_dataProvider).ConfigureUnboundContinuation();

                // Closed while the fetch was in flight: the context is sealed (and its
                // locks may already be disposed), so the late data is dropped.
                if (IsDisposing()) return;

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
        if (_closed) throw new InvalidOperationException("ABSmartly Context is closed");
        if (_closing > 0) throw new InvalidOperationException("ABSmartly Context is closing");
    }

    private void CheckReady(bool expectNotClosed)
    {
        if (!IsReady())
        {
            throw new InvalidOperationException("ABSmartly Context is not yet ready");
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
        if (IsDisposing()) return;

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
                        await RefreshAsync().ConfigureUnboundContinuation();
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

            experimentVariables.AssignmentRules = ParseAssignmentRules(experiment);

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
                            // Through the configured parser, as 1.3.0 and Java
                            // (variableParser.parse): a consumer's custom IVariableParser
                            // keeps control of JSON custom fields. Only the exact built-in
                            // type takes the full-value path (see
                            // DefaultVariableParser.ParseCustomFieldValue for the supported
                            // JSON kinds). A consumer subclass of DefaultVariableParser
                            // reimplementing IVariableParser.Parse stays a configured parser
                            // and keeps control through Parse, so its custom-field
                            // transformation is not silently replaced by built-in parsing.
                            if (_variableParser.GetType() == typeof(DefaultVariableParser))
                            {
                                value.Value = ((DefaultVariableParser)_variableParser).ParseCustomFieldValue(
                                    experiment.Name, customFieldValue.Name, customValue);
                            }
                            else
                            {
                                value.Value = _variableParser.Parse(this, experiment.Name, customFieldValue.Name, customValue);
                            }
                        }
                        else if(customFieldValue.Type.StartsWith("boolean"))
                        {
                            value.Value = Convert.ToBoolean(customValue);
                        }
                        else if(customFieldValue.Type.StartsWith("number"))
                        {
                            // Invariant culture: the payload is JSON-formatted ("1.5"), whatever
                            // the server's culture is.
                            value.Value = long.TryParse(customValue, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out var longVal)
                                ? (object)longVal
                                : double.Parse(customValue, NumberStyles.Float, CultureInfo.InvariantCulture);
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
            // Empty, not null, experiments: reads on a failed context (Experiments,
            // GetCustomFieldKeys) return empty instead of throwing.
            _data = new ContextData(Array.Empty<Experiment>());
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

    // As 1.3.0 and the Java SDK, event logger exceptions propagate to the caller,
    // except where they would change a publish outcome (see LogEventSafely).
    private void LogEvent(EventType eventType, object data)
    {
        _eventLogger?.HandleEvent(this, eventType, data);
    }

    // For the Publish event and a publish Error: a throwing logger must not turn a
    // delivered publish into a failure (which would resend the batch) or mask the
    // publish error, so its exception is logged and dropped, as Context.java flush().
    private void LogEventSafely(EventType eventType, object data)
    {
        try
        {
            LogEvent(eventType, data);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Context event logger threw while handling {EventType}", eventType);
        }
    }

    private void LogError(Exception error)
    {
        LogEvent(EventType.Error, error.Message);
    }

    #endregion


    #region Close & Dispose

    // Concurrent callers share one close attempt. If the final publish fails, the
    // context stays open with its events queued and the error is rethrown, so a
    // later Close/Dispose retries the publish (as closeAsync() in the Java SDK).
    //
    // The attempt is settled (closed, or reset for a retry) before any event logger
    // callback runs, so a logger that synchronously hands Close()/Dispose() to another
    // thread and waits for it cannot wait on this attempt.
    private Task CloseAsync() => CloseAsync(true);

    private async Task CloseAsync(bool waitForOutstanding)
    {
        if (_closed) return;

        // Called back from this context's own close: by the publisher during the
        // final flush, or synchronously by the event logger it notifies. A no-op, as
        // the _closing guard in 1.3.0: waiting would deadlock, retrying would recurse.
        if (_inCloseFlush.Value || ReferenceEquals(t_notifyingClose, this)) return;

        TaskCompletionSource<bool> attempt;
        bool owner;
        lock (_closeLock)
        {
            if (_closed) return;
            owner = _closeAttempt == null;
            if (owner)
                _closeAttempt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            attempt = _closeAttempt;
        }

        if (!owner)
        {
            // Called from a publisher callback whose publication is still in flight while
            // another close is under way: that close waits for this publication, so
            // waiting for it here would deadlock. A no-op, as a close called back during
            // the close's own flush.
            if (HasOwnOutstandingPublication()) return;

            // A synchronous Close()/Dispose() on a thread with a SynchronizationContext
            // (UI, classic ASP.NET) blocks that thread. The close in progress may be
            // waiting for a publication whose continuation needs this thread, so joining
            // it here could deadlock: return without waiting, as a second close did in
            // 1.3.0. The close in progress finishes the job (and keeps failed events
            // retryable); use FinalizeAsync/DisposeAsync to wait for it on such threads.
            if (!waitForOutstanding) return;

            await attempt.Task.ConfigureUnboundContinuation();
            return;
        }

        // Set under _eventLock, so a flush either detached its batch before this point
        // (and is registered as outstanding below) or sees the close and leaves the batch.
        lock (_eventLock) Interlocked.Exchange(ref _closing, 1);
        ClearRefreshTimer();

        // A publication started before this close may still fail and restore its
        // events: wait for it, so they are retried by the flush below (or, if that
        // fails too, the context stays open and they remain retryable).
        if (waitForOutstanding) await WaitForOutstandingPublishes().ConfigureUnboundContinuation();

        // Publications this caller cannot wait for remain: its own (a publisher closing
        // its context), or any, for a synchronous Close()/Dispose() on a thread with a
        // SynchronizationContext. The events queued now are still published before the
        // caller returns (an app may exit right after Dispose()), as the close's own
        // flush. Sealing now could strand the outstanding publications' events if they
        // fail, so the close then stays in progress and finishes in the background once
        // they settle, retrying any events they restore. The caller returns without
        // waiting for them (waiting could deadlock); FinalizeAsync or DisposeAsync from
        // elsewhere waits for the whole close.
        FlushOutcome earlier = default;
        if (HasOutstandingPublishes())
        {
            earlier = await FlushForCloseAsync().ConfigureUnboundContinuation();
            if (earlier.Error != null)
            {
                SettleCloseAttempt(attempt, earlier);
                ReportCloseOutcome(default, earlier, false, false); // throws the publish error
                return;
            }

            if (HasOutstandingPublishes())
            {
                // Its Publish event is reported once the attempt settles, as any close
                // observer: a logger that hands a close to another thread and waits for
                // it must not find this attempt still in progress.
                _ = CompleteCloseWhenPublishesSettle(attempt, earlier);
                return;
            }
        }

        await CompleteCloseAsync(attempt, earlier).ConfigureUnboundContinuation();
    }

    private bool HasOutstandingPublishes()
    {
        lock (_eventLock) return _inFlightPublishes.Count > 0;
    }

    private Task CompleteCloseWhenPublishesSettle(TaskCompletionSource<bool> attempt, FlushOutcome earlier) =>
        Task.Run(async () =>
        {
            // A fresh flow: not part of the publisher callback or close that started it.
            _publishScope.Value = null;
            _inCloseFlush.Value = false;
            try
            {
                await WaitForOutstandingPublishes().ConfigureUnboundContinuation();
                await CompleteCloseAsync(attempt, earlier).ConfigureUnboundContinuation();
            }
            catch (Exception)
            {
                // Reported through the event logger (Error); the attempt carries it to
                // any waiter, and the context stays open with its events retryable.
            }
        });

    // Publishes what is pending, then seals the context or, if that fails, leaves it
    // open for a retry, and notifies the event logger (first of an earlier successful
    // flush of this close, if any).
    private async Task CompleteCloseAsync(TaskCompletionSource<bool> attempt, FlushOutcome earlier)
    {
        var outcome = await FlushForCloseAsync().ConfigureUnboundContinuation();
        var disposeLocks = SettleCloseAttempt(attempt, outcome);
        ReportCloseOutcome(earlier, outcome, true, disposeLocks);
    }

    // The close's own flush: it takes the batch while the close is in progress, and a
    // close called back from its publisher is a no-op.
    private async Task<FlushOutcome> FlushForCloseAsync()
    {
        if (_pendingCount <= 0) return default;

        _inCloseFlush.Value = true;
        try
        {
            return await FlushCore().ConfigureUnboundContinuation();
        }
        finally
        {
            _inCloseFlush.Value = false;
        }
    }

    // Settles the attempt before any observer runs: a logger that hands Close()/Dispose()
    // to another thread and waits for it then finds the context closed, or (after a
    // failure) starts a fresh attempt, instead of waiting on this one. Returns whether
    // the locks must now be disposed.
    private bool SettleCloseAttempt(TaskCompletionSource<bool> attempt, FlushOutcome outcome)
    {
        if (outcome.Error != null)
        {
            // As Java: the context stays open for a retry, without a refresh timer.
            lock (_closeLock)
            {
                _closeAttempt = null;
                Interlocked.Exchange(ref _closing, 0);
            }

            attempt.TrySetException(outcome.Error);
            // Waiters, if any, observe the error; without them it must not surface
            // as an UnobservedTaskException.
            _ = attempt.Task.Exception;
            return false;
        }

        bool disposeLocks;
        lock (_closeLock)
        {
            _closed = true;
            Interlocked.Exchange(ref _closing, 0);
            disposeLocks = _disposeAfterClose;
        }

        attempt.TrySetResult(true);
        return disposeLocks;
    }

    private void ReportCloseOutcome(FlushOutcome earlier, FlushOutcome outcome, bool logClose, bool disposeLocks)
    {
        var previous = t_notifyingClose;
        t_notifyingClose = this;
        try
        {
            if (earlier.Published != null) LogEventSafely(EventType.Publish, earlier.Published);
            ReportFlush(outcome);
            if (logClose) LogEvent(EventType.Close, null);
        }
        finally
        {
            t_notifyingClose = previous;

            if (disposeLocks) RequestDisposeLocks();
        }
    }

    // After a Dispose: disposes the locks if the context is closed, or, while a close is
    // in progress that this Dispose did not wait for, asks that close to dispose them.
    private void DisposeLocksAfterClose()
    {
        lock (_closeLock)
        {
            if (!_closed)
            {
                if (_closeAttempt != null) _disposeAfterClose = true;
                return;
            }
        }

        RequestDisposeLocks();
    }

    // Finalizes the context (publishes pending events, then seals it) without
    // releasing its locks, so units, attributes and experiments stay readable.
    public Task FinalizeAsync() => CloseAsync();

    public void Close()
    {
        var wait = CanBlockOnOutstandingPublishes();
        AsyncHelpers.RunSync(async () => await CloseAsync(wait));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // Public (as in 1.3.0) for binary compatibility.
    public void Dispose(bool disposing)
    {
        if (disposing)
        {
            var wait = CanBlockOnOutstandingPublishes();
            AsyncHelpers.RunSync(async () => await CloseAsync(wait));

            DisposeLocksAfterClose();
        }
    }

    private void RequestDisposeLocks()
    {
        lock (_closeLock)
        {
            if (_settlingLoad)
            {
                _locksDisposePending = true;
                return;
            }
        }

        DisposeLocks();
    }

    private void DisposeLocks()
    {
        lock (_closeLock)
        {
            if (_locksDisposed) return;
            _locksDisposed = true;
        }

        _dataLock?.Dispose();
        _contextLock?.Dispose();
        _timeout?.Dispose();
        _refreshTimer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureUnboundContinuation();

        DisposeLocksAfterClose();
        if (!_closed) return;

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    #endregion


    #region Helper classes

    public class ExperimentVariables
    {
        public Experiment Data { get; set; }
        public List<Dictionary<string, object>> Variables { get; set; }

        // Data.AssignmentRules parsed once when the data is installed (null when there are none).
        internal AudienceMatcher.ParsedRules AssignmentRules { get; set; }
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

        // The variant was set by a matching assignment rule. It wins over the audience, traffic,
        // full-on and custom assignments; the exposure is flagged so the collector excludes it
        // from analysis.
        internal bool RuleOverride { get; set; }
        // The assignmentRules string this assignment was resolved against (the environment is
        // fixed for the context's lifetime), so a refresh that changes the rules makes it stale.
        internal string RuleKey { get; set; }
        // A split rule matched while the experiment's unit was not set, so the experiment's own
        // no-unit variant was given instead of randomizing.
        internal bool RuleMissingUnit { get; set; }
    }

    #endregion
}