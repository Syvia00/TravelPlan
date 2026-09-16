namespace TravelPlan.Web.Services;

/// <summary>Thrown for any non-success API response. Errors is populated from a
/// ValidationProblemDetails body when the API returned field-level validation errors.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ApiException(int statusCode, string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}
