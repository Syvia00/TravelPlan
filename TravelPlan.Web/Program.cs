using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TravelPlan.Web;
using TravelPlan.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
var apiScope = builder.Configuration["ApiScope"]
    ?? throw new InvalidOperationException("ApiScope must be configured (see appsettings.json).");

// Auth — Entra External ID (CIAM) via MSAL. DevAuthStateProvider removed; every request now
// needs a real signed-in identity, including local dev. Phone OTP (Azure Communication
// Services) is deliberately deferred — see deployment-runbook.md section 5.
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add(apiScope);
});

// Both named clients get a 3-minute timeout instead of HttpClient's 100-second default — a cold
// Container App plus a paused Azure SQL database can take 70s+ just for the container, before the
// database's own auto-resume (and DatabaseMigrationRunner's retry budget) adds more on top. A
// single measured cold start already hit 71s; 100s left too little headroom. ApiClientBase's own
// one-shot GET retry (~2s later) handles the more common case of an outright failure or 502/503/504
// during that same window — this timeout just makes sure a merely *slow* first response isn't
// also cut off client-side before it has a chance to complete.
var apiClientTimeout = TimeSpan.FromMinutes(3);

// The API client's HttpClient goes through AuthorizationMessageHandler so every call carries a
// bearer token MSAL acquires silently (or via redirect, if silent acquisition fails).
builder.Services.AddHttpClient("TravelPlan.Api", client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = apiClientTimeout;
    })
    .AddHttpMessageHandler(sp =>
    {
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
        handler.ConfigureHandler(authorizedUrls: [apiBaseUrl], scopes: [apiScope]);
        return handler;
    });
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("TravelPlan.Api"));

// A second, unauthenticated named client for share-link visitors — Pages/SharedTrip.razor builds
// its own ApiClient instances against this one (with the token set as a default "X-Share-Token"
// header) instead of using the DI-registered ApiClients above, which always go through
// AuthorizationMessageHandler and would force an anonymous visitor into an MSAL login redirect.
builder.Services.AddHttpClient("TravelPlan.Api.Shared", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = apiClientTimeout;
});

builder.Services.AddScoped<TripsApiClient>();
builder.Services.AddScoped<PlanItemsApiClient>();
builder.Services.AddScoped<PlanItemPhotosApiClient>();
builder.Services.AddScoped<BudgetItemsApiClient>();
builder.Services.AddScoped<AccommodationsApiClient>();
builder.Services.AddScoped<TravelLegsApiClient>();
builder.Services.AddScoped<TripMemoriesApiClient>();
builder.Services.AddScoped<TripCollaboratorsApiClient>();
builder.Services.AddScoped<TripShareLinksApiClient>();

await builder.Build().RunAsync();
