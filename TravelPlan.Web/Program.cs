using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TravelPlan.Web;
using TravelPlan.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddScoped<TripsApiClient>();
builder.Services.AddScoped<PlanItemsApiClient>();
builder.Services.AddScoped<BudgetItemsApiClient>();
builder.Services.AddScoped<AccommodationsApiClient>();
builder.Services.AddScoped<TravelLegsApiClient>();
builder.Services.AddScoped<TripMemoriesApiClient>();

// Dev-auth bypass — see README.md. Real auth (Entra External ID + phone OTP) lands in Phase 3.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, DevAuthStateProvider>();

await builder.Build().RunAsync();
