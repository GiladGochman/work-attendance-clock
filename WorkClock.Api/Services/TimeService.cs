using System.Text.Json;
using System.Text.Json.Serialization;
using WorkClock.Api.Exceptions;

namespace WorkClock.Api.Services;

/// <summary>
/// Fetches the current time from timeapi.io and converts to UTC.
/// Uses a named HttpClient registered in Program.cs via IHttpClientFactory.
/// </summary>
public class TimeService(IHttpClientFactory httpClientFactory, ILogger<TimeService> logger) : ITimeService
{
    // The field name in the JSON payload is "date_time", not "dateTime".
    // System.Text.Json does NOT auto-camel-case multi-word snake_case fields,
    // so we need an explicit DTO with [JsonPropertyName].
    private sealed class TimeApiResponse
    {
        [JsonPropertyName("date_time")]
        public string? DateTime { get; set; }
    }

    private const string ClientName = "TimeApi";
    private const string TimeZone  = "Europe/Zurich";

    public async Task<DateTime> GetNowAsync()
    {
        HttpClient client;
        HttpResponseMessage response;

        try
        {
            client   = httpClientFactory.CreateClient(ClientName);
            response = await client.GetAsync($"api/v1/time/current/zone?timeZone={TimeZone}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not TimeServiceException)
        {
            logger.LogError(ex, "Failed to reach the external time API.");
            throw new TimeServiceException("The external time service is unavailable. Please try again shortly.", ex);
        }

        string json = await response.Content.ReadAsStringAsync();

        TimeApiResponse? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TimeApiResponse>(json);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not deserialize time API response. Raw JSON: {Json}", json);
            throw new TimeServiceException("Received an unexpected response from the external time service.", ex);
        }

        if (string.IsNullOrWhiteSpace(dto?.DateTime))
        {
            logger.LogError("Time API response missing 'date_time' field. Raw JSON: {Json}", json);
            throw new TimeServiceException("The external time service returned a response without a 'date_time' value.");
        }

        // The value is an ISO-8601 string with timezone offset, e.g. "2026-04-26T21:46:16.092251+02:00".
        // Parse as DateTimeOffset to preserve the offset, then convert to UTC with .UtcDateTime.
        if (!DateTimeOffset.TryParse(dto.DateTime, out DateTimeOffset parsed))
        {
            logger.LogError("Could not parse 'date_time' value '{Value}' as a DateTimeOffset.", dto.DateTime);
            throw new TimeServiceException($"Could not parse the date/time value returned by the external time service: '{dto.DateTime}'.");
        }

        // Return the Zurich local time (not UTC). SpecifyKind(Utc) is used so
        // ASP.NET Core's JSON serializer emits a trailing 'Z', which prevents browsers
        // from applying their own timezone offset when parsing the value.
        return DateTime.SpecifyKind(parsed.DateTime, DateTimeKind.Utc);
    }
}
