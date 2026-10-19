using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace TravelPlan.Web.Services;

public abstract class ApiClientBase
{
    protected readonly HttpClient Http;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes =
    [
        HttpStatusCode.BadGateway,         // 502
        HttpStatusCode.ServiceUnavailable, // 503
        HttpStatusCode.GatewayTimeout,     // 504
    ];

    private readonly ILogger<ApiClientBase> _logger;

    protected ApiClientBase(HttpClient http, ILogger<ApiClientBase> logger)
    {
        Http = http;
        _logger = logger;
    }

    protected async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken = default)
    {
        var response = await GetWithRetryAsync(uri, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    protected async Task<T?> GetOrDefaultAsync<T>(string uri, CancellationToken cancellationToken = default) where T : class
    {
        var response = await GetWithRetryAsync(uri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    /// <summary>GET only — a cold Container App/database can fail outright or time out on the very
    /// first request after sitting idle; one retry ~2s later is usually enough to land on an
    /// already-warm instance. Deliberately never used for POST/PUT/DELETE: retrying one of those
    /// after an ambiguous failure could duplicate a write the server already applied. Logs the
    /// first failure at Warning — with the exception type or status code — before retrying, so a
    /// real occurrence is recorded even when the retry succeeds and the user never sees anything.
    /// The TaskCanceledException catch only treats this as a retryable timeout when the caller's
    /// own cancellationToken wasn't the cause (component disposed, navigated away, etc.) — that
    /// case must propagate immediately, not retry.</summary>
    private async Task<HttpResponseMessage> GetWithRetryAsync(string uri, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(uri, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "GET {Uri} failed with {ExceptionType}; retrying once after {Delay}", uri, ex.GetType().Name, RetryDelay);
            await Task.Delay(RetryDelay, cancellationToken);
            return await Http.GetAsync(uri, cancellationToken);
        }

        if (RetryableStatusCodes.Contains(response.StatusCode))
        {
            _logger.LogWarning("GET {Uri} returned {StatusCode}; retrying once after {Delay}", uri, (int)response.StatusCode, RetryDelay);
            response.Dispose();
            await Task.Delay(RetryDelay, cancellationToken);
            return await Http.GetAsync(uri, cancellationToken);
        }

        return response;
    }

    protected async Task<T> PostAsync<T>(string uri, object body, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(uri, body, JsonDefaults.Options, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    protected async Task<T> PutAsync<T>(string uri, object body, CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync(uri, body, JsonDefaults.Options, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    protected async Task DeleteAsync(string uri, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }
    }

    protected async Task<T> PostMultipartAsync<T>(string uri, HttpContent content, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync(uri, content, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    /// <summary>For an endpoint that returns the resource's raw bytes (e.g. a photo) rather than
    /// JSON — see PlanItemPhotosApiClient.DownloadAsync.</summary>
    protected async Task<byte[]> GetBytesAsync(string uri, CancellationToken cancellationToken = default)
    {
        var response = await GetWithRetryAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken);
        return result ?? throw new ApiException((int)response.StatusCode, "The API returned an empty response body.");
    }

    private static async Task<ApiException> BuildExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(JsonDefaults.Options, cancellationToken);
            if (problem?.Errors is { Count: > 0 })
            {
                return new ApiException(status, problem.Title ?? "Request failed validation.", problem.Errors);
            }

            return new ApiException(status, problem?.Detail ?? problem?.Title ?? $"Request failed with status {status}.");
        }
        catch
        {
            return new ApiException(status, $"Request failed with status {status}.");
        }
    }

    private class ValidationProblemBody
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }

        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
