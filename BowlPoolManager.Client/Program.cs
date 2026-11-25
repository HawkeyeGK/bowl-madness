using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using BowlPoolManager.Client;
using BowlPoolManager.Client.Security;
using BowlPoolManager.Client.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// AUTHENTICATION SERVICES
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, StaticWebAppsAuthenticationStateProvider>();

// SITE CONTEXT
builder.Services.AddScoped<ISiteContext, SiteContext>();

// COMPONENT LIBRARY (MudBlazor ships with the app: no CDN) AND THE HEADER POOL SWITCHER'S STATE
builder.Services.AddMudServices();
builder.Services.AddScoped<PoolSelection>();
builder.Services.AddScoped<MyEntryStore>();

// DOMAIN SERVICES
builder.Services.AddScoped<IPoolService, PoolService>();
builder.Services.AddScoped<IHoopsPoolService, HoopsPoolService>();
builder.Services.AddScoped<IHoopsGameService, HoopsGameService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

// REGISTER APPSTATE
builder.Services.AddScoped<AppState>();

await builder.Build().RunAsync();
