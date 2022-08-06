using ABSmartly.JsonExpressions;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests;

[TestFixture]
public class JsonExprTests
{
    private JsonExpr jsonExpr;

    #region Helper & Test Data

    // Helper
    private static KeyValuePair<string, object> ValueFor(object p)
    {
        return new KeyValuePair<string, object>("value", p);
    }
    private static KeyValuePair<string, object> VarFor(object p)
    {
        return new KeyValuePair<string, object>("var", 
            new Dictionary<string, object>
            {
                { "path", p }
            });
    }
    private static Dictionary<string, object> UnaryOp(string op, object arg)
    {
        return new Dictionary<string, object> { { op, arg } };
    }
    private static Dictionary<string, object> BinaryOp(string op, object lhs, object rhs)
    {
        return new Dictionary<string, object>()
        {
            { op, new List<object> { lhs, rhs } }
        };
    }

    // People
    private Dictionary<string, object> john;
    private Dictionary<string, object> terry;
    private Dictionary<string, object> kate;
    private Dictionary<string, object> maria;

    // Rules
    private List<object> ageTwentyAndUS;
    private List<object> ageOverFifty;
    private List<object> ageTwentyAndUS_Or_AgeOverFifty;
    private List<object> returning;
    private List<object> returning_And_AgeTwentyAndUS_Or_AgeOverFifty;
    private List<object> notReturning_And_Spanish;    

    #endregion

    [SetUp]
    public void Init()
    {
        jsonExpr = new JsonExpr();

        // People
        john = new Dictionary<string, object>
        {
            { "age", 20 },
            { "language", "en-US" },
            { "returning", false }
        };
        terry = new Dictionary<string, object>
        {
            { "age", 20 },
            { "language", "en-GB" },
            { "returning", true }
        };
        kate = new Dictionary<string, object>
        {
            { "age", 50 },
            { "language", "es-ES" },
            { "returning", false }
        };
        maria = new Dictionary<string, object>
        {
            { "age", 52 },
            { "language", "pt-PT" },
            { "returning", true }
        };

        // Rules
        ageTwentyAndUS = new List<object>
        {
            BinaryOp("eq", VarFor("age"), ValueFor(20)),
            BinaryOp("eq", VarFor("language"), ValueFor("en-US"))
        };
        ageOverFifty = new List<object>
        {
            BinaryOp("gte", VarFor("age"), ValueFor(50))
        };
        ageTwentyAndUS_Or_AgeOverFifty = new List<object>
        {
            new Dictionary<string, object>
            {
                { "or", new List<object> { ageTwentyAndUS, ageOverFifty } }
            }
        };
        returning = new List<object>
        {
            VarFor("returning")
        };
        returning_And_AgeTwentyAndUS_Or_AgeOverFifty = new List<object>
        {
            returning, ageTwentyAndUS_Or_AgeOverFifty
        };
        notReturning_And_Spanish = new List<object>
        {
            UnaryOp("not", returning),
            BinaryOp("eq", VarFor("language"), ValueFor("es-ES"))
        };
    }
}