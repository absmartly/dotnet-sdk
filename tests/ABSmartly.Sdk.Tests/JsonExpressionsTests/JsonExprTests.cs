using ABSmartly.JsonExpressions;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests;

[TestFixture]
public class JsonExprTests
{
    [TestCaseSource(nameof(GetJsonCases))]
    public void TestCases((List<object> rule, Dictionary<string, object> person, bool expected) input)
    {
        var (rules, person, expected) = input;
        JsonExpr.EvaluateBooleanExpr(rules, person).Should().Be(expected);
    }


    public static IEnumerable<(List<object> rule, Dictionary<string, object> person, bool expected)> GetJsonCases()
    {
        // People
        var john = new Dictionary<string, object>
        {
            { "age", 20 },
            { "language", "en-US" },
            { "returning", false }
        };

        var terry = new Dictionary<string, object>
        {
            { "age", 20 },
            { "language", "en-GB" },
            { "returning", true }
        };

        var kate = new Dictionary<string, object>
        {
            { "age", 50 },
            { "language", "es-ES" },
            { "returning", false }
        };

        var maria = new Dictionary<string, object>
        {
            { "age", 52 },
            { "language", "pt-PT" },
            { "returning", true }
        };

        // Rules
        var ageTwentyAndUs = new List<object>
        {
            BinaryOp("eq", VarFor("age"), ValueFor(20)),
            BinaryOp("eq", VarFor("language"), ValueFor("en-US"))
        };

        var ageOverFifty = new List<object>
        {
            BinaryOp("gte", VarFor("age"), ValueFor(50))
        };

        var ageTwentyAndUsOrAgeOverFifty = new List<object>
        {
            new Dictionary<string, object>
            {
                { "or", new List<object> { ageTwentyAndUs, ageOverFifty } }
            }
        };

        var returning = new List<object>
        {
            VarFor("returning")
        };

        var returningAndAgeTwentyAndUsOrAgeOverFifty = new List<object>
        {
            returning, ageTwentyAndUsOrAgeOverFifty
        };

        var notReturningAndSpanish = new List<object>
        {
            UnaryOp("not", returning),
            BinaryOp("eq", VarFor("language"), ValueFor("es-ES"))
        };

        // Age twenty and US English
        yield return (ageTwentyAndUs, john, true);
        yield return (ageTwentyAndUs, terry, false);
        yield return (ageTwentyAndUs, kate, false);
        yield return (ageTwentyAndUs, maria, false);

        // // age over fifty
        yield return (ageOverFifty, john, false);
        yield return (ageOverFifty, terry, false);
        yield return (ageOverFifty, kate, true);
        yield return (ageOverFifty, maria, true);

        // age twenty and US, or age over fifty
        yield return (ageTwentyAndUsOrAgeOverFifty, john, true);
        yield return (ageTwentyAndUsOrAgeOverFifty, terry, false);
        yield return (ageTwentyAndUsOrAgeOverFifty, kate, true);
        yield return (ageTwentyAndUsOrAgeOverFifty, maria, true);

        // returning
        yield return (returning, john, false);
        yield return (returning, terry, true);
        yield return (returning, kate, false);
        yield return (returning, maria, true);

        // returning and age twenty and US, or age over fifty
        yield return (returningAndAgeTwentyAndUsOrAgeOverFifty, john, false);
        yield return (returningAndAgeTwentyAndUsOrAgeOverFifty, terry, false);
        yield return (returningAndAgeTwentyAndUsOrAgeOverFifty, kate, false);
        yield return (returningAndAgeTwentyAndUsOrAgeOverFifty, maria, true);

        // not returning and Spanish
        yield return (notReturningAndSpanish, john, false);
        yield return (notReturningAndSpanish, terry, false);
        yield return (notReturningAndSpanish, kate, true);
        yield return (notReturningAndSpanish, maria, false);
    }

    private static Dictionary<string, object> ValueFor(object p)
    {
        return new() { ["value"] = p };
    }

    private static Dictionary<string, object> VarFor(object p)
    {
        return new()
        {
            ["var"] = new Dictionary<string, object>
            {
                ["path"] = p
            }
        };
    }

    private static Dictionary<string, object> UnaryOp(string op, object arg)
    {
        return new() { [op] = arg };
    }

    private static Dictionary<string, object> BinaryOp(string op, object lhs, object rhs)
    {
        return new()
        {
            [op] = new List<object> { lhs, rhs }
        };
    }
}