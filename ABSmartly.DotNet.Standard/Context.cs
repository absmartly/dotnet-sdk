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

public class Context
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

    public Context()
    {

    }
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