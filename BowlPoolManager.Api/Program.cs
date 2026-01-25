using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
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
// --------------------------------

var host = builder.Build();

// --- 2. ASYNC INITIALIZATION (The "Self-Healing" Step) ---
// This runs once on startup, asynchronously, ensuring the DB exists before accepting traffic.
using (var scope = host.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    
    // Only attempt if connection string is present (skips during build pipeline if env vars missing)
    if (!string.IsNullOrEmpty(config["CosmosDbConnectionString"]))
    {
        try 
        {
            var client = scope.ServiceProvider.GetRequiredService<CosmosClient>();
            
            // Async creation - perfectly safe here!
            var db = await client.CreateDatabaseIfNotExistsAsync(Constants.Database.DbName);
            
            // Create New Containers
            await db.Database.CreateContainerIfNotExistsAsync(Constants.Database.PlayersContainer, Constants.Database.DefaultPartitionKey);
            await db.Database.CreateContainerIfNotExistsAsync(Constants.Database.SeasonsContainer, Constants.Database.SeasonPartitionKey);
            await db.Database.CreateContainerIfNotExistsAsync(Constants.Database.PicksContainer, Constants.Database.SeasonPartitionKey);
            // NEW: Archives Container
            await db.Database.CreateContainerIfNotExistsAsync(Constants.Database.ArchivesContainer, Constants.Database.SeasonPartitionKey);
            // NEW: Configuration Container
            await db.Database.CreateContainerIfNotExistsAsync(Constants.Database.ConfigurationContainer, Constants.Database.DefaultPartitionKey);
        }
        catch (Exception ex)
        {
            // Log but don't crash - allows the function to start even if DB has issues (e.g. firewall)
            // though requests will likely fail later.
            logger.LogError(ex, "Error during database bootstrapping.");
        }
    }
}
// --------------------------------

await host.RunAsync();
