using System.Text;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class EdgeCaseInputTests
{
    [SetUp]
    public void SetUp()
    {
        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _eventHandler = Mock.Of<IContextEventHandler>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    private readonly Dictionary<string, string> _units = new()
    {
        ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec",
        ["user_id"] = "123456789"
    };

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);

    private IContextDataProvider _dataProvider = null!;
    private IContextEventLogger _eventLogger = null!;
    private IContextEventHandler _eventHandler = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [Test]
    public void TestUnicodeStringHandling()
    {
        var unicodeExperimentName = "exp_test_日本語_emoji_🎉";
        var unicodeVariableName = "设置.颜色";
        var unicodeVariableValue = "中文值_العربية_עברית_🌍";

        var experiment = new Experiment
        {
            Id = 1,
            Name = unicodeExperimentName,
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "Control", Config = null },
                new ExperimentVariant
                {
                    Name = "Treatment_测试",
                    Config = $"{{\"{unicodeVariableName}\":\"{unicodeVariableValue}\"}}"
                }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var experiments = context.GetExperiments();
        experiments.Should().Contain(unicodeExperimentName);

        var treatment = context.GetTreatment(unicodeExperimentName);
        treatment.Should().BeGreaterThanOrEqualTo(0);

        if (treatment == 1)
        {
            var variableValue = context.GetVariableValue(unicodeVariableName, "default");
            variableValue.Should().Be(unicodeVariableValue);
        }
    }

    [Test]
    public void TestSpecialCharactersInKeys()
    {
        var specialCharExperiments = new[]
        {
            "exp-with-dashes",
            "exp_with_underscores",
            "exp.with.dots",
            "exp:with:colons",
            "exp/with/slashes"
        };

        var experiments = specialCharExperiments.Select((name, i) => new Experiment
        {
            Id = i + 1,
            Name = name,
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515 + i,
            SeedLo = 233373850 + i,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant
                {
                    Name = "B",
                    Config = $"{{\"var-with-dash\":1,\"var_with_underscore\":2,\"var.with.dot\":3}}"
                }
            }
        }).ToArray();

        var data = new ContextData(experiments);
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        foreach (var expName in specialCharExperiments)
        {
            var treatment = context.GetTreatment(expName);
            treatment.Should().BeGreaterThanOrEqualTo(0);
            treatment.Should().BeLessThanOrEqualTo(1);
        }

        context.PendingCount.Should().Be(specialCharExperiments.Length);
    }

    [Test]
    public void TestDeeplyNestedVariables()
    {
        var nestedConfig = @"{
            ""level1"": {
                ""level2"": {
                    ""level3"": {
                        ""level4"": {
                            ""level5"": {
                                ""deepValue"": ""found_it""
                            }
                        }
                    }
                }
            },
            ""array_nested"": [
                {
                    ""nested_in_array"": {
                        ""value"": 42
                    }
                },
                [1, 2, [3, 4, [5, 6]]]
            ]
        }".Replace("\n", "").Replace("\r", "").Replace("  ", "");

        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_nested",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = nestedConfig }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var treatment = context.GetTreatment("exp_nested");
        treatment.Should().BeGreaterThanOrEqualTo(0);

        if (treatment == 1)
        {
            var level1 = context.GetVariableValue("level1", null);
            level1.Should().NotBeNull();
            level1.Should().BeOfType<Dictionary<string, object>>();

            var arrayNested = context.GetVariableValue("array_nested", null);
            arrayNested.Should().NotBeNull();
            arrayNested.Should().BeOfType<List<object>>();
        }
    }

    [Test]
    public void TestExtremeNumericValues()
    {
        var extremeConfig = @"{
            ""max_int"": 2147483647,
            ""min_int"": -2147483648,
            ""max_long"": 9223372036854775807,
            ""min_long"": -9223372036854775808,
            ""large_double"": 1.7976931348623157e308,
            ""small_double"": 2.2250738585072014e-308,
            ""zero"": 0,
            ""negative_zero"": -0.0,
            ""small_decimal"": 0.000000001,
            ""scientific_notation"": 1.23e10
        }".Replace("\n", "").Replace("\r", "").Replace("  ", "");

        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_extreme_numbers",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = extremeConfig }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var treatment = context.GetTreatment("exp_extreme_numbers");

        if (treatment == 1)
        {
            var maxInt = context.GetVariableValue("max_int", 0);
            maxInt.Should().Be(2147483647L);

            var minInt = context.GetVariableValue("min_int", 0);
            minInt.Should().Be(-2147483648L);

            var zero = context.GetVariableValue("zero", -1);
            zero.Should().Be(0L);

            var scientificNotation = context.GetVariableValue("scientific_notation", 0.0);
            scientificNotation.Should().BeOfType<double>();
        }
    }

    [Test]
    public void TestEmptyAndNullValues()
    {
        var emptyConfig = @"{
            ""empty_string"": """",
            ""null_value"": null,
            ""empty_array"": [],
            ""empty_object"": {},
            ""whitespace_string"": ""   ""
        }".Replace("\n", "").Replace("\r", "").Replace("  ", "");

        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_empty_values",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = emptyConfig }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var treatment = context.GetTreatment("exp_empty_values");

        if (treatment == 1)
        {
            var emptyString = context.GetVariableValue("empty_string", "default");
            emptyString.Should().Be("");

            var nullValue = context.GetVariableValue("null_value", "default");
            nullValue.Should().BeNull();

            var emptyArray = context.GetVariableValue("empty_array", null);
            emptyArray.Should().BeOfType<List<object>>();
            ((List<object>)emptyArray).Should().BeEmpty();

            var emptyObject = context.GetVariableValue("empty_object", null);
            emptyObject.Should().BeOfType<Dictionary<string, object>>();
            ((Dictionary<string, object>)emptyObject).Should().BeEmpty();
        }
    }

    [Test]
    public void TestBooleanValues()
    {
        var boolConfig = @"{
            ""true_value"": true,
            ""false_value"": false
        }".Replace("\n", "").Replace("\r", "").Replace("  ", "");

        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_bool_values",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = boolConfig }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var treatment = context.GetTreatment("exp_bool_values");

        if (treatment == 1)
        {
            var trueValue = context.GetVariableValue("true_value", false);
            trueValue.Should().Be(true);

            var falseValue = context.GetVariableValue("false_value", true);
            falseValue.Should().Be(false);
        }
    }

    [Test]
    public void TestVeryLongStrings()
    {
        var longString = new string('a', 10000);
        var longKeyName = new string('k', 1000);

        var longConfig = $"{{\"{longKeyName}\":\"{longString}\"}}";

        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_long_strings",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = longConfig }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var treatment = context.GetTreatment("exp_long_strings");

        if (treatment == 1)
        {
            var value = context.GetVariableValue(longKeyName, "default");
            value.Should().Be(longString);
            ((string)value).Length.Should().Be(10000);
        }
    }

    [Test]
    public void TestEscapedCharactersInStrings()
    {
        var escapedConfig = @"{
            ""with_quotes"": ""value with \""quotes\"""",
            ""with_backslash"": ""path\\to\\file"",
            ""with_newline"": ""line1\nline2"",
            ""with_tab"": ""col1\tcol2"",
            ""with_unicode_escape"": ""\u0048\u0065\u006C\u006C\u006F""
        }".Replace("\n", "").Replace("\r", "").Replace("  ", "");

        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_escaped_chars",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = escapedConfig }
            }
        };

        var data = new ContextData(new[] { experiment });
        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var treatment = context.GetTreatment("exp_escaped_chars");

        if (treatment == 1)
        {
            var withQuotes = context.GetVariableValue("with_quotes", "default");
            withQuotes.Should().Be("value with \"quotes\"");

            var withBackslash = context.GetVariableValue("with_backslash", "default");
            withBackslash.Should().Be("path\\to\\file");

            var withUnicodeEscape = context.GetVariableValue("with_unicode_escape", "default");
            withUnicodeEscape.Should().Be("Hello");
        }
    }

    private Context CreateContext(ContextConfig config, ContextData data) =>
        new(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory());
}
