namespace TravelPlan.Tests;

/// <summary>
/// Minimal stand-in for an external HTTP dependency (Frankfurter, Azure Maps) — returns whatever
/// the given function produces instead of making a real network call. Shared by
/// ExchangeRateServiceTests and MapsServiceTests.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
