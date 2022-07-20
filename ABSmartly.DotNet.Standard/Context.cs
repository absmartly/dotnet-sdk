using System.Collections.Generic;
using ABSmartly.DotNet.Time;
using ABSmartly.Internal;
using ABSmartly.Json;
using ABSmartly.Temp;
using Attribute = ABSmartly.Json.Attribute;

namespace ABSmartly;

public class Context
{
    private readonly Clock clock_;
	private readonly long publishDelay_;
	private readonly long refreshInterval_;
	private readonly IContextEventHandler eventHandler_;
	private readonly ContextEventLogger eventLogger_;
	private readonly IContextDataProvider dataProvider_;
	private readonly VariableParser variableParser_;
	private readonly AudienceMatcher audienceMatcher_;
	private readonly ScheduledExecutorService scheduler_;
	private readonly Dictionary<string, string> units_;
	private bool failed_;

	//private readonly ReentrantReadWriteLock dataLock_ = new ReentrantReadWriteLock();
	private ContextData data_;
	//private Dictionary<string, ExperimentVariables> index_;
	//private Dictionary<string, ExperimentVariables> indexVariables_;

	//private readonly ReentrantReadWriteLock contextLock_ = new ReentrantReadWriteLock();

	private readonly Dictionary<string, byte[]> hashedUnits_;
	private readonly Dictionary<string, VariantAssigner> assigners_;
	//private readonly Dictionary<string, Assignment> assignmentCache_ = new();

	//private readonly ReentrantLock eventLock_ = new ReentrantLock();
	private readonly List<Exposure> exposures_ = new List<Exposure>();
	private readonly List<GoalAchievement> achievements_ = new List<GoalAchievement>();

	private readonly List<Attribute> attributes_ = new List<Attribute>();
	private readonly Dictionary<string, int> overrides_;
	private readonly Dictionary<string, int> cassignments_;
	
	//private readonly AtomicInteger pendingCount_ = new AtomicInteger(0);
	//private readonly AtomicBoolean closing_ = new AtomicBoolean(false);
	//private readonly AtomicBoolean closed_ = new AtomicBoolean(false);
	//private readonly AtomicBoolean refreshing_ = new AtomicBoolean(false);

	//private volatile Task readyFuture_;
	//private volatile Task closingFuture_;
	//private volatile Task refreshFuture_;

	//private readonly ReentrantLock timeoutLock_ = new ReentrantLock();
	//private volatile ScheduledFuture<?> timeout_ = null;
	//private volatile ScheduledFuture<?> refreshTimer_ = null;
}