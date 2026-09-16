namespace TravelPlan.Web.Components.Calendar;

public static class CalendarMath
{
    /// <summary>Sunday-starting week, matching the mockup's S M T W T F S header.</summary>
    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);
}
