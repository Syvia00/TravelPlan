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

// The API client's HttpClient goes through AuthorizationMessageHandler so every call carries a
// bearer token MSAL acquires silently (or via redirect, if silent acquisition fails).
builder.Services.AddHttpClient("TravelPlan.Api", client => client.BaseAddress = new Uri(apiBaseUrl))
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
builder.Services.AddHttpClient("TravelPlan.Api.Shared", client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddScoped<TripsApiClient>();
builder.Services.AddScoped<PlanItemsApiClient>();
builder.Services.AddScoped<BudgetItemsApiClient>();
builder.Services.AddScoped<AccommodationsApiClient>();
builder.Services.AddScoped<TravelLegsApiClient>();
builder.Services.AddScoped<TripMemoriesApiClient>();
builder.Services.AddScoped<TripCollaboratorsApiClient>();
builder.Services.AddScoped<TripShareLinksApiClient>();

await builder.Build().RunAsync();
