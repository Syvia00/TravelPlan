namespace TravelPlan.Shared.Models.Enums;

/// <summary>
/// Named, explicit comparisons over TripRole's numeric ordering (Viewer=0 &lt; Editor=1), used
/// instead of raw &lt;/&lt;=/&gt;/&gt;= operators at call sites. This protects readers and
/// reviewers — a call site says what it means instead of relying on remembering which direction
/// is "more privileged" — not the database: see TripCollaboratorConfiguration.Role for the actual
/// storage-level fix (Role stored as int, not HasConversion&lt;string&gt;()) that keeps a
/// query-translated comparison numerically correct in the first place.
/// </summary>
public static class TripRoleExtensions
{
    /// <summary>Does this role grant at least the privilege of <paramref name="minimumRole"/>?</summary>
    public static bool SatisfiesMinimum(this TripRole role, TripRole minimumRole) => role >= minimumRole;

    /// <summary>Is this role strictly more privileged than <paramref name="other"/>?</summary>
    public static bool IsHigherThan(this TripRole role, TripRole other) => role > other;
}
