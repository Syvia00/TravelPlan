using System.Text.Json;
using System.Text.Json.Serialization;

namespace TravelPlan.Web.Services;

/// <summary>Matches TravelPlan.Api's Program.cs JSON config (string enums) so DTOs round-trip.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
