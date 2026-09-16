using System.Net.Http.Json;

namespace TravelPlan.Web.Services;

public abstract class ApiClientBase
{
    protected readonly HttpClient Http;

    protected ApiClientBase(HttpClient http)
    {
        Http = http;
    }

    protected async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(uri, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    protected async Task<T?> GetOrDefaultAsync<T>(string uri, CancellationToken cancellationToken = default) where T : class
    {
        var response = await Http.GetAsync(uri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<T>(response, cancellationToken);
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
