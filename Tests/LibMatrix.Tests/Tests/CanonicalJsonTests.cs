using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using LibMatrix.Extensions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class CanonicalJsonTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : TestBed<TestFixture>(testOutputHelper, fixture) {
    // Test cases from https://spec.matrix.org/v1.11/appendices/#examples
    private static readonly FrozenDictionary<string, string> testCases = new Dictionary<string, string>() {
        ["{}"] = "{}",
        ["{\n    \"one\": 1,\n    \"two\": \"Two\"\n}\n"] = "{\"one\":1,\"two\":\"Two\"}",
        ["{\n    \"b\": \"2\",\n    \"a\": \"1\"\n}\n"] = "{\"a\":\"1\",\"b\":\"2\"}",
        ["{\"b\":\"2\",\"a\":\"1\"}"] = "{\"a\":\"1\",\"b\":\"2\"}",
        ["{\n    \"auth\": {\n        \"success\": true,\n        \"mxid\": \"@john.doe:example.com\",\n        \"profile\": {\n            \"display_name\": \"John Doe\",\n            \"three_pids\": [\n                {\n                    \"medium\": \"email\",\n                    \"address\": \"[email protected]\"\n                },\n                {\n                    \"medium\": \"msisdn\",\n                    \"address\": \"123456789\"\n                }\n            ]\n        }\n    }\n}\n"] =
            "{\"auth\":{\"mxid\":\"@john.doe:example.com\",\"profile\":{\"display_name\":\"John Doe\",\"three_pids\":[{\"address\":\"[email protected]\",\"medium\":\"email\"},{\"address\":\"123456789\",\"medium\":\"msisdn\"}]},\"success\":true}}",
        ["{\n    \"a\": \"日本語\"\n}\n"] = "{\"a\":\"日本語\"}",
        ["{\n    \"本\": 2,\n    \"日\": 1\n}\n"] = "{\"日\":1,\"本\":2}",
        ["{\n    \"a\": \"\\u65E5\"\n}\n"] = "{\"a\":\"日\"}",
        ["{\n    \"a\": null\n}\n"] = "{\"a\":null}",
        ["{\n    \"a\": -0,\n    \"b\": 1e10\n}\n"] = "{\"a\":0,\"b\":10000000000}"
    }.ToFrozenDictionary();

    [Fact]
    public void SpecTests() {
        var i = 0;
        foreach (var (input, expected) in testCases) {
            var deserialised = JsonSerializer.Deserialize<Dictionary<string, object>>(input);
            var actual = CanonicalJsonSerializer.Serialize(deserialised);
            Assert.Equal(expected, actual);
            // testOutputHelper.WriteLine($"Test case {i++} successful!");
        }
    }

    [Fact]
    public void RepeatTests() {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1_000_000; i++) {
            SpecTests();
            if (i % 10000 == 0) {
                testOutputHelper.WriteLine($"{i} loops successful! Delta: {sw.Elapsed}");
                sw.Restart();
            }
        }
    }
}