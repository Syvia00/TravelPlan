using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using TravelPlan.Api.Services;

namespace TravelPlan.Tests;

/// <summary>
/// Exercises DatabaseMigrationRunner's retry-with-backoff loop directly, using real
/// Microsoft.Data.SqlClient.SqlException instances (built via reflection — SqlException has no
/// public constructor) carrying the exact error numbers Azure SQL actually produces. Session
/// 6.9's follow-up fix replaced an unguarded, unretried `Database.Migrate()` call — which crashed
/// the whole process on a single transient connection failure during the database's auto-pause
/// window — with this retry loop. The success path (a healthy database, migrations apply on the
/// first try) was confirmed by running the app locally end to end; these tests are what actually
/// prove the retry-then-succeed and retry-until-exhausted paths behave as designed, since forcing
/// a real Azure SQL serverless database to be paused at the exact moment a deploy lands isn't
/// something that can be exercised on demand.
///
/// A fake delay function replaces Task.Delay so these tests run in milliseconds rather than
/// actually waiting through the real ~2s-to-30s backoff schedule.
/// </summary>
public class DatabaseMigrationRunnerTests
{
    private static SqlException MakeSqlException(int number)
    {
        var asm = typeof(SqlException).Assembly;
        var errorCollectionType = asm.GetType("Microsoft.Data.SqlClient.SqlErrorCollection")!;
        var errorType = asm.GetType("Microsoft.Data.SqlClient.SqlError")!;

        var errorCollection = Activator.CreateInstance(errorCollectionType, nonPublic: true)!;
        var error = Activator.CreateInstance(
            errorType,
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [number, (byte)1, (byte)20, "server", "message", "procedure", 1, (Exception?)null],
            culture: null)!;

        errorCollectionType.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(errorCollection, [error]);

        var createException = typeof(SqlException).GetMethod(
            "CreateException",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [errorCollectionType, typeof(string)],
            modifiers: null)!;

        return (SqlException)createException.Invoke(null, [errorCollection, "11.0.0"])!;
    }

    private static bool IsTransient(Exception ex) =>
        ex is SqlException sqlEx && sqlEx.Number is 40613 or 40501 or 40540 or 49918 or 49919 or 49920 or 4060 or 10928 or 10929;

    [Fact]
    public async Task TransientErrorTwiceThenSuccess_RetriesAndSucceeds()
    {
        var attempts = 0;
        var delaysRequested = new List<TimeSpan>();

        Task MigrateAction(CancellationToken ct)
        {
            attempts++;
            if (attempts <= 2)
            {
                throw MakeSqlException(40613);
            }

            return Task.CompletedTask;
        }

        await DatabaseMigrationRunner.RunWithRetryAsync(
            MigrateAction,
            IsTransient,
            NullLogger.Instance,
            CancellationToken.None,
            delay: (delay, ct) =>
            {
                delaysRequested.Add(delay);
                return Task.CompletedTask;
            });

        Assert.Equal(3, attempts);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], delaysRequested);
    }

    [Fact]
    public async Task NonTransientError_StopsImmediatelyWithoutRetrying()
    {
        var attempts = 0;
        var delaysRequested = new List<TimeSpan>();

        Task MigrateAction(CancellationToken ct)
        {
            attempts++;
            // 208 ("Invalid object name") is a real, non-transient SQL error — a bad migration
            // script or schema mismatch, not a database that's still waking up.
            throw MakeSqlException(208);
        }

        await DatabaseMigrationRunner.RunWithRetryAsync(
            MigrateAction,
            IsTransient,
            NullLogger.Instance,
            CancellationToken.None,
            delay: (delay, ct) =>
            {
                delaysRequested.Add(delay);
                return Task.CompletedTask;
            });

        Assert.Equal(1, attempts);
        Assert.Empty(delaysRequested);
    }

    [Fact]
    public async Task PersistentTransientError_ExhaustsRetriesWithoutThrowing()
    {
        var attempts = 0;
        var delaysRequested = new List<TimeSpan>();

        Task MigrateAction(CancellationToken ct)
        {
            attempts++;
            throw MakeSqlException(40613);
        }

        // Must not throw — a persistent failure is logged, not propagated, so the host keeps
        // running and /health stays green even though migrations never applied.
        await DatabaseMigrationRunner.RunWithRetryAsync(
            MigrateAction,
            IsTransient,
            NullLogger.Instance,
            CancellationToken.None,
            delay: (delay, ct) =>
            {
                delaysRequested.Add(delay);
                return Task.CompletedTask;
            });

        Assert.Equal(10, attempts);
        Assert.Equal(9, delaysRequested.Count);
        // Backoff doubles from 2s, capped at 30s: 2, 4, 8, 16, 30, 30, 30, 30, 30.
        int[] expectedSeconds = [2, 4, 8, 16, 30, 30, 30, 30, 30];
        Assert.Equal(expectedSeconds.Select(s => TimeSpan.FromSeconds(s)), delaysRequested);
    }
}
