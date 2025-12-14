using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Services;

// MODERN SETUP: Uses FunctionsApplication instead of HostBuilder
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// 1. Register Cosmos Service
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();

// 2. One-time initialization logic (Self-Bootstrap)
// We build a temporary provider to access configuration safely
var sp = builder.Services.BuildServiceProvider();
var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
var connStr = config["CosmosDbConnectionString"];

// Only attempt DB creation if we have a connection string (skips during CI build)
if (!string.IsNullOrEmpty(connStr))
{
    try 
    {
        var client = new CosmosClient(connStr);
        var db = client.CreateDatabaseIfNotExistsAsync("BowlMadnessDb").GetAwaiter().GetResult();
        db.Database.CreateContainerIfNotExistsAsync("MainContainer", "/id").GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        // Safe logging for startup
        Console.WriteLine($"[Startup Warning] DB Setup skipped: {ex.Message}");
    }
}

builder.Build().Run();
