using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Core;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Logging.AllowAppInformationLogs();

builder.Services.AddHttpClient<ICfbdService, CfbdService>();
builder.Services.AddHttpClient<IBasketballDataService, BasketballDataService>();
builder.Services.AddHttpClient<IEspnDataService, EspnDataService>();
builder.Services.AddSingleton<IGameScoringService, GameScoringService>();
builder.Services.AddSingleton<IHoopsGameScoringService, HoopsGameScoringService>();

// --- REPOSITORIES & DATABASE ---
// --- 1. OPTIMIZED REGISTRATION (Fast, No Network Calls) ---
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var connStr = config["CosmosDbConnectionString"];
    if (string.IsNullOrEmpty(connStr))
    {
        connStr = config.GetConnectionString("CosmosDbConnectionString");
    }
    
    if (string.IsNullOrEmpty(connStr)) throw new InvalidOperationException("CosmosDbConnectionString is missing.");

    var clientOptions = new CosmosClientOptions 
    { 
        SerializerOptions = new CosmosSerializationOptions 
        { 
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase 
        } 
    };
    
    return new CosmosClient(connStr, clientOptions);
});

// REMOVED: Single Container registration is no longer needed.

builder.Services.AddSingleton<IGameRepository, GameRepository>();
builder.Services.AddSingleton<IPoolRepository, PoolRepository>();
builder.Services.AddSingleton<IHoopsPoolRepository, HoopsPoolRepository>();
builder.Services.AddSingleton<IHoopsGameRepository, HoopsGameRepository>();
builder.Services.AddSingleton<IBracketGeneratorService, BracketGeneratorService>();
builder.Services.AddSingleton<IEntryRepository, EntryRepository>();
builder.Services.AddSingleton<IHoopsEntryRepository, HoopsEntryRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IArchiveRepository, ArchiveRepository>();
builder.Services.AddSingleton<ISeasonRepository, SeasonRepository>();
builder.Services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddSingleton<IMigrationRepository, MigrationRepository>();

// --- TEST-SITE ONLY FEATURES (ResetTestData). The guard is true only when SiteEnvironment=Test and
// the Cosmos account is the test account; production never sets SiteEnvironment. ---
builder.Services.AddSingleton<ITestEnvironment>(sp =>
    new TestEnvironment(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<CosmosClient>().Endpoint));
builder.Services.AddSingleton<ITestDataRepository, TestDataRepository>();
builder.Services.AddSingleton<ITestScenarioProvider, EmbeddedTestScenarioProvider>();

// --- SCORE FEED. Football uses College Football Data unless a SuperAdmin has switched the stored mode to Mock
// (Score Feed admin page). The mode is read on every call; missing or unreadable means Live. ---
builder.Services.AddSingleton<CfbdScoreFeed>();
builder.Services.AddSingleton<IMockFeedStore, MockFeedStore>();
builder.Services.AddSingleton<IMockFeedStartingSet, EmbeddedMockFeedStartingSet>();
builder.Services.AddSingleton<IScoreFeedSettingsStore, ScoreFeedSettingsStore>();
builder.Services.AddSingleton<IMockFeedResetService, MockFeedResetService>();
builder.Services.AddSingleton<MockScoreFeed>();
builder.Services.AddSingleton<IScoreFeed>(sp => new SwitchingScoreFeed(
    sp.GetRequiredService<CfbdScoreFeed>(),
    sp.GetRequiredService<MockScoreFeed>(),
    sp.GetRequiredService<IScoreFeedSettingsStore>(),
    sp.GetRequiredService<ILogger<SwitchingScoreFeed>>()));
// --------------------------------

var host = builder.Build();

// Database initialization logic has been moved to InfrastructureFunctions.cs (InitializeDatabase)
// to improve startup performance. It is now manually triggered via the Admin Dashboard.
// --------------------------------

await host.RunAsync();
