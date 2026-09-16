using System.Text.Json;
using System.Text.Json.Serialization;

namespace TravelPlan.Api.Services.Reports;

/// <summary>
/// Options for serializing/deserializing TripMemory.ReportData. Kept separate from ASP.NET
/// Core's MVC JSON options (Program.cs) since that pipeline only covers controller
/// request/response bodies, not this ad-hoc persisted blob — but uses the same string-enum
/// convention for consistency.
/// </summary>
public static class ReportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
