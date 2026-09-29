using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;

namespace TravelPlan.Api.Services;

/// <summary>
/// Applies pending EF Core migrations with retry-with-backoff on transient Azure SQL errors —
/// specifically 40613 ("database is currently unavailable" — the exact error a serverless
/// database's auto-pause/auto-resume produces if a connection lands while it's still waking up).
///
/// Session 6/6.9's production outage happened because Program.cs used to call
/// `db.Database.Migrate()` directly, synchronously, in the blocking path *before* `app.Run()` —
/// so a single transient connection failure threw straight out of an unhandled call, aborting the
/// whole process (SIGABRT) before Kestrel ever started listening. Azure's own container startup
/// probe against /health never got a response in time, so it killed the container and called the
/// deploy failed; Azure's own retry then hit the same race again, repeatedly, once for every
/// following restart attempt that happened to land while the database was paused — a genuine
/// multi-hour crash-loop, not a one-off.
///
/// This is invoked from a background task registered on IHostApplicationLifetime.ApplicationStarted
/// (see Program.cs) — deliberately *not* from a call that the host awaits as part of its own
/// startup sequence (an IHostedService.StartAsync is awaited by the generic host before it
/// finishes starting). ApplicationStarted only fires once Kestrel is already bound and listening,
/// so the health check Azure's own warm-up probe uses is live immediately regardless of whether the
/// database happens to be awake yet — the one thing that actually prevents the container from
/// being killed for a slow migration, independent of how long the database takes to resume.
///
/// The retry loop itself lives in RunWithRetryAsync, taking the migration action, the transient
/// check, and the delay function as parameters — not because production needs that flexibility,
/// but because it's the only way to actually exercise the retry-then-succeed and
/// retry-until-exhausted paths in a test without waiting for real backoff delays or a real Azure
/// SQL database to be paused on demand. See DatabaseMigrationRunnerTests.
/// </summary>
public static class DatabaseMigrationRunner
{
    // Azure SQL error numbers that are transient specifically around connecting to a database that
    // isn't fully available *yet*, not general query failures. 40613 is the one directly observed
    // in production; the rest are well-documented Azure SQL "retry the connection" codes kept here
    // as a deliberately small, named set — not a blanket "retry everything" policy, since most SQL
    // exceptions (bad query, constraint violation, bad credentials) should still surface immediately.
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        40613, // Database unavailable — auto-pause/resume in progress, or failing over.
        40501, // Service busy — throttling.
        40540, // Service busy.
        49918, // Resource governance: not enough resources to process this request.
        49919, // Resource governance: too many create/update operations in progress.
        49920, // Resource governance: too many requests.
        4060, // Cannot open the requested database — can occur transiently at login.
        10928, // Resource limit reached (session count).
        10929, // Resource limit reached (worker threads).
    ];

    private const int MaxAttempts = 10;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retries roughly 2, 4, 8, 16, 30, 30, 30, 30, 30 seconds apart across 10 attempts (~3.5
    /// minutes total) — comfortably longer than Azure SQL serverless's typical auto-resume time
    /// (documented as usually a few seconds, occasionally up to about a minute), but bounded: a
    /// failure past that point is logged loudly as an actual problem rather than retried forever
    /// silently, since by then it's more likely a real configuration/connectivity issue than a
    /// slow resume.
    /// </summary>
    public static async Task MigrateWithRetryAsync(IServiceProvider rootServices, CancellationToken cancellationToken)
    {
        using var scope = rootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TravelPlanDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseMigrationRunner).FullName!);

        await RunWithRetryAsync(db.Database.MigrateAsync, IsTransientSqlException, logger, cancellationToken);
    }

    /// <summary>
    /// Real production incident (2026-09-29 08:46 UTC, first deploy of this retry logic): the
    /// exception MigrateAsync actually throws for a 40613 is NOT a bare SqlException — EF Core's
    /// SqlServerExecutionStrategy (the default, non-retrying one, since TravelPlanSqlServerDbContext
    /// never calls EnableRetryOnFailure) detects the transient SqlException itself and wraps it in
    /// an InvalidOperationException ("...consider enabling transient error resiliency by adding
    /// 'EnableRetryOnFailure'...") with the real SqlException as InnerException. An `ex is
    /// SqlException` check alone never matches that wrapper, so the very first version of this
    /// method treated a genuinely transient 40613 as non-transient and gave up after attempt 1 —
    /// caught live in production, not in a test, because there was no test yet covering the actual
    /// wrapped shape EF produces. Walking the InnerException chain is what actually catches it.
    /// </summary>
    internal static bool IsTransientSqlException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlEx && TransientErrorNumbers.Contains(sqlEx.Number))
            {
                return true;
            }
        }

        return false;
    }

    internal static async Task RunWithRetryAsync(
        Func<CancellationToken, Task> migrateAction,
        Func<Exception, bool> isTransient,
        ILogger logger,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        var currentDelay = InitialDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await migrateAction(cancellationToken);
                logger.LogInformation("Database migration completed (attempt {Attempt}/{MaxAttempts}).", attempt, MaxAttempts);
                return;
            }
            catch (Exception ex) when (isTransient(ex) && attempt < MaxAttempts)
            {
                logger.LogWarning(ex,
                    "Database migration attempt {Attempt}/{MaxAttempts} hit a transient error " +
                    "(likely the database waking up from auto-pause). Retrying in {DelaySeconds}s.",
                    attempt, MaxAttempts, currentDelay.TotalSeconds);

                await delay(currentDelay, cancellationToken);
                currentDelay = TimeSpan.FromSeconds(Math.Min(currentDelay.TotalSeconds * 2, MaxDelay.TotalSeconds));
            }
            catch (Exception ex)
            {
                // Either a non-transient error, or the last attempt exhausted its retries — either
                // way, don't crash the process. The app keeps running and serving /health; any
                // route that touches the database will surface its own clear error until this is
                // fixed and the app restarted (or, for a transient case that outlasted our budget,
                // until an operator retries the deploy once the database is confirmed awake).
                logger.LogCritical(ex,
                    "Database migration failed on attempt {Attempt}/{MaxAttempts} and will not be retried further. " +
                    "The application will continue running, but requests that touch the database will fail until " +
                    "this is resolved.", attempt, MaxAttempts);
                return;
            }
        }
    }
}
