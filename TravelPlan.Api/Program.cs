using System.Security.Claims;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using TravelPlan.Api.Auth;
using TravelPlan.Api.Data;
using TravelPlan.Api.Middleware;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Api.Services.Reports;
using TravelPlan.Shared.DTOs.Trips;

var builder = WebApplication.CreateBuilder(args);

// Data — provider choice is an explicit setting ("DatabaseProvider": "SqlServer"), not a
// heuristic on whether ConnectionStrings:DefaultConnection happens to be non-empty: local dev
// already puts the SQLite file path in that same key (appsettings.Development.json), so "is it
// set" can't distinguish the two. Decoupled from ASPNETCORE_ENVIRONMENT too — Development still
// selects Swagger/auto-migrate below even when pointed at the real Azure SQL database; auth
// itself no longer depends on the environment (see the AzureAd/JwtBearer setup below).
//
// Each provider gets its own DbContext subclass (TravelPlanSqliteDbContext /
// TravelPlanSqlServerDbContext) with its own Migrations/<Provider> folder, because migrations bake
// in provider-specific column types at scaffold time — one shared history can't serve both. All
// repositories/services depend only on the base TravelPlanDbContext, so registering by base type
// here (AddDbContext<TravelPlanDbContext, TConcrete>) keeps this split invisible to them. See
// docs/migrations.md for the resulting day-to-day migration workflow.
var useSqlServer = string.Equals(builder.Configuration["DatabaseProvider"], "SqlServer", StringComparison.OrdinalIgnoreCase);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (useSqlServer)
{
    builder.Services.AddDbContext<TravelPlanDbContext, TravelPlanSqlServerDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    builder.Services.AddDbContext<TravelPlanDbContext, TravelPlanSqliteDbContext>(options =>
        options.UseSqlite(connectionString ?? "Data Source=travelplan.dev.db"));
}

// CORS — allows the Blazor WebAssembly dev server's origin to call this API. Production origins
// (Azure Static Web Apps) get their own policy when the app is actually deployed in Phase 2.
const string WebAppCorsPolicy = "WebApp";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy(WebAppCorsPolicy, policy =>
        policy.WithOrigins(
                "https://localhost:7156",
                "http://localhost:5169",
                "https://jolly-sand-066886f10.5.azurestaticapps.net")
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

// Auth — Entra External ID (CIAM) JWT bearer validation, or an anonymous share-link token (an
// "X-Share-Token" header) as an alternative for a trip's Viewer/Editor share link — see
// ShareLinkAuthHandler. Phone OTP (Azure Communication Services) is deliberately deferred — see
// deployment-runbook.md section 5.
//
// "SmartAuth" is a policy scheme, not a handler: it picks which real scheme authenticates the
// request based on whether the share-token header is present, so every existing [Authorize]
// (no scheme specified) keeps working unchanged and transparently accepts either.
const string SmartAuthScheme = "SmartAuth";
var authBuilder = builder.Services.AddAuthentication(SmartAuthScheme)
    .AddPolicyScheme(SmartAuthScheme, "JWT or share link", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ShareLinkAuthHandler.HeaderName)
                ? ShareLinkAuthHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
    });
// AddMicrosoftIdentityWebApi returns a specialized builder that doesn't chain .AddScheme, so
// register it off the original AuthenticationBuilder instead of chaining further.
authBuilder.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
authBuilder.AddScheme<AuthenticationSchemeOptions, ShareLinkAuthHandler>(ShareLinkAuthHandler.SchemeName, options => { });

// External ID's v2.0 tokens carry claims under their raw JWT names ("oid"/"email"/"name"), not
// the ClaimTypes.* URIs UserSyncMiddleware reads — normalize them once here so that middleware
// (and anything else reading ClaimTypes.*) doesn't need to know the token's actual shape. "oid"
// is the stable per-user object ID; "sub" is the fallback for token shapes that omit it.
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var innerOnTokenValidated = options.Events?.OnTokenValidated;
    options.Events ??= new JwtBearerEvents();
    options.Events.OnTokenValidated = async context =>
    {
        if (innerOnTokenValidated is not null)
        {
            await innerOnTokenValidated(context);
        }

        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            var externalAuthId = identity.FindFirst("oid")?.Value ?? identity.FindFirst("sub")?.Value;
            if (externalAuthId is not null)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, externalAuthId));
            }

            var email = identity.FindFirst("email")?.Value ?? identity.FindFirst("emails")?.Value;
            if (email is not null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, email));
            }

            var name = identity.FindFirst("name")?.Value;
            if (name is not null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, name));
            }
        }
    };
});

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
builder.Services.AddScoped<ITripCollaboratorRepository, TripCollaboratorRepository>();
builder.Services.AddScoped<ITripShareLinkRepository, TripShareLinkRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Current-user context, populated per-request by UserSyncMiddleware
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// The single access-check authority every controller uses instead of an ad hoc ownership
// query — see ITripAccessService.
builder.Services.AddScoped<ITripAccessService, TripAccessService>();

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
