using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core; 

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICfbdService, CfbdService>();
builder.Services.AddSingleton<IGameScoringService, GameScoringService>();

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
builder.Services.AddSingleton<IEntryRepository, EntryRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IArchiveRepository, ArchiveRepository>();
builder.Services.AddSingleton<ISeasonRepository, SeasonRepository>();
builder.Services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddSingleton<IMigrationRepository, MigrationRepository>();
// --------------------------------

var host = builder.Build();

// Database initialization logic has been moved to InfrastructureFunctions.cs (InitializeDatabase)
// to improve startup performance. It is now manually triggered via the Admin Dashboard.
// --------------------------------

await host.RunAsync();
