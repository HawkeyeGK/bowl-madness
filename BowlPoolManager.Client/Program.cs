using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using BowlPoolManager.Client;
using BowlPoolManager.Client.Security;
using BowlPoolManager.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// AUTHENTICATION SERVICES
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, StaticWebAppsAuthenticationStateProvider>();

// DOMAIN SERVICES
builder.Services.AddScoped<IPoolService, PoolService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();

// REGISTER APPSTATE
builder.Services.AddScoped<AppState>();

await builder.Build().RunAsync();
