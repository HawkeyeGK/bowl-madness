using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace BowlPoolManager.Api.Services
{
    /// <summary>
    /// Loads the named test scenarios from JSON fixtures embedded in this assembly (Scenarios/*.json).
    /// The name is looked up in a fixed list, never used to build a path.
    ///
    /// A date in a fixture is the text {now+240h} or {now-36h}, meaning that many hours from the moment
    /// the scenario is loaded, so a scenario is never stale.
    /// </summary>
    public class EmbeddedTestScenarioProvider : ITestScenarioProvider
    {
        private static readonly string[] Names = { "before-selection-day", "entry-window", "mid-season" };
        private static readonly Regex DateToken = new(@"^\{now([+-])(\d+)h\}$", RegexOptions.Compiled);

        private const string ResourceRoot = "BowlPoolManager.Api.Scenarios";

        private readonly Func<DateTime> _utcNow;

        public EmbeddedTestScenarioProvider() : this(() => DateTime.UtcNow) { }

        public EmbeddedTestScenarioProvider(Func<DateTime> utcNow)
        {
            _utcNow = utcNow;
        }

        public IReadOnlyList<string> ScenarioNames => Names;

        public TestScenario? Get(string name)
        {
            if (!Names.Contains(name, StringComparer.Ordinal)) return null;

            var resource = $"{ResourceRoot}.{name}.json";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Scenario fixture {name} is missing.");
            using var reader = new StreamReader(stream);
            var file = JObject.Parse(reader.ReadToEnd());

            var now = _utcNow();
            var scenario = new TestScenario { Name = name };
            foreach (var item in (JArray)file["documents"]!)
            {
                var body = (JObject)item["body"]!;
                ExpandDates(body, now);
                scenario.Documents.Add(new TestDocument
                {
                    Container = item.Value<string>("container")!,
                    PartitionKey = item.Value<string>("partitionKey")!,
                    Body = body
                });
            }
            return scenario;
        }

        private static void ExpandDates(JToken token, DateTime now)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties().ToList())
                {
                    if (prop.Value.Type == JTokenType.String && TryExpand(prop.Value.Value<string>()!, now, out var date))
                        prop.Value = new JValue(date);
                    else
                        ExpandDates(prop.Value, now);
                }
            }
            else if (token is JArray arr)
            {
                foreach (var child in arr) ExpandDates(child, now);
            }
        }

        private static bool TryExpand(string text, DateTime now, out string date)
        {
            date = string.Empty;
            var m = DateToken.Match(text);
            if (!m.Success) return false;

            var hours = int.Parse(m.Groups[2].Value) * (m.Groups[1].Value == "-" ? -1 : 1);
            date = now.AddHours(hours).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            return true;
        }
    }
}
