using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core;

namespace BowlPoolManager.Api.Functions
{
    /// <summary>
    /// Test-site only. Order of checks is fixed: caller (SuperAdmin) first, then the TestEnvironment guard,
    /// then the scenario name, and only then any delete. See docs/plans/phase-0-test-environment.md, step 3.
    /// </summary>
    public class TestDataFunctions
    {
        private static readonly string[] ResetContainers =
        {
            Constants.Database.SeasonsContainer,
            Constants.Database.PicksContainer,
            Constants.Database.ArchivesContainer
        };

        private readonly ILogger _logger;
        private readonly IUserRepository _userRepo;
        private readonly ITestEnvironment _testEnvironment;
        private readonly ITestDataRepository _testData;
        private readonly ITestScenarioProvider _scenarios;

        public TestDataFunctions(
            ILoggerFactory loggerFactory,
            IUserRepository userRepo,
            ITestEnvironment testEnvironment,
            ITestDataRepository testData,
            ITestScenarioProvider scenarios)
        {
            _logger = loggerFactory.CreateLogger<TestDataFunctions>();
            _userRepo = userRepo;
            _testEnvironment = testEnvironment;
            _testData = testData;
            _scenarios = scenarios;
        }

        [Function("GetTestEnvironmentStatus")]
        public async Task<HttpResponseData> GetTestEnvironmentStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "TestEnvironment")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var enabled = _testEnvironment.IsEnabled;
            return await JsonResponseAsync(req, new
            {
                isTestEnvironment = enabled,
                scenarios = enabled ? _scenarios.ScenarioNames : Array.Empty<string>()
            });
        }

        [Function("ResetTestData")]
        public async Task<HttpResponseData> ResetTestData(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ResetTestData/{scenario}")] HttpRequestData req,
            string scenario)
        {
            // 1. Caller check first (SuperAdmin).
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            // 2. Guard: test site only.
            if (!_testEnvironment.IsEnabled)
            {
                _logger.LogWarning("ResetTestData refused: not a test environment.");
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("Test data tools are not available on this site.");
                return forbidden;
            }

            // 3. Scenario name, before anything is deleted.
            var data = _scenarios.Get(scenario);
            if (data == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Unknown scenario.");
                return badReq;
            }

            // 4. Delete, then load.
            var deleted = new Dictionary<string, int>();
            foreach (var container in ResetContainers)
                deleted[container] = await _testData.DeleteAllAsync(container);

            var loaded = new Dictionary<string, int>();
            foreach (var doc in data.Documents)
            {
                await _testData.UpsertAsync(doc.Container, doc.PartitionKey, doc.Body);
                loaded[doc.Container] = loaded.GetValueOrDefault(doc.Container) + 1;
            }

            _logger.LogInformation("ResetTestData loaded scenario {Scenario}.", data.Name);
            return await JsonResponseAsync(req, new { scenario = data.Name, deleted, loaded });
        }

        private static async Task<HttpResponseData> JsonResponseAsync(HttpRequestData req, object body)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(body));
            return response;
        }
    }
}
