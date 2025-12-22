using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Repositories; // NEW
using BowlPoolManager.Core; 

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICfbdService, CfbdService>();
builder.Services.AddSingleton<IGameScoringService, GameScoringService>();

// --- LEGACY: Register Cosmos Service (Keep for now) ---
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();
// -----------------------------------------------------

// --- NEW: Register Container & Repositories ---
builder.Services.AddSingleton<Container>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var connStr = config["CosmosDbConnectionString"];
    
    // Fail gracefully if missing during build
    if (string.IsNullOrEmpty(connStr)) return null!; 

    var clientOptions = new CosmosClientOptions { SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } };
    var client = new CosmosClient(connStr, clientOptions);
    
    // Ensure Database & Container exist
    var db = client.CreateDatabaseIfNotExistsAsync(Constants.Database.DbName).GetAwaiter().GetResult();
    var containerResponse = db.Database.CreateContainerIfNotExistsAsync(Constants.Database.ContainerName, Constants.Database.PartitionKeyPath).GetAwaiter().GetResult();
    
    return containerResponse.Container;
});

builder.Services.AddSingleton<IGameRepository, GameRepository>();
builder.Services.AddSingleton<IPoolRepository, PoolRepository>();
builder.Services.AddSingleton<IEntryRepository, EntryRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
// -----------------------------------------------

// Bootstrap for Legacy Service (Can be removed in Phase 4)
var sp = builder.Services.BuildServiceProvider();
var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
var connStr = config["CosmosDbConnectionString"];

if (!string.IsNullOrEmpty(connStr))
{
    try 
    {
        var client = new CosmosClient(connStr);
        var db = client.CreateDatabaseIfNotExistsAsync(Constants.Database.DbName).GetAwaiter().GetResult();
        db.Database.CreateContainerIfNotExistsAsync(Constants.Database.ContainerName, Constants.Database.PartitionKeyPath).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup Warning] DB Setup skipped: {ex.Message}");
    }
}

builder.Build().Run();
