using Newtonsoft.Json.Linq;

namespace BowlPoolManager.Api.Services
{
    public class TestDocument
    {
        public string Container { get; set; } = string.Empty;
        public string PartitionKey { get; set; } = string.Empty;
        public JObject Body { get; set; } = new();
    }

    public class TestScenario
    {
        public string Name { get; set; } = string.Empty;
        public List<TestDocument> Documents { get; set; } = new();
    }

    public interface ITestScenarioProvider
    {
        IReadOnlyList<string> ScenarioNames { get; }

        /// <summary>Returns the named scenario, or null if the name is not in ScenarioNames.</summary>
        TestScenario? Get(string name);
    }
}
