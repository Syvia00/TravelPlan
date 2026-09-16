using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TravelPlan.Shared.DTOs.TripMemories.Reports;

namespace TravelPlan.Api.Services.Reports;

public interface IReportHtmlRenderer
{
    /// <summary>Renders a report — Itinerary or MemorySummary — as a static HTML page. The
    /// template branches on report.ReportType internally; same component either way.</summary>
    Task<string> RenderAsync(TripReportDto report);
}

/// <summary>Renders report data as a static HTML page via a Razor component, using ASP.NET
/// Core's HtmlRenderer — no Blazor hosting involved, just component-to-string.</summary>
public class ReportHtmlRenderer : IReportHtmlRenderer
{
    private readonly HtmlRenderer _htmlRenderer;

    public ReportHtmlRenderer(HtmlRenderer htmlRenderer)
    {
        _htmlRenderer = htmlRenderer;
    }

    public Task<string> RenderAsync(TripReportDto report)
    {
        return _htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(TripReportTemplate.Report)] = report,
            });

            var output = await _htmlRenderer.RenderComponentAsync<TripReportTemplate>(parameters);
            return output.ToHtmlString();
        });
    }
}
