using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Auth;
using TravelPlan.Api.Data;
using TravelPlan.Api.Middleware;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Api.Services.Reports;
using TravelPlan.Shared.DTOs.Trips;

var builder = WebApplication.CreateBuilder(args);

// Data
builder.Services.AddDbContext<TravelPlanDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=travelplan.dev.db"));
// Azure SQL provider swap for production lands in Phase 2.

// CORS — allows the Blazor WebAssembly dev server's origin to call this API. Production origins
// (Azure Static Web Apps) get their own policy when the app is actually deployed in Phase 2.
const string WebAppCorsPolicy = "WebApp";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy(WebAppCorsPolicy, policy =>
        policy.WithOrigins("https://localhost:7156", "http://localhost:5169")
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

// Auth — DevAuthHandler only, in Development. Entra External ID + phone OTP land in Phase 3.
var authenticationBuilder = builder.Services.AddAuthentication(DevAuthHandler.SchemeName);
if (builder.Environment.IsDevelopment())
{
    authenticationBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, options => { });
}
builder.Services.AddAuthorization();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<CreateTripDtoValidator>();

// Repositories
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<IPlanItemRepository, PlanItemRepository>();
builder.Services.AddScoped<IBudgetItemRepository, BudgetItemRepository>();
builder.Services.AddScoped<IAccommodationRepository, AccommodationRepository>();
builder.Services.AddScoped<ITravelLegRepository, TravelLegRepository>();
builder.Services.AddScoped<IDestinationRepository, DestinationRepository>();
builder.Services.AddScoped<ITripMemoryRepository, TripMemoryRepository>();

// Current-user context, populated per-request by UserSyncMiddleware
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Reports — SkiaSharp draws once (PNG + PDF from the same recorded picture); the Razor
// component renders the same ReportData as HTML via HtmlRenderer, no Blazor hosting needed.
builder.Services.AddScoped<ITripReportService, TripReportService>();
builder.Services.AddScoped<IReportHtmlRenderer, ReportHtmlRenderer>();
builder.Services.AddScoped<HtmlRenderer>();

// Trip completion — manual (POST /api/trips/{id}/complete) and automatic (EndDate passing,
// checked periodically by the background service) both funnel through the same service.
builder.Services.AddScoped<ITripCompletionService, TripCompletionService>();
builder.Services.AddHostedService<TripCompletionBackgroundService>();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SupportNonNullableReferenceTypes());
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TravelPlanDbContext>();
    db.Database.Migrate();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(WebAppCorsPolicy);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<UserSyncMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
