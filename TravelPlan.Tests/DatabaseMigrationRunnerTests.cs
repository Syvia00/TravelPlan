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
///
/// This file's own history is itself the argument for testing against real exception shapes: its
/// first version passed 16/16 locally and in CI, then the very next real production deploy hit a
/// 40613 that this retry loop failed to retry — because EF Core wraps the transient SqlException in
/// an InvalidOperationException, a shape no test here constructed. See IsTransientSqlException's
/// doc comment and the wrapped-exception tests below.
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

    // Deliberately calling the real DatabaseMigrationRunner.IsTransientSqlException here, not a
    // hand-copied predicate — the first version of this test file had its own local copy that
    // checked `ex is SqlException` directly, which passed against a bare SqlException in tests but
    // missed the InvalidOperationException wrapper EF Core's SqlServerExecutionStrategy actually
    // throws in production (see IsTransientSqlException's doc comment). Testing against the real
    // method is the only way that gap couldn't have recurred silently.
    private static readonly Func<Exception, bool> IsTransient = DatabaseMigrationRunner.IsTransientSqlException;

    [Theory]
    [InlineData(40613, true)]
    [InlineData(40501, true)]
    [InlineData(4060, true)]
    [InlineData(208, false)] // "Invalid object name" — a real, non-transient SQL error.
    public void IsTransientSqlException_BareSqlException_MatchesOnlyKnownTransientNumbers(int number, bool expectedTransient)
    {
        Assert.Equal(expectedTransient, DatabaseMigrationRunner.IsTransientSqlException(MakeSqlException(number)));
    }

    [Fact]
    public void IsTransientSqlException_WrappedInInvalidOperationException_StillDetectsIt()
    {
        // The exact shape observed in production (2026-09-29 08:46 UTC): EF Core's default
        // (non-retrying) SqlServerExecutionStrategy detects the transient SqlException itself and
        // rethrows it wrapped in an InvalidOperationException recommending EnableRetryOnFailure,
        // rather than letting the bare SqlException propagate.
        var wrapped = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure. Consider enabling transient error resiliency by adding 'EnableRetryOnFailure' to the 'UseSqlServer' call.",
            MakeSqlException(40613));

        Assert.True(DatabaseMigrationRunner.IsTransientSqlException(wrapped));
    }

    [Fact]
    public void IsTransientSqlException_UnrelatedException_ReturnsFalse()
    {
        Assert.False(DatabaseMigrationRunner.IsTransientSqlException(new InvalidOperationException("unrelated")));
    }

    [Fact]
    public async Task WrappedTransientErrorThenSuccess_RetriesAndSucceeds()
    {
        // Reproduces the exact end-to-end shape of the 2026-09-29 08:46 UTC incident: MigrateAsync
        // throws the EF-wrapped InvalidOperationException(SqlException 40613), not a bare
        // SqlException. Before the IsTransientSqlException fix, this would have logged critical and
        // given up after attempt 1 instead of retrying — exactly what happened in production.
        var attempts = 0;
        var delaysRequested = new List<TimeSpan>();

        Task MigrateAction(CancellationToken ct)
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException(
                    "Consider enabling transient error resiliency by adding 'EnableRetryOnFailure' to the 'UseSqlServer' call.",
                    MakeSqlException(40613));
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

        Assert.Equal(2, attempts);
        Assert.Equal([TimeSpan.FromSeconds(2)], delaysRequested);
    }

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
