using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
builder.Services.AddSingleton<Container>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var connStr = config["CosmosDbConnectionString"];
    
    // Fail gracefully if missing during build
    if (string.IsNullOrEmpty(connStr)) return null!; 

    var clientOptions = new CosmosClientOptions { SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } };
    var client = new CosmosClient(connStr, clientOptions);
    
    // Bootstrap: Ensure Database & Container exist
    var db = client.CreateDatabaseIfNotExistsAsync(Constants.Database.DbName).GetAwaiter().GetResult();
    var containerResponse = db.Database.CreateContainerIfNotExistsAsync(Constants.Database.ContainerName, Constants.Database.PartitionKeyPath).GetAwaiter().GetResult();
    
    return containerResponse.Container;
});

builder.Services.AddSingleton<IGameRepository, GameRepository>();
builder.Services.AddSingleton<IPoolRepository, PoolRepository>();
builder.Services.AddSingleton<IEntryRepository, EntryRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
// --------------------------------

builder.Build().Run();
