using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Register Cosmos Service
        services.AddSingleton<ICosmosDbService, CosmosDbService>();

        // One-time initialization logic (Self-Bootstrap)
        var sp = services.BuildServiceProvider();
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var connStr = config["CosmosDbConnectionString"];
        if (!string.IsNullOrEmpty(connStr))
        {
            // Ensure Database and Container exist on startup
            var client = new CosmosClient(connStr);
            var db = client.CreateDatabaseIfNotExistsAsync("BowlMadnessDb").GetAwaiter().GetResult();
            // We use /id as partition key for simplicity in this mixed container
            db.Database.CreateContainerIfNotExistsAsync("MainContainer", "/id").GetAwaiter().GetResult();
        }
    })
    .Build();

host.Run();
